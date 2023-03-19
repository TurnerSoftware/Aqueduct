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

        public async ValueTask<bool> WriteAsync(ReadOnlySequence<byte> buffer, CancellationToken cancellationToken)
        {
            if (IsCompleted)
            {
                return false;
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

            RemainingBytes -= bytesToRead;
            IsCompleted = flushResult.IsCompleted || RemainingBytes == 0;

            return flushResult.IsCompleted;
        }

        public ValueTask CompleteWriterAsync(Exception? exception = null)
        {
            IsCompleted = true;
            return Pipe.Writer.CompleteAsync(exception);
        }

        public Task RunToCompletionAsync()
        {
            if (ReaderTask is not null)
            {
                return ReaderTask;
            }
            return Task.CompletedTask;
        }

        public async Task TryRunToCompletionAsync()
        {
            if (ReaderTask is not null)
            {
                if (ReaderTask.IsFaulted || ReaderTask.IsCompleted)
                {
                    return;
                }

                try
                {
                    await ReaderTask;
                }
                catch
                {
                    //Ignore any exceptions
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

                    var isComplete = await target.WriteAsync(buffer, sourceConfig.CancellationToken);
                    if (isComplete)
                    {
                        //It IS NOT an error if the reader no longer needs further writes
                        //It IS an error if the task is faulted so we need to await the task to ensure that
                        await target.RunToCompletionAsync();
                    }
                }

                sourceReader.AdvanceTo(buffer.End);
            }

            //Complete reader and all branch writers
            await sourceReader.CompleteAsync();
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                await target.CompleteWriterAsync();
                await target.RunToCompletionAsync();
            }
        }
        catch (Exception innerException)
        {
            var exception = new BifurcationException("An exception occurred during bifurcation", innerException);

            await sourceReader.CompleteAsync(exception);
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                await target.CompleteWriterAsync(exception);

                //This ensures that all reader tasks are no longer running before we bubble the exception
                await target.TryRunToCompletionAsync();
            }

            throw exception;
        }
    }
}