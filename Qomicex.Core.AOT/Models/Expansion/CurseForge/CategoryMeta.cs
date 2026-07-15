using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CategoryMeta(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("url")] string? Url
);
