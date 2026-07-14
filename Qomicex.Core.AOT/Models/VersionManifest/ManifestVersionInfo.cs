using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionManifest;

/// <summary>
/// 表示版本清单中的单个版本条目
/// </summary>
public record ManifestVersionInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("time")] DateTime Time,
    [property: JsonPropertyName("releaseTime")] DateTime ReleaseTime
);