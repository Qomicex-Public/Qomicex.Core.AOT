using Qomicex.Core.AOT.Models.Download;

namespace Qomicex.Core.AOT.Interfaces.Core;

public interface IDownloadSourceManager
{
    IReadOnlyList<DownloadSource> GetAvailableSources(Models.ResourceType resourceType);

    IEnumerable<string> GenerateMirrorUrls(string originalUrl, Models.ResourceType resourceType);

    Task<bool> TestSourceAsync(DownloadSource source);

    void AddCustomSource(DownloadSource source);

    DownloadSource? GetPreferredSource(Models.ResourceType resourceType);
}
