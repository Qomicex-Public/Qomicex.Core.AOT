namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public class CurseForgeSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public string DownloadCount { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public string IconUrl { get; set; } = string.Empty;
    public List<CategoryMeta> Categories { get; set; } = [];
    public List<AuthorMeta> Authors { get; set; } = [];
    public List<ScreenshotsMeta> Screenshots { get; set; } = [];
}
