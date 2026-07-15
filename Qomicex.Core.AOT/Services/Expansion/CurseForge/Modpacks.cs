using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal sealed class Modpacks : CurseForgeBase, ICurseForgeSource
{
    private const int GameId = 432;
    private const int ClassId = 4471;

    public Modpacks(HttpClient http, string apiKey) : base(http, apiKey) { }
}
