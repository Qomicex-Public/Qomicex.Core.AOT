using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示一个库文件（依赖项）
/// </summary>
public record Library(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("downloads")]
    LibraryDownloads Downloads,
    [property: JsonPropertyName("rules")] List<Rule>? Rules,
    [property: JsonPropertyName("natives")]
    Dictionary<string, string>? Natives,
    [property: JsonPropertyName("extract")]
    LibraryExtract? Extract
);

/// <summary>
/// 库文件的下载信息
/// </summary>
public record LibraryDownloads(
    [property: JsonPropertyName("artifact")]
    Artifact? Artifact,
    [property: JsonPropertyName("classifiers")]
    Dictionary<string, Artifact>? Classifiers
);

/// <summary>
/// 文件信息
/// </summary>
public record Artifact(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha1")] string Sha1,
    [property: JsonPropertyName("size")] long Size
);

/// <summary>
/// 库文件的提取规则（主要用于 natives）
/// </summary>
public record LibraryExtract(
    [property: JsonPropertyName("exclude")]
    List<string>? Exclude
);