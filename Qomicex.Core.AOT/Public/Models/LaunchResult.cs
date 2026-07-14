namespace Qomicex.Core.AOT.Public.Models;

public sealed class LaunchResult
{
    public bool Success { get; init; }
    public int ProcessId { get; init; }
    public string? Message { get; init; }
    public Exception? Exception { get; init; }

    public Action<string>? OnOutput = null;
    public Action<string>? OnError = null;
    public Action<int>? OnExit = null;
}
