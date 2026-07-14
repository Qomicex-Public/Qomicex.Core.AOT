using System.Text.Json;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Services;

public class VersionManifestService : IVersionManifestService
{
    private readonly HttpClient _httpClient;
    private readonly CombinedJsonContext _ctx = CombinedJsonContext.Default;

    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest.json";

    public VersionManifestService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<VersionManifestRoot> GetVersionManifestAsync()
    {
        var response = await _httpClient.GetStringAsync(ManifestUrl);
        return JsonSerializer.Deserialize(response, _ctx.VersionManifestRoot)
            ?? throw new JsonException("解析版本清单失败");
    }

    public async Task<CompleteVersionMetadata> GetVersionMetadataAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("元数据URL不能为空", nameof(url));

        var response = await _httpClient.GetStringAsync(url);
        return JsonSerializer.Deserialize(response, _ctx.CompleteVersionMetadata)
            ?? throw new JsonException($"解析版本元数据失败: {url}");
    }
}
