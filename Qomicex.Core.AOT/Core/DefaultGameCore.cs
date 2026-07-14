using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Services;

namespace Qomicex.Core.AOT.Core;

public sealed class DefaultGameCore : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    public IVersionManagementService Version { get; }
    internal IAuthProvider Auth { get; }
    internal ILaunchExecutor Launch { get; }

    internal DefaultGameCore(
        IVersionManagementService version,
        IAuthProvider auth,
        ILaunchExecutor launch,
        HttpClient http)
    {
        Version = version;
        Auth = auth;
        Launch = launch;
        _http = http;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
    }
}
