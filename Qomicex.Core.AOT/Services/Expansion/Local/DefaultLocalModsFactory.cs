namespace Qomicex.Core.AOT.Services.Expansion.Local;

internal sealed class DefaultLocalResourcesFactory : ILocalResourcesFactory
{
    private readonly HttpClient _http;
    private readonly string _gameRoot;

    public DefaultLocalResourcesFactory(HttpClient http, string gameRoot)
    {
        _http = http;
        _gameRoot = gameRoot;
    }

    public Mods CreateMods(string version, bool versionSegmented, string apiKey)
        => new Mods(_http, _gameRoot, version, versionSegmented, apiKey);

    public Saves CreateSaves(string version, bool versionSegmented, string apiKey)
        => new Saves(_http, _gameRoot, version, versionSegmented, apiKey);

    public Resourcepack CreateResourcepack(string version, bool versionSegmented, string apiKey)
        => new Resourcepack(_http, _gameRoot, version, versionSegmented, apiKey);

    public Shaders CreateShaders(string version, bool versionSegmented, string apiKey)
        => new Shaders(_http, _gameRoot, version, versionSegmented, apiKey);

    public Screenshots CreateScreenshots(string version, bool versionSegmented, string apiKey)
        => new Screenshots(_http, _gameRoot, version, versionSegmented, apiKey);

    public DataPacks CreateDataPacks(string version, bool versionSegmented, string apiKey)
        => new DataPacks(_http, _gameRoot, version, versionSegmented, apiKey);
}
