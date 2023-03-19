namespace TurnerSoftware.Aqueduct;

public class BifurcationException : Exception
{
    public BifurcationException(string? message) : base(message) { }
    public BifurcationException(string? message, Exception? innerException) : base(message, innerException) { }
}
