using System.Security.Cryptography;
using Qomicex.Core.AOT.Exceptions;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Download;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Services;

internal class DefaultResourceCompleter : IResourceCompleter
{
    private readonly string _gameRootPath;
    private readonly IDownloadSourceManager _sourceManager;
    private readonly HttpClient _httpClient;
    private readonly int _maxConcurrentDownloads;
    private readonly AssetIndexDataJsonContext _assetIndexCtx = AssetIndexDataJsonContext.Default;

    public DefaultResourceCompleter(
        string gameRootPath,
        IDownloadSourceManager sourceManager,
        HttpClient? httpClient = null,
        int maxConcurrentDownloads = 8)
    {
        _gameRootPath = gameRootPath;
        _sourceManager = sourceManager;
        _httpClient = httpClient ?? new HttpClient();
        _maxConcurrentDownloads = maxConcurrentDownloads;
    }

    public async Task CompleteResourcesAsync(CompleteVersionMetadata metadata, IProgress<DownloadProgress>? progress = null)
    {
        var downloadTasks = new List<Task>();

        if (metadata.Downloads.Client != null)
            downloadTasks.Add(DownloadArtifactAsync(metadata.Downloads.Client, progress));

        foreach (var library in metadata.Libraries)
        {
            var artifacts = GetLibraryArtifacts(library);
            foreach (var artifact in artifacts)
                downloadTasks.Add(DownloadArtifactAsync(artifact, progress));
        }

        if (metadata.AssetIndex != null && !string.IsNullOrEmpty(metadata.AssetIndex.Url))
            downloadTasks.Add(DownloadAssetIndexAsync(metadata.AssetIndex, progress));

        using var semaphore = new SemaphoreSlim(_maxConcurrentDownloads);
        var throttled = downloadTasks.Select(async task =>
        {
            await semaphore.WaitAsync();
            try { await task; }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(throttled);
    }

    public async Task<bool> CheckResourcesCompleteAsync(CompleteVersionMetadata metadata)
    {
        var clientPath = Path.Combine(_gameRootPath, "versions", metadata.Id, $"{metadata.Id}.jar");
        if (!File.Exists(clientPath))
            return false;

        foreach (var library in metadata.Libraries)
        {
            var artifacts = GetLibraryArtifacts(library);
            foreach (var artifact in artifacts)
            {
                var localPath = Path.Combine(_gameRootPath, "libraries", artifact.Path);
                if (!File.Exists(localPath))
                    return false;
                if (!ValidateFileHash(localPath, artifact.Sha1))
                    return false;
            }
        }

        return true;
    }

    private async Task DownloadArtifactAsync(Artifact artifact, IProgress<DownloadProgress>? progress)
    {
        var localPath = Path.Combine(_gameRootPath, "libraries", artifact.Path);
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(localPath) && ValidateFileHash(localPath, artifact.Sha1))
            return;

        var mirrorUrls = _sourceManager.GenerateMirrorUrls(artifact.Url, Models.ResourceType.Library);
        var lastException = new Exception("所有下载源都失败了");

        foreach (var url in mirrorUrls)
        {
            try
            {
                await DownloadFileWithRetryAsync(url, localPath, artifact.Sha1, progress);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        throw new DownloadFailedException($"下载 {artifact.Path} 失败", lastException);
    }

    private async Task DownloadFileWithRetryAsync(
        string url, string localPath, string expectedSha1,
        IProgress<DownloadProgress>? progress, int maxRetries = 3)
    {
        for (var retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;
                var lastUpdate = DateTime.Now;
                var bytesSinceUpdate = 0L;

                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(localPath);

                var buffer = new byte[8192];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    downloadedBytes += bytesRead;
                    bytesSinceUpdate += bytesRead;

                    var now = DateTime.Now;
                    if ((now - lastUpdate).TotalSeconds >= 0.5)
                    {
                        var speed = bytesSinceUpdate / (now - lastUpdate).TotalSeconds;
                        progress?.Report(new DownloadProgress(
                            FileName: Path.GetFileName(localPath),
                            DownloadedBytes: downloadedBytes,
                            TotalBytes: totalBytes,
                            Percentage: totalBytes > 0 ? downloadedBytes * 100.0 / totalBytes : 0,
                            SpeedBytesPerSecond: (long)speed,
                            RetryCount: retry,
                            Status: DownloadStatus.Downloading
                        ));
                        bytesSinceUpdate = 0;
                        lastUpdate = now;
                    }
                }

                if (!string.IsNullOrEmpty(expectedSha1) && !ValidateFileHash(localPath, expectedSha1))
                {
                    File.Delete(localPath);
                    throw new Exception($"文件哈希不匹配: {Path.GetFileName(localPath)}");
                }

                progress?.Report(new DownloadProgress(
                    FileName: Path.GetFileName(localPath),
                    DownloadedBytes: downloadedBytes,
                    TotalBytes: totalBytes,
                    Percentage: 100,
                    SpeedBytesPerSecond: 0,
                    RetryCount: retry,
                    Status: DownloadStatus.Completed
                ));

                return;
            }
            catch when (retry < maxRetries - 1)
            {
                progress?.Report(new DownloadProgress(
                    FileName: Path.GetFileName(localPath),
                    DownloadedBytes: 0, TotalBytes: 0, Percentage: 0,
                    SpeedBytesPerSecond: 0, RetryCount: retry + 1,
                    Status: DownloadStatus.Retrying
                ));
                await Task.Delay(1000 * (retry + 1));
            }
        }
    }

    private async Task DownloadAssetIndexAsync(AssetIndex assetIndex, IProgress<DownloadProgress>? progress)
    {
        var localPath = Path.Combine(_gameRootPath, "assets", "indexes", $"{assetIndex.Id}.json");
        var directory = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(localPath) && ValidateFileHash(localPath, assetIndex.Sha1))
            return;

        var mirrorUrls = _sourceManager.GenerateMirrorUrls(assetIndex.Url, Models.ResourceType.AssetIndex);
        foreach (var url in mirrorUrls)
        {
            try
            {
                await DownloadFileWithRetryAsync(url, localPath, assetIndex.Sha1, progress);
                await ProcessAssetIndexAsync(localPath, progress);
                return;
            }
            catch { /* try next mirror */ }
        }
    }

    private async Task ProcessAssetIndexAsync(string indexPath, IProgress<DownloadProgress>? progress)
    {
        var json = await File.ReadAllTextAsync(indexPath);
        var indexData = System.Text.Json.JsonSerializer.Deserialize(json, _assetIndexCtx.AssetIndexData);

        if (indexData?.Objects == null)
            return;

        var totalAssets = indexData.Objects.Count;
        var downloaded = 0;

        foreach (var asset in indexData.Objects)
        {
            var hash = asset.Value.Hash;
            var assetPath = Path.Combine(hash[..2], hash);
            var localAssetPath = Path.Combine(_gameRootPath, "assets", "objects", assetPath);

            if (!File.Exists(localAssetPath) || !ValidateFileHash(localAssetPath, hash))
            {
                var url = $"https://resources.download.minecraft.net/{assetPath}";
                foreach (var mirrorUrl in _sourceManager.GenerateMirrorUrls(url, Models.ResourceType.Asset))
                {
                    try
                    {
                        await DownloadFileWithRetryAsync(mirrorUrl, localAssetPath, hash, null);
                        break;
                    }
                    catch { /* try next mirror */ }
                }
            }

            downloaded++;
            if (downloaded % 100 == 0)
            {
                progress?.Report(new DownloadProgress(
                    FileName: $"Assets ({downloaded}/{totalAssets})",
                    DownloadedBytes: downloaded, TotalBytes: totalAssets,
                    Percentage: downloaded * 100.0 / totalAssets,
                    SpeedBytesPerSecond: 0, RetryCount: 0,
                    Status: DownloadStatus.Downloading
                ));
            }
        }
    }

    private List<Artifact> GetLibraryArtifacts(Library library)
    {
        var artifacts = new List<Artifact>();

        if (library.Downloads.Artifact != null)
        {
            if (library.Rules == null || ShouldIncludeLibrary(library.Rules))
                artifacts.Add(library.Downloads.Artifact);
        }

        if (library.Natives != null && library.Downloads.Classifiers != null)
        {
            var osName = GetCurrentOsName();
            if (library.Natives.TryGetValue(osName, out var nativeClassifier))
            {
                var classifierKey = nativeClassifier.Replace("${arch}", GetCurrentArch());
                if (library.Downloads.Classifiers.TryGetValue(classifierKey, out var nativeArtifact))
                    artifacts.Add(nativeArtifact);
            }
        }

        return artifacts;
    }

    private static bool ShouldIncludeLibrary(List<Rule> rules)
    {
        var allow = false;
        foreach (var rule in rules)
        {
            if (rule.Action == "allow")
            {
                if (rule.Os == null || IsOsMatch(rule.Os))
                    allow = true;
            }
            else if (rule.Action == "disallow")
            {
                if (rule.Os == null || IsOsMatch(rule.Os))
                    allow = false;
            }
        }
        return allow;
    }

    private static bool IsOsMatch(OsRequirement os)
    {
        if (os.Name != GetCurrentOsName())
            return false;

        if (!string.IsNullOrEmpty(os.Version) && !Environment.OSVersion.VersionString.Contains(os.Version))
            return false;

        if (!string.IsNullOrEmpty(os.Arch) && os.Arch != GetCurrentArch())
            return false;

        return true;
    }

    private static string GetCurrentOsName() =>
        OperatingSystem.IsWindows() ? "windows" :
        OperatingSystem.IsLinux() ? "linux" :
        OperatingSystem.IsMacOS() ? "osx" : "unknown";

    private static string GetCurrentArch() =>
        Environment.Is64BitOperatingSystem ? "64" : "32";

    private static bool ValidateFileHash(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath) || string.IsNullOrEmpty(expectedHash))
            return false;

        using var sha1 = SHA1.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha1.ComputeHash(stream);
        var actualHash = Convert.ToHexString(hash).ToLower();
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

}
