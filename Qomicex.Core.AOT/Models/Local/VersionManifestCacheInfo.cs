namespace Qomicex.Core.AOT.Models.Local;

public record VersionManifestCacheInfo(
    DateTime CachedTime,
    int VersionCount,
    string LatestRelease,
    string LatestSnapshot
);
