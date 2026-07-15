using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal sealed class Shaders : CurseForgeBase, ICurseForgeSource
{
    private const int GameId = 432;
    private const int ClassId = 6552;

    public Shaders(HttpClient http, string apiKey) : base(http, apiKey) { }
}
