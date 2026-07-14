using Qomicex.Core.AOT.Models.Download;

namespace Qomicex.Core.AOT.Interfaces.Events;

public interface IInstallProgressEvent
{
    event EventHandler<DownloadProgress>? InstallProgressChanged;
}
