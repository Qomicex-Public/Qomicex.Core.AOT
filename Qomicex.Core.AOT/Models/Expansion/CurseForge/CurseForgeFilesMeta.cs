using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeFilesMeta(
    [property: JsonPropertyName("fileId")] int FileId,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("releaseType")] int ReleaseType,
    [property: JsonPropertyName("gameVersion")] string? GameVersion,
    [property: JsonPropertyName("modLoader")] int ModLoader
);
