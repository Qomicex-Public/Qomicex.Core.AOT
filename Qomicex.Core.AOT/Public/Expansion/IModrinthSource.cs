using Qomicex.Core.AOT.Models.Expansion.Modrinth;

namespace Qomicex.Core.AOT.Public.Expansion;

public interface IModrinthSource
{
    Task<SearchResult> SearchAsync(
        string query,
        string? projectType = null,
        string? gameVersion = null,
        string[]? categories = null,
        string[]? loaders = null,
        string index = "relevance",
        int page = 0,
        int pageSize = 20
    );

    Task<ProjectInfo> GetProjectInfoAsync(string projectId);

    Task<List<ProjectVersionInfo>> GetProjectVersionInfoAsync(string projectId);

    Task<VersionInfo> GetVersionInfoAsync(string versionId);

    Task<List<ProjectVersionInfo>> GetProjectVersionsFromHashesAsync(List<string> hashes);

    Task<Dictionary<string, ProjectVersionInfo>> GetProjectVersionsFromHashesDictAsync(List<string> hashes);

    Task<List<ModrinthTag>> GetCategoriesAsync();

    Task<List<ModrinthTag>> GetLoadersAsync();

    Task<List<ModrinthTag>> GetProjectTypesAsync();
}
