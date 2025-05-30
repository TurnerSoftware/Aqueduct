using System.IO.Pipelines;
using System.Text;

namespace TurnerSoftware.Aqueduct.Tests;

[TestClass]
public class PipeBifurcationTests
{
	private static PipeReader CreateSource(string value) => PipeReader.Create(new(Encoding.ASCII.GetBytes(value)));

	private static Func<PipeReader, CancellationToken, Task> CreateStringTarget(Func<Task<string>, Task> assertion)
	{
		return (reader, cancellationToken) =>
		{
			static async Task<string> ReadFullAsync(PipeReader reader, CancellationToken cancellationToken)
			{
				using var stream = reader.AsStream();
				using var a = new StreamReader(stream);
				return await a.ReadToEndAsync();
			}
			return assertion(ReadFullAsync(reader, cancellationToken));
		};
	}

	[TestMethod]
	public async Task NoTargets_ThrowsException()
	{
		var source = CreateSource("Test Value");

		await FluentActions.Awaiting(() => PipeBifurcation.BifurcatedReadAsync<object>(source, BifurcationSourceConfig.DefaultConfig))
			.Should().ThrowAsync<ArgumentException>()
			.WithMessage("No target configurations*");
	}

	[TestMethod]
	public async Task SingleTarget_TargetExceptionBubbles()
	{
		var source = CreateSource("Test Value");

		await FluentActions.Awaiting(() => PipeBifurcation.BifurcatedReadAsync(source, BifurcationSourceConfig.DefaultConfig, new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					await resultTask;
					throw new ApplicationException("My processing exception");
				})
			)))
			.Should()
			.ThrowAsync<BifurcationException>()
				.WithInnerException<BifurcationException, ApplicationException>()
				.WithMessage("My processing exception");
	}

	[TestMethod]
	public async Task SingleTarget_DefaultConfig_Success()
	{
		var source = CreateSource("Test Value");
		var targetReaderHasCompleted = false;

		await PipeBifurcation.BifurcatedReadAsync(
			source,
			BifurcationSourceConfig.DefaultConfig,
			new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					var result = await resultTask;
					result.Should().Be("Test Value");
					targetReaderHasCompleted = true;
				})
			)
		);

		targetReaderHasCompleted.Should().BeTrue();
	}

	[TestMethod]
	public async Task SingleTarget_ReaderCompletesEarly_Success()
	{
		var source = new Pipe();
		var buffer = new byte[16];
		await source.Writer.WriteAsync(buffer);
		var targetReaderHasCompleted = false;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			source.Reader,
			new BifurcationSourceConfig(minReadBufferSize: -1),
			new BifurcationTargetConfig(
				async (Stream reader, CancellationToken cancellationToken) =>
				{
					var buffer = new byte[1];
					await reader.ReadAsync(buffer, cancellationToken);
					targetReaderHasCompleted = true;
				},
				//These are set to exaggerate the problem where exiting early
				//still has the pipe being fed bytes till the point it blocks
				blockAfter: 16,
				resumeAfter: 8
			)
		);

		await source.Writer.WriteAsync(buffer);
		await source.Writer.CompleteAsync();
		await bifurcationTask;
		targetReaderHasCompleted.Should().BeTrue();
	}

	[TestMethod]
	public async Task MultiTarget_DefaultConfig_Success()
	{
		var source = CreateSource("Test Value");
		var completedTargetReaders = 0;

		await PipeBifurcation.BifurcatedReadAsync(source,
			BifurcationSourceConfig.DefaultConfig,
			new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					var result = await resultTask;
					result.Should().Be("Test Value");
					Interlocked.Increment(ref completedTargetReaders);
				})
			),
			new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					var result = await resultTask;
					result.Should().Be("Test Value");
					Interlocked.Increment(ref completedTargetReaders);
				})
			)
		);

		completedTargetReaders.Should().Be(2);
	}

	[TestMethod]
	public async Task SingleTarget_ConfiguredMaxTotalBytes_LimitsBytes()
	{
		var source = CreateSource("Test Value");

		await PipeBifurcation.BifurcatedReadAsync(
			source,
			BifurcationSourceConfig.DefaultConfig,
			new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					var result = await resultTask;
					result.Should().Be("Test");
				}),
				maxTotalBytes: 4
			)
		);
	}

	[TestMethod]
	public async Task MultiTarget_ConfiguredMaxTotalBytesForOne_LimitsBytesOnlyForConfigured()
	{
		var source = CreateSource("Test Value");

		await PipeBifurcation.BifurcatedReadAsync(
			source,
			BifurcationSourceConfig.DefaultConfig,
			new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					var result = await resultTask;
					result.Should().Be("Test");
				}),
				maxTotalBytes: 4
			),
			new BifurcationTargetConfig(
				CreateStringTarget(async resultTask =>
				{
					var result = await resultTask;
					result.Should().Be("Test Value");
				})
			)
		);
	}

	[TestMethod]
	public async Task MultiTarget_CompletedTargets_AreNotWrittenToFurther()
	{
		var sourcePipe = new Pipe();

		var firstTargetReadBufferLength = -1L;
		var firstTargetReaderIsComplete = false;
		var secondTargetReadBufferLength = -1L;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					var firstResult = await reader.ReadAsync(cancellationToken);
					firstTargetReadBufferLength = firstResult.Buffer.Length;
					reader.AdvanceTo(firstResult.Buffer.End);
					var secondResult = await reader.ReadAsync(cancellationToken);
					firstTargetReadBufferLength += secondResult.Buffer.Length;
					reader.AdvanceTo(secondResult.Buffer.End);
					var thirdResult = await reader.ReadAsync(cancellationToken);
					firstTargetReaderIsComplete = thirdResult.IsCompleted;
				},
				maxTotalBytes: 6
			),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					var firstResult = await reader.ReadAsync(cancellationToken);
					secondTargetReadBufferLength = firstResult.Buffer.Length;
					reader.AdvanceTo(firstResult.Buffer.End);
					var secondResult = await reader.ReadAsync(cancellationToken);
					secondTargetReadBufferLength += secondResult.Buffer.Length;
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should().CompleteWithinAsync(TimeSpan.FromSeconds(1));
		firstTargetReadBufferLength.Should().Be(6);
		firstTargetReaderIsComplete.Should().BeTrue();
		secondTargetReadBufferLength.Should().Be(8);
	}

	private record TestResultData(long TargetOneValue, long TargetTwoValue);

	[TestMethod]
	public async Task MultiTarget_ResultsAreReturnedInOrderFromTargets()
	{
		var source = CreateSource("Test Value");

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			source,
			new BifurcationSourceConfig(),
			new BifurcationTargetConfig<TestResultData>(
				async (reader, cancellationToken) =>
				{
					var result = await reader.ReadAsync(cancellationToken);
					return new(result.Buffer.Length, 0);
				}
			),
			new BifurcationTargetConfig<TestResultData>(
				async (reader, cancellationToken) =>
				{
					var result = await reader.ReadAsync(cancellationToken);
					return new(0, result.Buffer.Length);
				}
			)
		);

		var result = await bifurcationTask;
		result.Should().NotBeNull().And.HaveCount(2);
		result[0].Should().BeEquivalentTo(new TestResultData(10, 0));
		result[1].Should().BeEquivalentTo(new TestResultData(0, 10));
	}
	
	[TestMethod]
	public async Task MultiTarget_NonBubblingExceptionsStillReturnResultsThatCompleted()
	{
		var source = CreateSource("Test Value");

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			source,
			new BifurcationSourceConfig(bubbleExceptions: false),
			new BifurcationTargetConfig<bool>(
				(PipeReader reader, CancellationToken cancellationToken) =>
				{
					throw new Exception("Whoops");
				}
			),
			new BifurcationTargetConfig<bool>(
				(PipeReader reader, CancellationToken cancellationToken) => Task.FromResult(true)
			)
		);

		var result = await bifurcationTask;
		result.Should().NotBeNull().And.HaveCount(2);
		result[0].Should().BeFalse();
		result[1].Should().BeTrue();
	}

	[TestMethod]
	public async Task MultiTarget_ExceptionsFromOtherTargets_PushesExceptionsToTargets()
	{
		var sourcePipe = new Pipe();

		Exception targetReaderException = null!;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4, bubbleExceptions: false),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					await reader.ReadAsync(cancellationToken);
					throw new Exception("TargetException");
				}
			),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					try
					{
						var readResult = await reader.ReadAsync(cancellationToken);
						reader.AdvanceTo(readResult.Buffer.End);
						await reader.ReadAsync(cancellationToken);
					}
					catch (Exception ex)
					{
						targetReaderException = ex;
					}
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should()
			.NotThrowAsync<BifurcationException>();

		targetReaderException.Should().BeOfType<BifurcationException>()
			.Subject.InnerException!.Message.Should().Be("TargetException");
	}

	[TestMethod]
	public async Task MultiTarget_ExceptionsFromOtherTargets_ExceptionHandlerIsTriggered()
	{
		var sourcePipe = new Pipe();

		Exception targetReaderException = null!;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4, bubbleExceptions: false),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					await reader.ReadAsync(cancellationToken);
					throw new Exception("TargetException");
				}
			),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					var readResult = await reader.ReadAsync(cancellationToken);
					reader.AdvanceTo(readResult.Buffer.End);
					await reader.ReadAsync(cancellationToken);
				},
				exception =>
				{
					targetReaderException = exception;
					return Task.CompletedTask;
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should()
			.NotThrowAsync<BifurcationException>();

		targetReaderException.Should().BeOfType<BifurcationException>()
			.Subject.InnerException!.Message.Should().Be("TargetException");
	}

	[TestMethod]
	public async Task SingleTarget_ExceptionsFromSelf_ExceptionHandlerIsTriggered()
	{
		var sourcePipe = new Pipe();

		Exception targetReaderException = null!;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4, bubbleExceptions: false),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					await reader.ReadAsync(cancellationToken);
					throw new Exception("TargetException");
				},
				exception =>
				{
					targetReaderException = exception;
					return Task.CompletedTask;
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should()
			.NotThrowAsync<BifurcationException>();

		targetReaderException.Should().BeOfType<BifurcationException>()
			.Subject.InnerException!.Message.Should().Be("TargetException");
	}

	[TestMethod]
	public async Task SourceConfig_MinimumBufferSize_BufferIsAtLeastMinimum()
	{
		var sourcePipe = new Pipe();
		var readBufferLength = -1L;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					var result = await reader.ReadAsync(cancellationToken);
					readBufferLength = result.Buffer.Length;
					await reader.CompleteAsync();
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should().CompleteWithinAsync(TimeSpan.FromSeconds(1));
		readBufferLength.Should().Be(4);
	}

	[TestMethod]
	public async Task SourceConfig_MinimumBufferSize_MinimumDoesNotImpactLargerSize()
	{
		var sourcePipe = new Pipe();
		var readBufferLength = -1L;

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					var result = await reader.ReadAsync(cancellationToken);
					readBufferLength = result.Buffer.Length;
					await reader.CompleteAsync();
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[6]);
			await Task.Delay(100);
			await sourcePipe.Writer.WriteAsync(new byte[2]);
			await Task.Delay(100);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should().CompleteWithinAsync(TimeSpan.FromSeconds(1));
		readBufferLength.Should().Be(6);
	}

	[TestMethod]
	public async Task SourceConfig_BubbleExceptions_Enabled()
	{
		var sourcePipe = new Pipe();

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					await reader.ReadAsync(cancellationToken);
					throw new Exception("TargetException");
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should()
			.ThrowAsync<BifurcationException>()
				.WithInnerException<BifurcationException, Exception>()
				.WithMessage("TargetException");
	}

	[TestMethod]
	public async Task SourceConfig_BubbleExceptions_Disabled()
	{
		var sourcePipe = new Pipe();

		var bifurcationTask = PipeBifurcation.BifurcatedReadAsync(
			sourcePipe.Reader,
			new BifurcationSourceConfig(minReadBufferSize: 4, bubbleExceptions: false),
			new BifurcationTargetConfig(
				async (reader, cancellationToken) =>
				{
					await reader.ReadAsync(cancellationToken);
					throw new Exception("TargetException");
				}
			)
		);

		var action = async () =>
		{
			await sourcePipe.Writer.WriteAsync(new byte[4]);
			await sourcePipe.Writer.CompleteAsync();
			await bifurcationTask;
		};

		await action.Should()
			.NotThrowAsync<BifurcationException>();
	}
}