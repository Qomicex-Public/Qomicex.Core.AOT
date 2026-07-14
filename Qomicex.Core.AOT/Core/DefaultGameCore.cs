using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Services;

namespace Qomicex.Core.AOT.Core;

public class DefaultGameCore : IDisposable
{
    private readonly IVersionManagementService _versionService;
    private bool _disposed;

    public DefaultGameCore(string gameRootPath)
    {
        if (string.IsNullOrEmpty(gameRootPath))
            throw new ArgumentException("游戏根目录不能为空", nameof(gameRootPath));
        _versionService = new VersionManagementService(gameRootPath);
    }

    public DefaultGameCore(string gameRootPath, IDownloadSourceManager downloadSourceManager)
    {
        if (string.IsNullOrEmpty(gameRootPath))
            throw new ArgumentException("游戏根目录不能为空", nameof(gameRootPath));
        _versionService = new VersionManagementService(gameRootPath, downloadSourceManager: downloadSourceManager);
    }

    public async Task<List<ManifestVersionInfo>> GetAvailableVersionsAsync(bool forceRefresh = false)
        => await _versionService.GetAvailableVersionsAsync(forceRefresh);

    public async Task<LatestVersionInfo> GetLatestVersionsAsync(bool forceRefresh = false)
        => await _versionService.GetLatestVersionsAsync(forceRefresh);

    public List<LocalVersionInfo> GetInstalledVersions()
        => _versionService.GetInstalledVersions();

    public async Task InstallVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null)
        => await _versionService.InstallVersionAsync(versionId, progress);

    public async Task UninstallVersionAsync(string versionId)
        => await _versionService.UninstallVersionAsync(versionId);

    public bool IsVersionInstalled(string versionId)
        => _versionService.IsVersionInstalled(versionId);

    public async Task<CompleteVersionMetadata> GetVersionMetadataAsync(string versionId)
        => await _versionService.GetVersionMetadataAsync(versionId);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;
    }
}
