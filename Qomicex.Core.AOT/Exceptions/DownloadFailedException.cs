namespace Qomicex.Core.AOT.Exceptions;

public class DownloadFailedException : Exception
{
    public DownloadFailedException() { }

    public DownloadFailedException(string message) : base(message) { }

    public DownloadFailedException(string message, Exception inner) : base(message, inner) { }
}
