using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeLogo(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("thumbnailUrl")] string? ThumbnailUrl
);
