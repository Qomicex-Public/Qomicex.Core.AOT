namespace Qomicex.Core.AOT.Models.Download;

public enum DownloadSourceType
{
    Official,
    BMCLAPI,
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


