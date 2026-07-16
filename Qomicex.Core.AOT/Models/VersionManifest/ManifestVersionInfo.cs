using Qomicex.Core.AOT.Utils;
using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionManifest;

/// <summary>
/// 表示版本清单中的单个版本条目
/// </summary>
public record ManifestVersionInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("time")][property: JsonConverter(typeof(MinecraftDateTimeConverter))] DateTimeOffset Time,
    [property: JsonPropertyName("releaseTime")][property: JsonConverter(typeof(MinecraftDateTimeConverter))] DateTimeOffset ReleaseTime
);