using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal sealed class ResourcePacks : CurseForgeBase, ICurseForgeSource
{
    private const int GameId = 432;
    private const int ClassId = 12;

    public ResourcePacks(HttpClient http, string apiKey) : base(http, apiKey) { }
}
