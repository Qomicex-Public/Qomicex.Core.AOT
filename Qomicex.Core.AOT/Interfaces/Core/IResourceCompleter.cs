using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Interfaces.Core;

public interface IResourceCompleter
{
    Task CompleteResourcesAsync(CompleteVersionMetadata metadata, IProgress<DownloadProgress>? progress = null);

    Task<bool> CheckResourcesCompleteAsync(CompleteVersionMetadata metadata);
}
