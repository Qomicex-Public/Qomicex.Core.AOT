using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Expansion.Modrinth;
using Qomicex.Core.AOT.Public.Expansion;
using static Qomicex.Core.AOT.Models.Expansion.Modrinth.Index;

namespace Qomicex.Core.AOT.Services.Expansion.Modrinth;

internal class ModrinthBase : IModrinthSource
{
    private const string DefaultBaseUrl = "https://api.modrinth.com/";

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private static readonly ModrinthJsonContext JsonCtx = ModrinthJsonContext.Default;

    public ModrinthBase(HttpClient http, string? baseUrl = null)
    {
        _http = http;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/') + "/";

        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private async Task<string> GetDataAsync(string url)
    {
        if (!url.StartsWith("http"))
            url = _baseUrl + url;

        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostDataAsync(string url, object data)
    {
        if (!url.StartsWith("http"))
            url = _baseUrl + url;

        var jsonData = JsonSerializer.Serialize(data);
        var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<SearchResult> SearchAsync(
        string query,
        string? projectType = null,
        string? gameVersion = null,
        string[]? categories = null,
        string[]? loaders = null,
        string index = Relevance,
        int page = 0,
        int pageSize = 20)
    {
        ArgumentException.ThrowIfNullOrEmpty(query);
        ArgumentOutOfRangeException.ThrowIfNegative(pageSize, nameof(pageSize));
        if (pageSize > 100) throw new ArgumentOutOfRangeException(nameof(pageSize), "每页数量最大 100");

        var url = new StringBuilder($"{_baseUrl}v2/search?query={Uri.EscapeDataString(query)}");

        var facets = new List<string>();
        if (!string.IsNullOrEmpty(projectType))
            facets.Add($"[\"project_type:{Uri.EscapeDataString(projectType)}\"]");
        if (categories != null)
            foreach (var c in categories)
                facets.Add($"[\"categories:{Uri.EscapeDataString(c)}\"]");
        if (loaders != null)
            foreach (var l in loaders)
                facets.Add($"[\"categories:{Uri.EscapeDataString(l)}\"]");
        if (!string.IsNullOrEmpty(gameVersion))
            facets.Add($"[\"versions:{Uri.EscapeDataString(gameVersion)}\"]");
        if (facets.Count > 0)
            url.Append($"&facets={Uri.EscapeDataString($"[{string.Join(",", facets)}]")}");

        url.Append($"&limit={pageSize}&offset={page * pageSize}&index={Uri.EscapeDataString(index)}");

        var response = await GetDataAsync(url.ToString());
        return JsonSerializer.Deserialize(response, JsonCtx.SearchResult)
            ?? throw new InvalidOperationException("搜索结果反序列化失败");
    }

    public async Task<ProjectInfo> GetProjectInfoAsync(string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        var response = await GetDataAsync($"{_baseUrl}v2/project/{Uri.EscapeDataString(projectId)}");
        return JsonSerializer.Deserialize(response, JsonCtx.ProjectInfo)
            ?? throw new InvalidOperationException("项目信息反序列化失败");
    }

    public async Task<List<ProjectVersionInfo>> GetProjectVersionInfoAsync(string projectId)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectId);
        var response = await GetDataAsync($"{_baseUrl}v2/project/{Uri.EscapeDataString(projectId)}/version");
        var dict = JsonSerializer.Deserialize(response, JsonCtx.ModrinthVersionResponse);
        return dict?.Values.Select(v => new ProjectVersionInfo(
            v.Id, v.ProjectId, v.Name, v.VersionNumber,
            v.GameVersions, v.Loaders, null, v.DatePublished,
            0, null, null, null
        )).ToList() ?? [];
    }

    public async Task<VersionInfo> GetVersionInfoAsync(string versionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(versionId);
        var response = await GetDataAsync($"{_baseUrl}v2/version/{Uri.EscapeDataString(versionId)}");
        return JsonSerializer.Deserialize(response, JsonCtx.VersionInfo)
            ?? throw new InvalidOperationException("版本信息反序列化失败");
    }

    public async Task<List<ProjectVersionInfo>> GetProjectVersionsFromHashesAsync(List<string> hashes)
    {
        var dict = await GetProjectVersionsFromHashesDictAsync(hashes);
        return dict.Values.ToList();
    }

    public async Task<Dictionary<string, ProjectVersionInfo>> GetProjectVersionsFromHashesDictAsync(List<string> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);
        if (hashes.Count == 0) return [];

        var response = await PostDataAsync($"{_baseUrl}v2/version_files", new { hashes, algorithm = "sha1" });
        var dict = JsonSerializer.Deserialize(response, JsonCtx.DictionaryStringModrinthVersionInfo);
        if (dict == null) return [];

        return dict.ToDictionary(
            kv => kv.Key,
            kv => new ProjectVersionInfo(
                kv.Value.Id, kv.Value.ProjectId, kv.Value.Name, kv.Value.VersionNumber,
                kv.Value.GameVersions, kv.Value.Loaders, null, kv.Value.DatePublished,
                0, null, null, null
            ));
    }

    public async Task<List<ModrinthTag>> GetCategoriesAsync()
    {
        return await GetTagsAsync("category");
    }

    public async Task<List<ModrinthTag>> GetLoadersAsync()
    {
        return await GetTagsAsync("loader");
    }

    public async Task<List<ModrinthTag>> GetProjectTypesAsync()
    {
        var response = await GetDataAsync($"{_baseUrl}v2/tag/project_type");
        var stringTags = JsonSerializer.Deserialize(response, JsonCtx.ListString);
        return stringTags?.Select(t => new ModrinthTag(t, null, null)).ToList() ?? [];
    }

    private async Task<List<ModrinthTag>> GetTagsAsync(string tagType)
    {
        ArgumentException.ThrowIfNullOrEmpty(tagType);
        var response = await GetDataAsync($"{_baseUrl}v2/tag/{tagType}");
        return JsonSerializer.Deserialize(response, JsonCtx.ListModrinthTag) ?? [];
    }
}
