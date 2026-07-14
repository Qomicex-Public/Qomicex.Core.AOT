using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示资源文件索引信息
/// </summary>
public record AssetIndex(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sha1")] string Sha1,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("totalSize")]
    long TotalSize,
    [property: JsonPropertyName("url")] string Url
);