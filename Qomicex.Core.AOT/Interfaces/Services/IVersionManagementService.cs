using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Interfaces.Services;

public interface IVersionManagementService
{
    Task<VersionManifestRoot> GetManifestAsync(bool forceRefresh = false);

    Task<List<ManifestVersionInfo>> GetAvailableVersionsAsync(bool forceRefresh = false);

    Task<LatestVersionInfo> GetLatestVersionsAsync(bool forceRefresh = false);

    Task<CompleteVersionMetadata> GetVersionMetadataAsync(string versionId);

    bool IsVersionInstalled(string versionId);

    Task InstallVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null);

    Task UninstallVersionAsync(string versionId);

    List<LocalVersionInfo> GetInstalledVersions();
}