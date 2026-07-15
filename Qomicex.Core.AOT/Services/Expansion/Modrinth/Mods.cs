using Qomicex.Core.AOT.Models.Expansion.Modrinth;
using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.Modrinth;

internal sealed class Mods : ModrinthBase, IModrinthSource
{
    public Mods(HttpClient http) : base(http) { }

    public new async Task<SearchResult> SearchAsync(
        string query, string? projectType = null, string? gameVersion = null,
        string[]? categories = null, string[]? loaders = null,
        string index = "relevance", int page = 0, int pageSize = 20)
    {
        return await base.SearchAsync(query, "mod", gameVersion, categories, loaders, index, page, pageSize);
    }
}
