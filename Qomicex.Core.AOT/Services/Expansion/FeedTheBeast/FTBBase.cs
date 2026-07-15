using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;
using Qomicex.Core.AOT.Public.Expansion;

namespace Qomicex.Core.AOT.Services.Expansion.FeedTheBeast;

internal class FTBBase : IFTBSource
{
    private const string DefaultBaseUrl = "https://api.feed-the-beast.com/v1/modpacks/public";

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _cacheDir;
    private static readonly FTBJsonContext JsonCtx = FTBJsonContext.Default;

    private List<ModpackInfo> _cache = [];
    private long _cacheSavedAt;
    private static readonly SemaphoreSlim CacheLock = new(1, 1);
    private static readonly string CacheFileName = "ftb_cache.json";

    public FTBBase(HttpClient http, string? baseUrl = null, string? cacheDir = null)
    {
        _http = http;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Qomicex");

        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string CacheFile => Path.Combine(_cacheDir, CacheFileName);

    private async Task<string> GetDataAsync(string url)
    {
        if (!url.StartsWith("http"))
            url = _baseUrl + url;
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<ModpackInfo>> FetchAllPacksAsync()
    {
        await CacheLock.WaitAsync();
        try
        {
            if (_cache.Count == 0 && File.Exists(CacheFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(CacheFile);
                    var cached = JsonSerializer.Deserialize(json, JsonCtx.CacheData);
                    if (cached?.Modpacks.Count > 0)
                    {
                        _cache = cached.Modpacks;
                        _cacheSavedAt = cached.SavedAt;
                        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - _cacheSavedAt < 3600)
                            return _cache;
                    }
                }
                catch { }
            }

            var idsJson = await GetDataAsync("/modpack/all");
            var idsDoc = JsonNode.Parse(idsJson)!.AsObject();
            var ids = idsDoc["packs"] is JsonArray arr 
                ? arr.Select(n => (int)(n ?? 0)).ToList() 
                : [];

            var semaphore = new SemaphoreSlim(8);
            var tasks = ids.Select(async id =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var packJson = await GetDataAsync($"/modpack/{id}");
                    return JsonSerializer.Deserialize(packJson, JsonCtx.ModpackInfo);
                }
                catch { return null; }
                finally { semaphore.Release(); }
            });

            var results = await Task.WhenAll(tasks);
            _cache = results.Where(p => p != null).ToList()!;
            _cacheSavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                if (!string.IsNullOrEmpty(_cacheDir))
                    Directory.CreateDirectory(_cacheDir);
                var cacheData = new CacheData(_cacheSavedAt, _cache);
                await File.WriteAllTextAsync(CacheFile, JsonSerializer.Serialize(cacheData, JsonCtx.CacheData));
            }
            catch { }

            return _cache;
        }
        finally { CacheLock.Release(); }
    }

    public async Task<List<ModpackInfo>> SearchAsync(
        string? query = null,
        List<string>? tags = null,
        string? mcVersion = null,
        string? loader = null,
        string sort = "featured",
        int limit = 20)
    {
        var all = await FetchAllPacksAsync();

        IEnumerable<ModpackInfo> result = all;

        if (!string.IsNullOrEmpty(query))
        {
            var q = query.ToLower();
            result = result.Where(p =>
                (p.Name?.ToLower().Contains(q) ?? false) ||
                (p.Synopsis?.ToLower().Contains(q) ?? false));
        }

        if (tags?.Count > 0)
        {
            result = result.Where(p =>
                p.Tags?.Any(t => tags.Any(f => t.Name?.Equals(f, StringComparison.OrdinalIgnoreCase) ?? false)) ?? false);
        }

        if (!string.IsNullOrEmpty(mcVersion))
        {
            result = result.Where(p =>
            {
                var latest = IFTBSource.GetLatestVersion(p);
                return latest?.Targets?.Any(t => t.Type == "game" && t.Version == mcVersion) ?? false;
            });
        }

        if (!string.IsNullOrEmpty(loader))
        {
            result = result.Where(p =>
            {
                var latest = IFTBSource.GetLatestVersion(p);
                return latest?.Targets?.Any(t =>
                    t.Type == "modloader" && t.Name?.Equals(loader, StringComparison.OrdinalIgnoreCase) == true) ?? false;
            });
        }

        result = sort switch
        {
            "trending" => result.OrderByDescending(p => p.Plays14d),
            "name" => result.OrderBy(p => p.Name),
            "plays" => result.OrderByDescending(p => p.Plays),
            "downloads" => result.OrderByDescending(p => p.Installs),
            "released" => result.OrderByDescending(p => p.Released),
            "updated" => result.OrderByDescending(p => p.Updated),
            _ => result.OrderByDescending(p => p.Featured == true).ThenByDescending(p => p.Plays)
        };

        return result.Take(limit).ToList();
    }

    public async Task<ModpackInfo?> GetPackDetailAsync(int id)
    {
        try
        {
            var json = await GetDataAsync($"/modpack/{id}");
            return JsonSerializer.Deserialize(json, JsonCtx.ModpackInfo);
        }
        catch { return null; }
    }

    public async Task<VersionDetail?> GetVersionDetailAsync(int packId, int versionId)
    {
        try
        {
            var json = await GetDataAsync($"/modpack/{packId}/{versionId}");
            return JsonSerializer.Deserialize(json, JsonCtx.VersionDetail);
        }
        catch { return null; }
    }

    public async Task<ChangelogResult?> GetChangelogAsync(int packId, int versionId)
    {
        try
        {
            var json = await GetDataAsync($"/modpack/{packId}/{versionId}/changelog");
            return JsonSerializer.Deserialize(json, JsonCtx.ChangelogResult);
        }
        catch { return null; }
    }

    public static string FormatNumber(long n)
    {
        if (n >= 1_000_000) return $"{n / 1_000_000.0:F1}M";
        if (n >= 1_000) return $"{n / 1_000.0:F1}K";
        return n.ToString();
    }

    public static string FormatSize(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F2} GB";
        if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F2} MB";
        if (bytes >= 1_024) return $"{bytes / 1_024.0:F2} KB";
        return $"{bytes} B";
    }
}
