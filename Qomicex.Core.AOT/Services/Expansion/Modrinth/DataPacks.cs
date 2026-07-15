using Qomicex.Core.AOT.Models.Expansion.Modrinth;

namespace Qomicex.Core.AOT.Services.Expansion.Modrinth;

internal sealed class DataPacks(HttpClient http) : ModrinthBase(http)
{
    public new async Task<SearchResult> SearchAsync(
        string query, string? projectType = null, string? gameVersion = null,
        string[]? categories = null, string[]? loaders = null,
        string index = "relevance", int page = 0, int pageSize = 20)
    {
        return await base.SearchAsync(query, "datapack", gameVersion, categories, loaders, index, page, pageSize);
    }
}
