using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.Modrinth;

public record ModrinthTag(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("icon")] string? Icon,
    [property: JsonPropertyName("description")] string? Description
);
