using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Public.Expansion;
using Qomicex.Core.AOT.Public.Services;
using Qomicex.Core.AOT.Services.Expansion.CurseForge;
using Qomicex.Core.AOT.Services.Expansion.FeedTheBeast;
using Qomicex.Core.AOT.Services.Expansion.Local;
using Qomicex.Core.AOT.Services.Expansion.Modrinth;
using Qomicex.Core.AOT.Services.Installers;

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
    public IJavaProvider JavaProvider { get; }
    public IInstallerProvider InstallerProvider { get; }
    public IVersionLocator Locator { get; }
    public IOptionsProvider? Options { get; }
    public IServerManager? ServerManager { get; }
    public IDownloadSourceManager DownloadManager { get; }
    public IInstallerFactory Installer { get; }
    public ILocalResourcesFactory LocalResourceProvider { get; }

    internal DefaultGameCore(
        IVersionManagementService version,
        IAuthProvider auth,
        ILaunchExecutor launch,
        IJavaProvider java,
        IInstallerProvider installerProvider,
        IVersionLocator locator,
        HttpClient http,
        string gameRoot,
        IOptionsProvider? optionsProvider = null,
        IServerManager? serverManager = null,
        IDownloadSourceManager? downloadManager = null,
        IInstallerFactory? installerFactory = null,
        ILocalResourcesFactory? localResourceProvider = null)
    {
        Version = version;
        Auth = auth;
        Launch = launch;
        JavaProvider = java;
        Locator = locator;
        HttpClient = http;
        GameRoot = gameRoot;
        _http = http;
        InstallerProvider = installerProvider;
        Options = optionsProvider;
        ServerManager = serverManager;
        DownloadManager = downloadManager!;
        Installer = installerFactory!;
        LocalResourceProvider = localResourceProvider!;
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
