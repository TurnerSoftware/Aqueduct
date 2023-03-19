using System.IO.Pipelines;

namespace TurnerSoftware.Aqueduct;

/// <summary>
/// Manages the configuration for a specific bifurcation target.
/// </summary>
public class BifurcationTargetConfig
{
	internal const int DefaultBlockAfter = 32768;
	internal const int DefaultResumeAfter = 16384;
	internal const int DefaultMaxTotalBytes = -1;

    /// <summary>
    /// The reader function that will handle this specific bifurcation target.
    /// </summary>
    public Func<PipeReader, CancellationToken, Task> Reader { get; }
    /// <summary>
    /// The number of unread bytes before writing will block to the bifurcation target.
    /// </summary>
    public int BlockAfter { get; }
    /// <summary>
    /// The number of unread bytes before resuming writing to the bifurcation target.
    /// </summary>
    public int ResumeAfter { get; }
    /// <summary>
    /// The max number of bytes to write to the bifurcation target.
    /// </summary>
    public int MaxTotalBytes { get; }

    /// <summary>
    /// Creates a new <see cref="BifurcationTargetConfig"/> for <see cref="Stream"/>-based readers.
    /// </summary>
    /// <param name="reader">The reader function that will handle this specific bifurcation target.</param>
    /// <param name="blockAfter">The number of unread bytes before writing will block to the bifurcation target. This must be the same or greater than <paramref name="resumeAfter"/>.</param>
    /// <param name="resumeAfter">The number of unread bytes before resuming writing to the bifurcation target. This must be the same or lower than <paramref name="blockAfter"/>.</param>
    /// <param name="maxTotalBytes">The max number of bytes to write to the bifurcation target. Use -1 to specify no limit.</param>
    /// <exception cref="ArgumentException"></exception>
    public BifurcationTargetConfig(
        Func<Stream, CancellationToken, Task> reader,
        int blockAfter = DefaultBlockAfter,
        int resumeAfter = DefaultResumeAfter,
        int maxTotalBytes = DefaultMaxTotalBytes
    ) : this(
        (pipeReader, cancellationToken) => reader(pipeReader.AsStream(), cancellationToken),
        blockAfter,
        resumeAfter,
        maxTotalBytes
    )
    { }

    /// <summary>
    /// Creates a new <see cref="BifurcationTargetConfig"/> for <see cref="PipeReader"/>-based readers.
    /// </summary>
    /// <param name="reader">The reader function that will handle this specific bifurcation target.</param>
    /// <param name="blockAfter">The number of unread bytes before writing will block to the bifurcation target. This must be the same or greater than <paramref name="resumeAfter"/>.</param>
    /// <param name="resumeAfter">The number of unread bytes before resuming writing to the bifurcation target. This must be the same or lower than <paramref name="blockAfter"/>.</param>
    /// <param name="maxTotalBytes">The max number of bytes to write to the bifurcation target. Use -1 to specify no limit.</param>
    /// <exception cref="ArgumentException"></exception>
    public BifurcationTargetConfig(
        Func<PipeReader, CancellationToken, Task> reader,
        int blockAfter = DefaultBlockAfter,
        int resumeAfter = DefaultResumeAfter,
        int maxTotalBytes = DefaultMaxTotalBytes
    )
    {
        if (blockAfter < resumeAfter)
        {
            throw new ArgumentException($"{nameof(BlockAfter)} must be equal to or greater than {nameof(ResumeAfter)}", nameof(blockAfter));
        }

        if (maxTotalBytes != -1 && maxTotalBytes <= 0)
        {
            throw new ArgumentException($"Invalid value for {nameof(MaxTotalBytes)}. Must be a value greater than 0, or if there is no limit, -1.", nameof(maxTotalBytes));
        }

        Reader = reader;
        BlockAfter = blockAfter;
        ResumeAfter = resumeAfter;
        MaxTotalBytes = maxTotalBytes;
    }
}
