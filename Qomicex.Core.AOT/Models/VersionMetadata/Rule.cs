using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.VersionMetadata;

/// <summary>
/// 表示一个条件规则
/// </summary>
public record Rule(
    [property: JsonPropertyName("action")] string Action,
    [property: JsonPropertyName("os")] OsRequirement? Os,
    [property: JsonPropertyName("features")]
    Dictionary<string, bool>? Features
);

/// <summary>
/// 表示操作系统要求
/// </summary>
public record OsRequirement(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")]
    string? Version,
    [property: JsonPropertyName("arch")] string? Arch
);