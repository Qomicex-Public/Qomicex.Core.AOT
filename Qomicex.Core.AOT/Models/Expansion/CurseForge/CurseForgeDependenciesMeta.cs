using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeDependenciesMeta(
    [property: JsonPropertyName("modId")] int Id,
    [property: JsonPropertyName("relationType")] int Type
);
