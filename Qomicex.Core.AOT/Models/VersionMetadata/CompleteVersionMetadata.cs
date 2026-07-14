using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示单个游戏版本的完整元数据
/// 对应：https://piston-meta.mojang.com/v1/packages/.../version.json
/// </summary>
public record CompleteVersionMetadata(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("mainClass")] string MainClass,
    [property: JsonPropertyName("inheritsFrom")] string? InheritsFrom,
    [property: JsonPropertyName("jar")] string? Jar,
    [property: JsonPropertyName("arguments")] VersionArguments? Arguments,
    [property: JsonPropertyName("libraries")] List<Library> Libraries,
    [property: JsonPropertyName("assetIndex")] AssetIndex AssetIndex,
    [property: JsonPropertyName("downloads")] VersionDownloads Downloads,
    [property: JsonPropertyName("javaVersion")] JavaVersion? JavaVersion,
    [property: JsonPropertyName("minimumLauncherVersion")] int? MinimumLauncherVersion,
    [property: JsonPropertyName("releaseTime")] DateTime ReleaseTime,
    [property: JsonPropertyName("time")] DateTime Time
);