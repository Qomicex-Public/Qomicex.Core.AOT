using Qomicex.Core.AOT.Models.Download;

namespace Qomicex.Core.AOT.Interfaces.Events;

public interface IDownloadProgressEvent
{
    event EventHandler<DownloadProgress>? DownloadProgressChanged;
}
