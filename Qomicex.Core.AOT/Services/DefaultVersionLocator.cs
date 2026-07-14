using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Services;

internal class DefaultVersionLocator : IVersionLocator
{
    private readonly string _versionsRootPath;
    private readonly Dictionary<string, LocalVersionInfo> _versionCache = new();
    private readonly Dictionary<string, CompleteVersionMetadata> _metadataCache = new();
    private readonly VersionMetadataJsonContext _metadataCtx = VersionMetadataJsonContext.Default;
    private bool _isCacheDirty = true;

    public DefaultVersionLocator(string gameRootPath)
    {
        _versionsRootPath = Path.Combine(gameRootPath, "versions");
        Directory.CreateDirectory(_versionsRootPath);
        RefreshCache();
    }

    public List<LocalVersionInfo> GetAllVersions()
    {
        EnsureCacheFresh();
        return _versionCache.Values.ToList();
    }

    public CompleteVersionMetadata? GetVersionMetadata(string versionId)
    {
        if (string.IsNullOrEmpty(versionId))
            return null;

        EnsureCacheFresh();

        if (_metadataCache.TryGetValue(versionId, out var metadata))
            return metadata;

        var versionPath = GetVersionPath(versionId);
        var jsonPath = Path.Combine(versionPath, $"{versionId}.json");

        if (!File.Exists(jsonPath))
            return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            metadata = System.Text.Json.JsonSerializer.Deserialize(json, _metadataCtx.CompleteVersionMetadata);
            if (metadata != null)
                _metadataCache[versionId] = metadata;
            return metadata;
        }
        catch
        {
            return null;
        }
    }

    public bool IsVersionInstalled(string versionId)
    {
        EnsureCacheFresh();
        return _versionCache.ContainsKey(versionId);
    }

    public void RefreshCache()
    {
        _isCacheDirty = true;
        _versionCache.Clear();
        _metadataCache.Clear();
        EnsureCacheFresh();
    }

    public string GetVersionPath(string versionId)
    {
        return Path.Combine(_versionsRootPath, versionId);
    }

    private void EnsureCacheFresh()
    {
        if (!_isCacheDirty)
            return;

        _versionCache.Clear();
        _metadataCache.Clear();

        if (!Directory.Exists(_versionsRootPath))
        {
            _isCacheDirty = false;
            return;
        }

        foreach (var versionDir in Directory.GetDirectories(_versionsRootPath))
        {
            var versionId = Path.GetFileName(versionDir);
            var jsonPath = Path.Combine(versionDir, $"{versionId}.json");

            if (!File.Exists(jsonPath))
                continue;

            try
            {
                var metadata = GetVersionMetadata(versionId);
                if (metadata == null)
                    continue;

                var isComplete = IsVersionComplete(versionId, metadata);
                var totalSize = CalculateVersionSize(versionDir);

                _versionCache[versionId] = new LocalVersionInfo(
                    Id: versionId,
                    Type: metadata.Type,
                    ReleaseTime: metadata.ReleaseTime,
                    IsComplete: isComplete,
                    VersionPath: versionDir,
                    TotalSize: totalSize
                );
            }
            catch
            {
                // ponytail: skip unparseable version dirs
            }
        }

        _isCacheDirty = false;
    }

    private bool IsVersionComplete(string versionId, CompleteVersionMetadata metadata)
    {
        var clientPath = Path.Combine(GetVersionPath(versionId), $"{versionId}.jar");
        return File.Exists(clientPath);
    }

    private static long CalculateVersionSize(string versionPath)
    {
        try
        {
            return new DirectoryInfo(versionPath)
                .GetFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
}
