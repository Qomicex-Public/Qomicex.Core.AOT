using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record FingerprintsFilesMeta(
    [property: JsonPropertyName("modId")] int ModId,
    [property: JsonPropertyName("id")] int FileId,
    [property: JsonPropertyName("dependencies")] List<CurseForgeDependenciesMeta>? Dependencies
);
