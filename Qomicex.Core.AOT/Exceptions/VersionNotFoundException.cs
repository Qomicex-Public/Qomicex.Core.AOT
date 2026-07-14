namespace Qomicex.Core.AOT.Exceptions;

public class VersionNotFoundException : Exception
{
    public VersionNotFoundException() { }

    public VersionNotFoundException(string message) : base(message) { }

    public VersionNotFoundException(string message, Exception inner) : base(message, inner) { }
}
