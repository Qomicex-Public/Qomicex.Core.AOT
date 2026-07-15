using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeFileInfo(
    [property: JsonPropertyName("id")] string FileId,
    [property: JsonPropertyName("modId")] string ModId,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("releaseType")] int ReleaseType,
    [property: JsonPropertyName("fileStatus")] int FileStatus,
    [property: JsonPropertyName("dependencies")] List<CurseForgeDependenciesMeta>? Dependencies
);
