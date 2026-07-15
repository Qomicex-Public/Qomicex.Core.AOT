using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public record ProjectVersionInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version_number")] string? VersionNumber,
    [property: JsonPropertyName("game_versions")] List<string>? GameVersionIds,
    [property: JsonPropertyName("loaders")] List<string>? Loaders,
    [property: JsonPropertyName("changelog")] string? Changelog,
    [property: JsonPropertyName("date_published")] DateTime PublishedAt,
    [property: JsonPropertyName("downloads")] int DownloadCount,
    [property: JsonPropertyName("version_type")] string? VersionType,
    [property: JsonPropertyName("files")] List<VersionFileInfo>? Files,
    [property: JsonPropertyName("dependencies")] List<DependenciesInfo>? DependenciesInfos
);
