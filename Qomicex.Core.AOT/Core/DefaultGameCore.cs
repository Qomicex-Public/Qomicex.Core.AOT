using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Public.Expansion;
using Qomicex.Core.AOT.Services.Expansion.CurseForge;
using Qomicex.Core.AOT.Services.Expansion.FeedTheBeast;
using Qomicex.Core.AOT.Services.Expansion.Modrinth;

namespace Qomicex.Core.AOT.Core;

public sealed class DefaultGameCore : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    public HttpClient HttpClient { get; }
    public string GameRoot { get; }
    public IVersionManagementService Version { get; }
    public IAuthProvider Auth { get; }
    public ILaunchExecutor Launch { get; }

    internal DefaultGameCore(
        IVersionManagementService version,
        IAuthProvider auth,
        ILaunchExecutor launch,
        HttpClient http,
        string gameRoot)
    {
        Version = version;
        Auth = auth;
        Launch = launch;
        HttpClient = http;
        GameRoot = gameRoot;
        _http = http;
    }

    public IModrinthSource CreateModrinthSource() => new ModrinthBase(_http);
    public ICurseForgeSource CreateCurseForgeSource(string apiKey) => new CurseForgeBase(_http, apiKey);
    public IFTBSource CreateFTBSource() => new FTBBase(_http);

    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
    }
}
