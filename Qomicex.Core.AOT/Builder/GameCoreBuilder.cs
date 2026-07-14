using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Services;

namespace Qomicex.Core.AOT.Builder;

public sealed class GameCoreBuilder
{
    private readonly CoreOptions _options = new();
    private IVersionManagementService? _version;
    private IAuthProvider? _auth;
    private ILaunchExecutor? _launch;
    private HttpClient? _http;
    private IDownloadSourceManager? _source;

    public GameCoreBuilder Configure(Action<CoreOptions> configure)
    {
        configure(_options);
        return this;
    }

    public GameCoreBuilder UseGameRoot(string path)
    {
        _options.GameRoot = path;
        return this;
    }

    public GameCoreBuilder UseMicrosoftAuth(string clientId)
    {
        _options.MicrosoftClientId = clientId;
        return this;
    }

    public GameCoreBuilder UseDownloadMirror(DownloadMirror mirror)
    {
        _options.DownloadMirror = mirror;
        return this;
    }

    public GameCoreBuilder WithVersionService(IVersionManagementService version)
    {
        _version = version;
        return this;
    }

    public GameCoreBuilder WithAuthProvider(IAuthProvider auth)
    {
        _auth = auth;
        return this;
    }

    public GameCoreBuilder WithLaunchExecutor(ILaunchExecutor launch)
    {
        _launch = launch;
        return this;
    }

    public GameCoreBuilder WithHttpClient(HttpClient http)
    {
        _http = http;
        return this;
    }

    public GameCoreBuilder WithDownloadSourceManager(IDownloadSourceManager source)
    {
        _source = source;
        return this;
    }

    public DefaultGameCore Build()
    {
        var http = _http ?? new HttpClient();
        var downloadSource = _source ?? new DefaultDownloadSourceManager(_options.DownloadMirror);
        var version = _version ?? new VersionManagementService(_options.GameRoot, http, downloadSource);
        var auth = _auth ?? new DefaultAuthProvider();
        var launch = _launch ?? new DefaultLaunchExecutor();

        return new DefaultGameCore(version, auth, launch, http);
    }
}
