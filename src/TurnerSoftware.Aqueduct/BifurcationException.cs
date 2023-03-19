namespace TurnerSoftware.Aqueduct;

/// <summary>
/// An exception specifically for capturing errors that arise from bifurcation.
/// </summary>
public class BifurcationException : Exception
{
	internal BifurcationException() : base() { }
    internal BifurcationException(string? message) : base(message) { }
    internal BifurcationException(string? message, Exception? innerException) : base(message, innerException) { }
}
