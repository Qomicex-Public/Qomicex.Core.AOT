using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示该版本所需的 Java 版本信息
/// </summary>
public record JavaVersion(
    [property: JsonPropertyName("component")] string Component,
    [property: JsonPropertyName("majorVersion")] int MajorVersion
);