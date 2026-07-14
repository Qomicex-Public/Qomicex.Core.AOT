namespace Qomicex.Core.AOT.Models.Download;

public record DownloadProgress(
    string FileName,
    long DownloadedBytes,
    long TotalBytes,
    double Percentage,
    long SpeedBytesPerSecond,
    int RetryCount,
    DownloadStatus Status
);

public enum DownloadStatus
{
    Pending,
    Downloading,
    Completed,
    Failed,
    Retrying,
    Cancelled
}
