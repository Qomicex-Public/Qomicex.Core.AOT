namespace Qomicex.Core.AOT.Models.Local;

public record LocalVersionInfo(
    string Id,
    string Type,
    DateTime ReleaseTime,
    bool IsComplete,
    string VersionPath,
    long TotalSize
);
