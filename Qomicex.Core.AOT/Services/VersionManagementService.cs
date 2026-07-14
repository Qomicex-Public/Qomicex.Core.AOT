using Qomicex.Core.AOT.Exceptions;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Interfaces.Services;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionManifest;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Services;

internal class VersionManagementService : IVersionManagementService
{
    private readonly string _gameRootPath;
    private readonly VersionManifestService _manifestService;
    private readonly IVersionLocator _versionLocator;
    private readonly IResourceCompleter _resourceCompleter;
    private readonly IDownloadSourceManager _downloadSourceManager;
    private readonly VersionManifestCache _cache;
    private readonly CombinedJsonContext _jsonCtx = CombinedJsonContext.Default;

    private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

    public VersionManagementService(
        string gameRootPath,
        HttpClient? httpClient = null,
        IDownloadSourceManager? downloadSourceManager = null)
    {
        _gameRootPath = gameRootPath;
        _manifestService = new VersionManifestService(httpClient ?? new HttpClient());
        _versionLocator = new DefaultVersionLocator(gameRootPath);
        _downloadSourceManager = downloadSourceManager ?? new DefaultDownloadSourceManager();
        _resourceCompleter = new DefaultResourceCompleter(gameRootPath, _downloadSourceManager);
        _cache = new VersionManifestCache(Path.Combine(gameRootPath, "cache", "version_manifest.json"));
    }

    /// <summary>
    /// 获取版本清单（优先使用缓存）
    /// </summary>
    public async Task<VersionManifestRoot> GetManifestAsync(bool forceRefresh = false)
    {
        // 如果不是强制刷新，尝试从缓存加载
        if (!forceRefresh && _cache.HasValidCache())
        {
            var cached = await _cache.LoadFromCacheAsync();
            if (cached != null)
                return cached;
        }

        // 缓存无效或不存在，从网络获取
        var manifest = await _manifestService.GetVersionManifestAsync();
        await _cache.SaveToCacheAsync(manifest);
        return manifest;
    }

    /// <summary>
    /// 获取所有可用版本列表
    /// </summary>
    public async Task<List<ManifestVersionInfo>> GetAvailableVersionsAsync(bool forceRefresh = false)
    {
        var manifest = await GetManifestAsync(forceRefresh);
        return manifest.Versions;
    }

    /// <summary>
    /// 获取最新版本信息
    /// </summary>
    public async Task<LatestVersionInfo> GetLatestVersionsAsync(bool forceRefresh = false)
    {
        var manifest = await GetManifestAsync(forceRefresh);
        return manifest.Latest;
    }

    /// <summary>
    /// 获取特定版本的完整元数据
    /// </summary>
    public async Task<CompleteVersionMetadata> GetVersionMetadataAsync(string versionId)
    {
        // 1. 先从本地已安装的版本中读取
        var localMetadata = _versionLocator.GetVersionMetadata(versionId);
        if (localMetadata != null)
            return localMetadata;

        // 2. 从网络获取
        var manifest = await GetManifestAsync();
        var versionInfo = manifest.Versions.FirstOrDefault(v => v.Id == versionId);

        if (versionInfo == null)
            throw new VersionNotFoundException($"版本 {versionId} 在官方清单中不存在");

        if (string.IsNullOrEmpty(versionInfo.Url))
            throw new VersionMetadataException($"版本 {versionId} 的元数据URL无效");

        // 3. 下载并保存到本地
        var metadata = await _manifestService.GetVersionMetadataAsync(versionInfo.Url);
        await SaveVersionMetadataToLocal(versionId, metadata);

        return metadata;
    }

    /// <summary>
    /// 检查版本是否已安装
    /// </summary>
    public bool IsVersionInstalled(string versionId)
    {
        return _versionLocator.IsVersionInstalled(versionId);
    }

    /// <summary>
    /// 获取已安装的版本列表
    /// </summary>
    public List<LocalVersionInfo> GetInstalledVersions()
    {
        return _versionLocator.GetAllVersions();
    }

    /// <summary>
    /// 安装指定版本
    /// </summary>
    public async Task InstallVersionAsync(string versionId, IProgress<DownloadProgress>? progress = null)
    {
        // 1. 获取版本元数据
        var metadata = await GetVersionMetadataAsync(versionId);

        // 2. 检查是否需要处理版本继承
        if (!string.IsNullOrEmpty(metadata.InheritsFrom))
        {
            // 如果继承自其他版本，先确保父版本已安装
            await InstallVersionAsync(metadata.InheritsFrom, progress);
        }

        // 3. 创建版本目录
        var versionPath = Path.Combine(_gameRootPath, "versions", versionId);
        Directory.CreateDirectory(versionPath);

        // 4. 保存版本JSON文件
        var jsonPath = Path.Combine(versionPath, $"{versionId}.json");
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(metadata, _jsonCtx.CompleteVersionMetadata);
        await File.WriteAllTextAsync(jsonPath, jsonContent);

        // 5. 补全资源（使用资源补全器）
        await _resourceCompleter.CompleteResourcesAsync(metadata, progress);

        // 6. 刷新版本定位器缓存
        _versionLocator.RefreshCache();
    }

    public async Task UninstallVersionAsync(string versionId)
    {
        var versionPath = _versionLocator.GetVersionPath(versionId);
        if (Directory.Exists(versionPath))
            await Task.Run(() => Directory.Delete(versionPath, true));
        _versionLocator.RefreshCache();
    }

    /// <summary>
    /// 将版本元数据保存到本地
    /// </summary>
    private async Task SaveVersionMetadataToLocal(string versionId, CompleteVersionMetadata metadata)
    {
        var versionPath = Path.Combine(_gameRootPath, "versions", versionId);
        Directory.CreateDirectory(versionPath);

        var jsonPath = Path.Combine(versionPath, $"{versionId}.json");
        var jsonContent = System.Text.Json.JsonSerializer.Serialize(metadata, _jsonCtx.CompleteVersionMetadata);
        await File.WriteAllTextAsync(jsonPath, jsonContent);
    }
}