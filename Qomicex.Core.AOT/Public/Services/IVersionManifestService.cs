using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Interfaces.Services;

public interface IVersionManifestService
{
    Task<VersionManifestRoot> GetVersionManifestAsync();

    Task<CompleteVersionMetadata> GetVersionMetadataAsync(string url);
}
