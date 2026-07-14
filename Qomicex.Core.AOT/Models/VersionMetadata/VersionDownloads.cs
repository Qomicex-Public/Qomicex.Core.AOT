using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示版本核心Jar文件的下载信息
/// </summary>
public record VersionDownloads(
    [property: JsonPropertyName("client")] Artifact Client,
    [property: JsonPropertyName("server")] Artifact? Server
);