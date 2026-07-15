using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;

public record VersionDetail(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("parent")] int Parent,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("plays")] long Plays,
    [property: JsonPropertyName("installs")] long Installs,
    [property: JsonPropertyName("updated")] long Updated,
    [property: JsonPropertyName("changelog")] string? ChangelogUrl,
    [property: JsonPropertyName("specs")] SpecsInfo? Specs,
    [property: JsonPropertyName("targets")] List<TargetInfo>? Targets,
    [property: JsonPropertyName("files")] List<FtbFileInfo>? Files
);

public record FtbFileInfo(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("sha1")] string? Sha1,
    [property: JsonPropertyName("size")] long Size,
    [property: JsonPropertyName("clientonly")] bool ClientOnly,
    [property: JsonPropertyName("serveronly")] bool ServerOnly,
    [property: JsonPropertyName("optional")] bool Optional,
    [property: JsonPropertyName("updated")] long Updated
);

public record ChangelogResult(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("html")] string? Html,
    [property: JsonPropertyName("updated")] long Updated
);

public record CacheData(
    [property: JsonPropertyName("savedAt")] long SavedAt,
    [property: JsonPropertyName("modpacks")] List<ModpackInfo> Modpacks
);
