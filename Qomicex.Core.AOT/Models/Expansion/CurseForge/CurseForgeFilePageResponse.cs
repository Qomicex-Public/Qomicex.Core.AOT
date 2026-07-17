using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeFilePageResponse(
    List<CurseForgeFilePageItem> Files,
    int TotalCount
);

public record CurseForgeFilePageItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("fileName")] string? FileName,
    [property: JsonPropertyName("downloadUrl")] string? DownloadUrl,
    [property: JsonPropertyName("fileDate")] DateTime FileDate,
    [property: JsonPropertyName("fileLength")] long FileLength,
    [property: JsonPropertyName("gameVersions")] List<string>? GameVersions,
    [property: JsonPropertyName("modLoader")] int ModLoader,
    [property: JsonPropertyName("sortableGameVersions")] List<CurseForgeSortableGameVersion>? SortableGameVersions,
    [property: JsonPropertyName("dependencies")] List<CurseForgeDependenciesMeta>? Dependencies
);

public record CurseForgeSortableGameVersion(
    [property: JsonPropertyName("gameVersion")] string GameVersion,
    [property: JsonPropertyName("gameVersionPadded")] string? GameVersionPadded
);
