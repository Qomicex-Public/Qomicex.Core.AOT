using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public record SearchResult(
    [property: JsonPropertyName("hits")] List<SearchResultInfo> Results,
    [property: JsonPropertyName("total_hits")] int TotalResults
);

public record SearchResultInfo(
    [property: JsonPropertyName("project_id")] string Id,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("title")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("body")] string? FullDescription,
    [property: JsonPropertyName("project_type")] string? Type,
    [property: JsonPropertyName("client_side")] string? ClientSide,
    [property: JsonPropertyName("server_side")] string? ServerSide,
    [property: JsonPropertyName("downloads")] int DownloadCount,
    [property: JsonPropertyName("follows")] int FollowCount,
    [property: JsonPropertyName("icon_url")] string? IconUrl,
    [property: JsonPropertyName("date_created")] DateTime CreatedAt,
    [property: JsonPropertyName("date_modified")] DateTime UpdatedAt,
    [property: JsonPropertyName("license")] string? License,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("categories")] List<string>? Categories,
    [property: JsonPropertyName("versions")] List<string>? VersionIds,
    [property: JsonPropertyName("gallery")] List<string>? GalleryUrls
);
