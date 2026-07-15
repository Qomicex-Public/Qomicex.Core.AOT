using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal sealed class Mods : CurseForgeBase, ICurseForgeSource
{
    private const int GameId = 432;
    private const int ClassId = 6;

    public Mods(HttpClient http, string apiKey) : base(http, apiKey) { }
}
