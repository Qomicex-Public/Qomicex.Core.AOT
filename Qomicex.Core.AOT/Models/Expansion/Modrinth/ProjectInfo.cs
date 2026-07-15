using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public record ProjectInfo(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("project_type")] string? Type,
    [property: JsonPropertyName("team")] string? Team,
    [property: JsonPropertyName("organization")] string? Organization,
    [property: JsonPropertyName("title")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("body")] string? FullDescription,
    [property: JsonPropertyName("published")] DateTime PublishAt,
    [property: JsonPropertyName("updated")] DateTime UpdatedAt,
    [property: JsonPropertyName("approved")] DateTime? ApprovedAt,
    [property: JsonPropertyName("downloads")] int DownloadCount,
    [property: JsonPropertyName("followers")] int FollowCount,
    [property: JsonPropertyName("categories")] List<string>? Categories,
    [property: JsonPropertyName("additional_categories")] List<string>? AdditionalCategories,
    [property: JsonPropertyName("loaders")] List<string>? Loaders,
    [property: JsonPropertyName("game_versions")] List<string>? GameVersionIds,
    [property: JsonPropertyName("versions")] List<string>? Versions,
    [property: JsonPropertyName("icon_url")] string? IconUrl,
    [property: JsonPropertyName("issues_url")] string? IssuesUrl,
    [property: JsonPropertyName("source_url")] string? SourceUrl,
    [property: JsonPropertyName("wiki_url")] string? WikiUrl,
    [property: JsonPropertyName("discord_url")] string? DiscordUrl,
    [property: JsonPropertyName("client_side")] string? ClientSide,
    [property: JsonPropertyName("server_side")] string? ServerSide,
    [property: JsonPropertyName("gallery")] List<GalleryItem>? Gallery
);

public record GalleryItem(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("featured")] bool Featured,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("created")] DateTime Created,
    [property: JsonPropertyName("ordering")] int? Ordering
);
