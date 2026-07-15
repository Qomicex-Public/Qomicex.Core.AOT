using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.FeedTheBeast;

internal sealed class Modpacks : FTBBase, IFTBSource
{
    public Modpacks(HttpClient http, string? baseUrl = null, string? cacheDir = null)
        : base(http, baseUrl, cacheDir) { }
}
