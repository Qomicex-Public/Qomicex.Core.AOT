namespace Qomicex.Core.AOT.Exceptions;

public class ResourceCompletionException : Exception
{
    public ResourceCompletionException() { }

    public ResourceCompletionException(string message) : base(message) { }

    public ResourceCompletionException(string message, Exception inner) : base(message, inner) { }
}
