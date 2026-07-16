using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Public.Services;
using Qomicex.Core.AOT.Services;
using Qomicex.Core.AOT.Services.Expansion;
using Qomicex.Core.AOT.Services.Installers;

namespace Qomicex.Core.AOT.Builder;

public sealed class GameCoreBuilder
{
    private readonly CoreOptions _options = new();
    private IVersionManagementService? _version;
    private IAuthProvider? _auth;
    private ILaunchExecutor? _launch;
    private HttpClient? _http;
    private IDownloadSourceManager? _source;
    private IJavaProvider? _javaProvider;
    private IInstallerProvider? _installerProvider;

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

    public GameCoreBuilder UseOfflineAuth(string username)
    {
        _options.AuthMode = AuthMode.Offline;
        _options.AuthOptions = _options.AuthOptions with { Name = username, Mode = AuthMode.Offline };
        return this;
    }

    public GameCoreBuilder UseMicrosoftAuth(string clientId)
    {
        _options.AuthMode = AuthMode.Microsoft;
        _options.MicrosoftClientId = clientId;
        _options.AuthOptions = _options.AuthOptions with { Mode = AuthMode.Microsoft };
        return this;
    }

    public GameCoreBuilder UseYggdrasilAuth(string serverUrl, string? email = null)
    {
        _options.AuthMode = AuthMode.Yggdrasil;
        _options.YggdrasilServerUrl = serverUrl;
        _options.AuthOptions = _options.AuthOptions with
        {
            Mode = AuthMode.Yggdrasil,
            ServerUrl = serverUrl
        };
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

    public GameCoreBuilder WithJavaProvider(IJavaProvider javaProvider)
    {
        _javaProvider = javaProvider;
        return this;
    }

    public GameCoreBuilder WithInstallerProvider(IInstallerProvider installerProvider)
    {
        _installerProvider = installerProvider;
        return this;
    }

    public DefaultGameCore Build()
    {
        var http = _http ?? new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
        InstallerBase.DefaultUserAgent ??= _options.UserAgent;
        var downloadSource = _source ?? new DefaultDownloadSourceManager(_options.DownloadMirror);
        var version = _version ?? new VersionManagementService(_options.GameRoot, http, downloadSource);
        var auth = _auth ?? CreateAuthProvider(http);
        var launch = _launch ?? new LaunchExecutor(_options.LauncherName, _options.GameRoot);
        var javaProvider = _javaProvider ?? new JavaProvider(http);
        var installerProvider = _installerProvider ?? new InstallerProvider(http,_options.DownloadMirror);

        return new DefaultGameCore(version, auth, launch,javaProvider,installerProvider, http, _options.GameRoot);
    }

    private IAuthProvider CreateAuthProvider(HttpClient http)
    {
        return _options.AuthMode switch
        {
            AuthMode.Microsoft => new MicrosoftAuthProvider(http, _options.MicrosoftClientId ?? ""),
            AuthMode.Yggdrasil => new YggdrasilAuthProvider(http, _options.YggdrasilServerUrl ?? "https://authserver.mojang.com"),
            _ => new DefaultAuthProvider()
        };
    }
}
