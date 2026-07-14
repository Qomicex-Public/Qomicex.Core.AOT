using Qomicex.Core.AOT.Core;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Services;

namespace Qomicex.Core.AOT.Builder;

public sealed class GameCoreBuilder
{
    private readonly CoreOptions _options = new();

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

    public DefaultGameCore Build()
    {
        var http = new HttpClient();
        var downloadSource = new DefaultDownloadSourceManager();
        var version = new VersionManagementService(_options.GameRoot, http, downloadSource);

        return new DefaultGameCore(version, null!, null!, http);
    }
}
