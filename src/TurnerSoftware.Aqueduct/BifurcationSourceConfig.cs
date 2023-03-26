namespace TurnerSoftware.Aqueduct;

/// <summary>
/// Manages the configuration for bifurcation.
/// </summary>
public class BifurcationSourceConfig
{
	internal const int DefaultMinReadBufferSize = 4096;

	/// <summary>
	/// The default configuration for <see cref="BifurcationSourceConfig"/>.
	/// </summary>
	public static readonly BifurcationSourceConfig DefaultConfig = new();

	/// <summary>
	/// The minimum read buffer size before writing data to the targets. When -1 is set, there is no minimum read buffer size.
	/// </summary>
	public int MinReadBufferSize { get; }
	/// <summary>
	/// Whether to bubble exceptions during bifurcation to the calling code.
	/// </summary>
	public bool BubbleExceptions { get; }
	/// <summary>
	/// The token to monitor for cancellation requests.
	/// </summary>
	public CancellationToken CancellationToken { get; }

	/// <summary>
	/// Creates a new <see cref="BifurcationSourceConfig"/>.
	/// </summary>
	/// <param name="minReadBufferSize">The minimum read buffer size before writing data to the targets. Use -1 to specify no minimum read buffer size.</param>
	/// <param name="bubbleExceptions">Whether to bubble exceptions during bifurcation to the calling code.</param>
	/// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
	/// <exception cref="ArgumentException"></exception>
	public BifurcationSourceConfig(
		int minReadBufferSize = DefaultMinReadBufferSize,
		bool bubbleExceptions = true,
		CancellationToken cancellationToken = default
	)
	{
		if (minReadBufferSize != -1 && minReadBufferSize <= 0)
		{
			throw new ArgumentException($"Invalid value for {nameof(MinReadBufferSize)}. Must be a value greater than 0, or if there is no restriction, -1.", nameof(minReadBufferSize));
		}

		MinReadBufferSize = minReadBufferSize;
		BubbleExceptions = bubbleExceptions;
		CancellationToken = cancellationToken;
	}
}

/// <summary>
/// Manages the configuration for stream bifurcation.
/// </summary>
public class StreamBifurcationSourceConfig : BifurcationSourceConfig
{
	/// <summary>
	/// The default configuration for <see cref="StreamBifurcationSourceConfig"/>.
	/// </summary>
	public static readonly StreamBifurcationSourceConfig DefaultStreamConfig = new();

	/// <summary>
	/// Whether to leave the stream open after reading has completed.
	/// </summary>
	public bool LeaveOpen { get; }

	/// <summary>
	/// Creates a new <see cref="StreamBifurcationSourceConfig"/>.
	/// </summary>
	/// <param name="leaveOpen">Whether to leave the stream open after reading has completed.</param>
	/// <param name="minReadBufferSize">The minimum read buffer size before writing data to the targets. Use -1 to specify no minimum read buffer size.</param>
	/// <param name="bubbleExceptions">Whether to bubble exceptions during bifurcation to the calling code.</param>
	/// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
	/// <exception cref="ArgumentException"></exception>
	public StreamBifurcationSourceConfig(
		bool leaveOpen = false,
		int minReadBufferSize = DefaultMinReadBufferSize,
		bool bubbleExceptions = true,
		CancellationToken cancellationToken = default
	) : base(minReadBufferSize, bubbleExceptions, cancellationToken)
	{
		LeaveOpen = leaveOpen;
	}
}