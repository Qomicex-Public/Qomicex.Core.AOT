using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;

public record ModpackInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("synopsis")] string? Synopsis,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("featured")] bool? Featured,
    [property: JsonPropertyName("plays")] long Plays,
    [property: JsonPropertyName("installs")] long Installs,
    [property: JsonPropertyName("plays_14d")] long Plays14d,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("released")] long Released,
    [property: JsonPropertyName("private")] bool Private,
    [property: JsonPropertyName("tags")] List<TagInfo>? Tags,
    [property: JsonPropertyName("versions")] List<VersionInfo>? Versions,
    [property: JsonPropertyName("authors")] List<AuthorInfo>? Authors,
    [property: JsonPropertyName("links")] List<LinkInfo>? Links,
    [property: JsonPropertyName("art")] List<ArtInfo>? Art,
    [property: JsonPropertyName("meta")] MetaInfo? Meta,
    [property: JsonPropertyName("rating")] RatingInfo? Rating
);

public record TagInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name
);

public record VersionInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("released")] long Released,
    [property: JsonPropertyName("private")] bool Private,
    [property: JsonPropertyName("specs")] SpecsInfo? Specs,
    [property: JsonPropertyName("targets")] List<TargetInfo>? Targets
);

public record SpecsInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("minimum")] int Minimum,
    [property: JsonPropertyName("recommended")] int Recommended
);

public record TargetInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("updated")] long Updated
);

public record AuthorInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("website")] string? Website,
    [property: JsonPropertyName("updated")] long Updated
);

public record LinkInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("link")] string Url,
    [property: JsonPropertyName("type")] string? Type
);

public record ArtInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("compressed")] bool Compressed,
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("updated")] long Updated
);

public record MetaInfo(
    [property: JsonPropertyName("supportsWorlds")] bool SupportsWorlds,
    [property: JsonPropertyName("curseforgeProjectId")] int? CurseforgeProjectId,
    [property: JsonPropertyName("isLegacy")] bool IsLegacy
);

public record RatingInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("configured")] bool Configured,
    [property: JsonPropertyName("verified")] bool Verified,
    [property: JsonPropertyName("age")] int Age,
    [property: JsonPropertyName("gambling")] bool Gambling,
    [property: JsonPropertyName("frightening")] bool Frightening,
    [property: JsonPropertyName("alcoholdrugs")] bool AlcoholDrugs,
    [property: JsonPropertyName("nuditysexual")] bool NuditySexual,
    [property: JsonPropertyName("sterotypeshate")] bool StereotypesHate,
    [property: JsonPropertyName("language")] bool Language,
    [property: JsonPropertyName("violence")] bool Violence
);
