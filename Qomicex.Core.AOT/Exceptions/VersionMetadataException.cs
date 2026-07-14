namespace Qomicex.Core.AOT.Exceptions;

public class VersionMetadataException : Exception
{
    public VersionMetadataException() { }

    public VersionMetadataException(string message) : base(message) { }

    public VersionMetadataException(string message, Exception inner) : base(message, inner) { }
}
