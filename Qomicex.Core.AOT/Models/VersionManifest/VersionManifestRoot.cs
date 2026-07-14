using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionManifest;

/// <summary>
/// 表示从 Mojang API 获取的版本清单根对象
/// 对应：https://launchermeta.mojang.com/mc/game/version_manifest.json
/// </summary>
public record VersionManifestRoot(
    [property: JsonPropertyName("latest")] LatestVersionInfo Latest,
    [property: JsonPropertyName("versions")] List<ManifestVersionInfo> Versions
);

