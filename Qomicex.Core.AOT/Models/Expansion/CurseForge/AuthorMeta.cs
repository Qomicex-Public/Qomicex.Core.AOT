using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record AuthorMeta(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("url")] string? Url
);
