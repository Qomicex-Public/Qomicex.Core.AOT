using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record ScreenshotsMeta(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("thumbnailUrl")] string? ThumbnailUrl,
    [property: JsonPropertyName("url")] string? Url
);
