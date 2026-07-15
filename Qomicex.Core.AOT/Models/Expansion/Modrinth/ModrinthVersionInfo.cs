using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public record ModrinthVersionInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("project_id")] string ProjectId,
    [property: JsonPropertyName("author_id")] string? AuthorId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version_number")] string? VersionNumber,
    [property: JsonPropertyName("game_versions")] List<string>? GameVersions,
    [property: JsonPropertyName("loaders")] List<string>? Loaders,
    [property: JsonPropertyName("date_published")] DateTime DatePublished,
    [property: JsonPropertyName("files")] List<ModrinthFile>? Files
);

public record ModrinthFile(
    [property: JsonPropertyName("hashes")] Dictionary<string, string>? Hashes,
    [property: JsonPropertyName("filename")] string FileName,
    [property: JsonPropertyName("url")] string Url
);

public class ModrinthVersionResponse : Dictionary<string, ModrinthVersionInfo>
{
    public ModrinthVersionResponse() : base() { }
    public ModrinthVersionResponse(IDictionary<string, ModrinthVersionInfo> dictionary) : base(dictionary) { }
}
