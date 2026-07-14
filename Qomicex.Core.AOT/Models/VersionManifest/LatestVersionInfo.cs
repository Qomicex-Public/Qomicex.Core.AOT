using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionManifest;

/// <summary>
/// 表示最新版本信息
/// </summary>
public record LatestVersionInfo(
    [property: JsonPropertyName("release")]
    string Release,
    [property: JsonPropertyName("snapshot")]
    string Snapshot
);