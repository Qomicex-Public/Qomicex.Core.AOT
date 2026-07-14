namespace Qomicex.Core.AOT.Models.Download;

public enum DownloadSourceType
{
    Official,
    BMCLAPI,
    /// <summary>
    /// 已经弃用,但是说不定会复活呢 (
    /// </summary>
    MCBBS,
    Custom
}

public record DownloadSource(
    DownloadSourceType Type,
    string Name,
    string BaseUrl,
    bool IsEnabled,
    int Priority,
    string? Description = null
);


