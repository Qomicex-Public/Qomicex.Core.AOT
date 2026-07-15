using System.Text.Json.Serialization;

namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public record CurseForgeInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("downloadCount")] int DownloadCount,
    [property: JsonPropertyName("isFeatured")] bool IsFeatured,
    [property: JsonPropertyName("categories")] List<CategoryMeta>? Categories,
    [property: JsonPropertyName("authors")] List<AuthorMeta>? Authors,
    [property: JsonPropertyName("screenshots")] List<ScreenshotsMeta>? Screenshots,
    [property: JsonPropertyName("latestFilesIndexes")] List<CurseForgeFilesMeta>? Files
);
