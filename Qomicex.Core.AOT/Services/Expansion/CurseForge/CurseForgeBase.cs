using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Expansion.CurseForge;
using Qomicex.Core.AOT.Public.Expansion;
using Qomicex.Core.AOT.Utils;

namespace Qomicex.Core.AOT.Services.Expansion.CurseForge;

internal class CurseForgeBase : ICurseForgeSource
{
    private const string DefaultBaseUrl = "https://api.curseforge.com";
    private const int MaxBatchFileIds = 100;

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private static readonly CurseForgeJsonContext JsonCtx = CurseForgeJsonContext.Default;

    public CurseForgeBase(HttpClient http, string apiKey, string? baseUrl = null)
    {
        _http = http;
        _apiKey = apiKey;
        _baseUrl = (baseUrl ?? DefaultBaseUrl).TrimEnd('/');
    }

    private async Task<string> GetData(string url, string key)
    {
        var fullUrl = url.StartsWith("http") ? url : _baseUrl + url;
        using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
        request.Headers.Add("x-api-key", key);
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> PostData(string url, string key, string jsonData)
    {
        var fullUrl = url.StartsWith("http") ? url : _baseUrl + url;
        using var request = new HttpRequestMessage(HttpMethod.Post, fullUrl);
        request.Headers.Add("x-api-key", key);
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
        request.Headers.UserAgent.ParseAdd("QomicexCore/1.0");
        request.Content = new StringContent(jsonData, Encoding.UTF8, "application/json");

        Trace.WriteLine($"[CurseForge] POST {fullUrl}");
        var sw = Stopwatch.StartNew();
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _http.SendAsync(request, timeoutCts.Token);
            sw.Stop();
            Trace.WriteLine($"[CurseForge] POST {fullUrl} => {(int)response.StatusCode} 耗时 {sw.ElapsedMilliseconds}ms");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            sw.Stop();
            Trace.WriteLine($"[CurseForge] POST {fullUrl} => 失败 耗时 {sw.ElapsedMilliseconds}ms: {ex.Message}");
            throw;
        }
    }

    public async Task<CurseForgeSearchResponse> SearchAsync(
        string searchFilter,
        string[]? gameVersions,
        int?[]? categories,
        string[]? modLoaderTypes,
        int? sortField = 1,
        int? page = 1,
        int? pageSize = 25,
        int? classId = null)
    {
        var p = page ?? 1;
        var ps = pageSize ?? 25;
        var index = ((p - 1) * ps).ToString();
        if (int.Parse(index) + ps > 10000)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize cannot exceed 10,000 items.");

        var modLoaders = string.Join(",", modLoaderTypes ?? []);
        var cats = categories is { Length: > 0 } ? $"&categoryIds=[{string.Join(",", categories)}]" : "";
        var versions = string.Join(",", (gameVersions ?? []).Select(v => $"\"{v}\""));
        var cls = classId.HasValue ? $"&classId={classId.Value}" : "";
        var url = $"{_baseUrl}/v1/mods/search?gameId=432{cls}&searchFilter={searchFilter}&sortOrder=desc&gameVersions=[{versions}]&pageSize={ps}&index={index}{cats}&modLoaderTypes=[{modLoaders}]&sortField={sortField}";

        var data = await GetData(url, _apiKey);
        var root = JsonNode.Parse(data);
        var result = root?["data"] as JsonArray;
        var totalCount = root?["pagination"]?["totalCount"]?.GetValue<int>() ?? 0;
        if (result == null) return new CurseForgeSearchResponse([], 0);

        var results = new List<CurseForgeSearchResult>();
        foreach (var mod in result)
        {
            var modData = mod!.AsObject();
            var latestFilesIndexes = modData["latestFilesIndexes"] as JsonArray;
            var gameVersionsList = latestFilesIndexes?
                .Select(n => n?["gameVersion"]?.ToString())
                .OfType<string>()
                .Distinct()
                .OrderBy(v => v)
                .ToList() ?? [];

            results.Add(new CurseForgeSearchResult
            {
                Id = modData["id"]?.ToString() ?? "",
                Name = modData["name"]?.ToString() ?? "",
                Slug = modData["slug"]?.ToString() ?? "",
                Summary = modData["summary"]?.ToString() ?? "",
                Status = modData["status"]?.ToString() ?? "",
                GameVersion = string.Join(", ", gameVersionsList),
                DownloadCount = modData["downloadCount"]?.ToString() ?? "",
                IconUrl = modData["logo"]?["url"]?.ToString() ?? "",
                IsFeatured = modData["isFeatured"]?.GetValue<bool>() ?? false,
                Authors = ParseJsonArray<AuthorMeta>(modData["authors"] as JsonArray, n => new AuthorMeta(
                    n?["id"]?.GetValue<int>() ?? 0,
                    n?["name"]?.ToString() ?? "",
                    n?["url"]?.ToString()
                )),
                Categories = ParseJsonArray<CategoryMeta>(modData["categories"] as JsonArray, n => new CategoryMeta(
                    (int)(n?["id"] ?? 0),
                    n?["name"]?.ToString() ?? "",
                    n?["slug"]?.ToString(),
                    n?["url"]?.ToString()
                )),
                Screenshots = ParseJsonArray<ScreenshotsMeta>(modData["screenshots"] as JsonArray, n => new ScreenshotsMeta(
                    (int)(n?["id"] ?? 0),
                    (int)(n?["modId"] ?? 0),
                    n?["title"]?.ToString(),
                    n?["description"]?.ToString(),
                    n?["thumbnailUrl"]?.ToString(),
                    n?["url"]?.ToString()
                ))
            });
        }
        return new CurseForgeSearchResponse(results, totalCount);
    }

    public async Task<CurseForgeInfo> GetModInfoAsync(string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var data = await GetData($"{_baseUrl}/v1/mods/{id}", _apiKey);
        var result = JsonNode.Parse(data)?["data"];
        if (result == null) throw new InvalidOperationException("CurseForge 响应为空");

        var info = result.ToObject(JsonCtx.CurseForgeInfo);
        return info!;
    }

    public async Task<CurseForgeFileInfo> GetFileInfoAsync(string modId, string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(modId);
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        var data = await GetData($"{_baseUrl}/v1/mods/{modId}/files/{fileId}", _apiKey);
        var result = JsonNode.Parse(data)?["data"];
        if (result == null) throw new InvalidOperationException("CurseForge 文件信息响应为空");

        return result.ToObject(JsonCtx.CurseForgeFileInfo)!;
    }

    public async Task<string> GetDownloadUrlAsync(string id, string fileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(fileId);
        var data = await GetData($"{_baseUrl}/v1/mods/{id}/files/{fileId}/download-url", _apiKey);
        return JsonNode.Parse(data)?["data"]?.ToString()
            ?? throw new InvalidOperationException("提取下载链接失败");
    }

    public async Task<Dictionary<long, CurseForgeBatchFileInfo>> GetFilesBatchAsync(IReadOnlyList<long> fileIds)
    {
        ArgumentNullException.ThrowIfNull(fileIds);
        if (fileIds.Count == 0) return new Dictionary<long, CurseForgeBatchFileInfo>();

        var result = new Dictionary<long, CurseForgeBatchFileInfo>();

        for (var i = 0; i < fileIds.Count; i += MaxBatchFileIds)
        {
            var batch = fileIds.Skip(i).Take(MaxBatchFileIds).ToList();
            var batchNum = i / MaxBatchFileIds + 1;

            // 过滤无效 fileId：CF API 要求正整数 int32
            var validBatch = batch.Where(id => id > 0 && id <= int.MaxValue).ToList();
            var skipped = batch.Count - validBatch.Count;
            if (skipped > 0)
                Console.WriteLine($"[CurseForge] 批次 {batchNum} 过滤掉 {skipped} 个无效 fileId（≤0 或超出 int32 范围）");
            if (validBatch.Count == 0)
            {
                Console.WriteLine($"[CurseForge] 批次 {batchNum} 无有效 fileId，跳过");
                continue;
            }

            Console.WriteLine($"[CurseForge] 批次 {batchNum} 有效 fileId 前5个: [{string.Join(",", validBatch.Take(5))}] 总数={validBatch.Count}");

            var jsonData = $"{{\"fileIds\":[{string.Join(",", validBatch)}]}}";
            try
            {
                var responseText = await PostData("/v1/mods/files", _apiKey, jsonData);
                var array = JsonNode.Parse(responseText)?["data"] as JsonArray;
                if (array == null) continue;

                foreach (var node in array)
                {
                    var obj = node!.AsObject();
                    var id = obj["id"]?.GetValue<long>() ?? 0;
                    if (id == 0) continue;

                    result[id] = new CurseForgeBatchFileInfo
                    {
                        Id = id,
                        ModId = obj["modId"]?.GetValue<long>() ?? 0,
                        FileName = obj["fileName"]?.ToString(),
                        DownloadUrl = obj["downloadUrl"]?.ToString(),
                        FileLength = obj["fileLength"]?.GetValue<long>(),
                        Sha1 = FindSha1FromHashes(obj["hashes"] as JsonArray)
                    };
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                Console.WriteLine($"[CurseForge] 批次 {batchNum} 返回 400 Bad Request，跳过此批（{validBatch.Count} 个 fileId）");
                Console.WriteLine($"[CurseForge] 失败批次 fileIds: {string.Join(",", validBatch)}");
            }
        }

        return result;
    }

    private static string? FindSha1FromHashes(JsonArray? hashes)
    {
        if (hashes == null) return null;
        foreach (var hash in hashes)
        {
            if (hash?["algo"]?.GetValue<int>() == 1)
                return hash["value"]?.ToString();
        }
        return null;
    }

    public async Task<List<FingerprintsFilesMeta>> GetInfoFromHashesAsync(List<long> hashes)
    {
        var dict = await GetInfoFromHashesDictAsync(hashes);
        return dict.Values.ToList();
    }

    public async Task<Dictionary<long, FingerprintsFilesMeta>> GetInfoFromHashesDictAsync(List<long> hashes)
    {
        ArgumentNullException.ThrowIfNull(hashes);
        if (hashes.Count == 0) return [];

        var jsonData = JsonSerializer.Serialize(new FingerprintsRequest(hashes), JsonCtx.FingerprintsRequest);
        var data = await PostData("/v1/fingerprints", _apiKey, jsonData);
        var exactMatches = JsonNode.Parse(data)?["data"]?["exactMatches"] as JsonArray;
        if (exactMatches == null) return [];

        var result = new Dictionary<long, FingerprintsFilesMeta>();
        foreach (var match in exactMatches)
        {
            var fileData = match?["file"];
            if (fileData == null) continue;

            var meta = fileData.ToObject(JsonCtx.FingerprintsFilesMeta);
            if (meta == null) continue;

            var fingerprint = fileData["fileFingerprint"]?.GetValue<long>()
                ?? fileData["id"]?.GetValue<long>()
                ?? 0;
            if (fingerprint != 0)
                result[fingerprint] = meta;
        }
        return result;
    }

    private static List<T> ParseJsonArray<T>(JsonArray? arr, Func<JsonNode?, T> factory)
    {
        if (arr == null) return [];
        return arr.Select(factory).ToList();
    }
}
