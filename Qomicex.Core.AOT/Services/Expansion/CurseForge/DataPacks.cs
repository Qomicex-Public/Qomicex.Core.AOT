using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal sealed class DataPacks : CurseForgeBase, ICurseForgeSource
{
    private const int GameId = 432;
    private const int ClassId = 6945;

    public DataPacks(HttpClient http, string apiKey) : base(http, apiKey) { }
}
