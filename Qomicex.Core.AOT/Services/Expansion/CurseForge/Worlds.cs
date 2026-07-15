using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal sealed class Worlds : CurseForgeBase, ICurseForgeSource
{
    private const int GameId = 432;
    private const int ClassId = 17;

    public Worlds(HttpClient http, string apiKey) : base(http, apiKey) { }
}
