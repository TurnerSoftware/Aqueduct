using System.Buffers;
using System.IO.Pipelines;

namespace TurnerSoftware.Aqueduct;

internal static class PipeBifurcation
{
	private class BifurcationState
	{
		private readonly Pipe Pipe;

		private Task? ReaderTask;
		private int RemainingBytes;

		public readonly BifurcationTargetConfig Config;

		public BifurcationState(BifurcationTargetConfig config)
		{
			Config = config;

			Pipe = new Pipe(new PipeOptions(
				pauseWriterThreshold: config.BlockAfter,
				resumeWriterThreshold: config.ResumeAfter
			));

			RemainingBytes = config.MaxTotalBytes;
		}

		public bool IsCompleted { get; private set; }

		public void StartReader(CancellationToken cancellationToken)
		{
			ReaderTask = Config.Reader(Pipe.Reader, cancellationToken);
		}

		/// <summary>
		/// Using the <paramref name="buffer"/>, writes as much as configured to the bifurcation target.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="cancellationToken"></param>
		/// <returns>Whether the target can still be written to.</returns>
		public async ValueTask<bool> WriteAsync(ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
		{
			if (IsCompleted)
			{
				return false;
			}

			//Await faulted readers to correctly bubble exceptions
			if (ReaderTask is not null && ReaderTask.IsFaulted)
			{
				await ReaderTask;
			}

			var bytesToRead = (int)buffer.Length;
			if (RemainingBytes != -1)
			{
				bytesToRead = Math.Min(RemainingBytes, bytesToRead);
			}

			var destination = Pipe.Writer.GetMemory(bytesToRead);
			buffer.CopyTo(destination.Span);

			Pipe.Writer.Advance(bytesToRead);

			var flushResult = await Pipe.Writer.FlushAsync(cancellationToken);

			if (RemainingBytes != -1)
			{
				RemainingBytes -= bytesToRead;
				if (RemainingBytes == 0)
				{
					return false;
				}
			}

			return !flushResult.IsCompleted;
		}

		/// <summary>
		/// Completes the bifurcation target and awaits the reader. Any exceptions the reader throws will bubble out.
		/// </summary>
		/// <returns></returns>
		public async Task CompleteAsync()
		{
			if (IsCompleted)
			{
				return;
			}

			IsCompleted = true;
			await Pipe.Writer.CompleteAsync();

			//Run the task to completion
			if (ReaderTask is not null)
			{
				await ReaderTask;
			}
		}

		/// <summary>
		/// Completes the bifurcation target in a faulted state, awaiting the reader and exception handler.
		/// No exceptions from either the reader or exception handler will bubble.
		/// </summary>
		/// <param name="exception">The exception used to trigger the faulted state.</param>
		/// <returns></returns>
		public async Task CompleteWithExceptionAsync(Exception exception)
		{
			await Pipe.Writer.CompleteAsync(exception);
			if (ReaderTask is not null)
			{
				//Ensure that the reader task has completed execution (faulted or not)
				if (!ReaderTask.IsCompleted && !ReaderTask.IsFaulted)
				{
					try
					{
						await ReaderTask;
					}
					catch
					{
						//Ignore any exceptions
					}
				}

				//Trigger any custom exception handler
				if (Config.ExceptionHandler is not null)
				{
					try
					{
						await Config.ExceptionHandler(exception);
					}
					catch
					{
						//Ignore any exceptions
					}
				}
			}
		}
	}

	public static async Task BifurcatedReadAsync(PipeReader sourceReader, BifurcationSourceConfig sourceConfig, params BifurcationTargetConfig[] targetConfigs)
	{
		if (targetConfigs.Length == 0)
		{
			throw new ArgumentException("No target configurations to bifurcate the source reader to", nameof(targetConfigs));
		}

		var earlyCompletedTargets = 0;
		var targets = new BifurcationState[targetConfigs.Length];

		for (var i = 0; i < targetConfigs.Length; i++)
		{
			targets[i] = new(targetConfigs[i]);
			targets[i].StartReader(sourceConfig.CancellationToken);
		}

		try
		{
			while (true)
			{
				var result = await sourceReader.ReadAsync(sourceConfig.CancellationToken);
				var buffer = result.Buffer;

				if (buffer.IsEmpty && result.IsCompleted)
				{
					break;
				}

				//Ensure a minimum buffer size (if configured)
				if (!result.IsCompleted && sourceConfig.MinReadBufferSize != -1 && buffer.Length < sourceConfig.MinReadBufferSize)
				{
					sourceReader.AdvanceTo(buffer.Start, buffer.End);
					continue;
				}

				for (var i = 0; i < targets.Length; i++)
				{
					var target = targets[i];
					if (target.IsCompleted)
					{
						continue;
					}

					var canKeepWriting = await target.WriteAsync(buffer, sourceConfig.CancellationToken);
					if (!canKeepWriting)
					{
						await target.CompleteAsync();
						earlyCompletedTargets++;
					}
				}

				//Exit reading early if all targets have completed
				if (earlyCompletedTargets == targets.Length)
				{
					break;
				}

				sourceReader.AdvanceTo(buffer.End);
			}

			//Complete reader and all branch writers
			await sourceReader.CompleteAsync();
			for (var i = 0; i < targets.Length; i++)
			{
				var target = targets[i];
				await target.CompleteAsync();
			}
		}
		catch (Exception innerException)
		{
			var exception = new BifurcationException("An exception occurred during bifurcation", innerException);

			await sourceReader.CompleteAsync(exception);
			for (var i = 0; i < targets.Length; i++)
			{
				var target = targets[i];
				await target.CompleteWithExceptionAsync(exception);
			}

			if (sourceConfig.BubbleExceptions)
			{
				throw exception;
			}
		}
	}
}