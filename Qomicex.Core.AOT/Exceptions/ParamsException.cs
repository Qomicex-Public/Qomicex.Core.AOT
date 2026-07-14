namespace Qomicex.Core.AOT.Exceptions;

public class ParamsException : Exception
{
    public ParamsException() { }

    public ParamsException(string message) : base(message) { }

    public ParamsException(string message, Exception inner) : base(message, inner) { }
}
