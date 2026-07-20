namespace Qomicex.Core.AOT.Services.Expansion.Local;

internal sealed class DefaultLocalModsFactory : ILocalModsFactory
{
    private readonly HttpClient _http;
    private readonly string _gameRoot;

    public DefaultLocalModsFactory(HttpClient http, string gameRoot)
    {
        _http = http;
        _gameRoot = gameRoot;
    }

    public Mods Create(string version, bool versionSegmented, string apiKey)
        => new Mods(_http, _gameRoot, version, versionSegmented, apiKey);
}
