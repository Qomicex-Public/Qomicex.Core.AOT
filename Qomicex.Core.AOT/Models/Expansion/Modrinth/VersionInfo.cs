using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public record VersionInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("title")] string Name,
    [property: JsonPropertyName("version_number")] string? VersionNumber,
    [property: JsonPropertyName("game_versions")] List<string>? GameVersionIds,
    [property: JsonPropertyName("loaders")] List<string>? Loaders,
    [property: JsonPropertyName("changelog")] string? Changelog,
    [property: JsonPropertyName("published")] DateTime PublishedAt,
    [property: JsonPropertyName("updated")] DateTime UpdatedAt,
    [property: JsonPropertyName("approved")] DateTime? ApprovedAt,
    [property: JsonPropertyName("downloads")] int DownloadCount,
    [property: JsonPropertyName("icon_url")] string? IconUrl,
    [property: JsonPropertyName("files")] List<VersionFileInfo>? Files,
    [property: JsonPropertyName("dependencies")] List<DependenciesInfo>? DependenciesInfos
);

public record VersionFileInfo(
    [property: JsonPropertyName("filename")] string Filename,
    [property: JsonPropertyName("url")] string DownloadUrl,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("primary")] bool IsPrimary,
    [property: JsonPropertyName("file_type")] string? FileType,
    [property: JsonPropertyName("hashes")] FileHashes? Hashes
);

public record FileHashes(
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("sha512")] string? Sha512
);

public record DependenciesInfo(
    [property: JsonPropertyName("version_id")] string? VersionId,
    [property: JsonPropertyName("project_id")] string? ProjectId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("dependency_type")] string? DependencyType
);
