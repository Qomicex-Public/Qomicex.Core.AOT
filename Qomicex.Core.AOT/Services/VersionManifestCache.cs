using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.VersionManifest;

namespace Qomicex.Core.AOT.Services;

internal class VersionManifestCache
{
    private readonly string _cacheFilePath;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly CombinedJsonContext _ctx = CombinedJsonContext.Default;

    public VersionManifestCache(string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
        var directory = Path.GetDirectoryName(cacheFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public bool HasValidCache()
    {
        if (!File.Exists(_cacheFilePath))
            return false;

        var fileInfo = new FileInfo(_cacheFilePath);
        return DateTime.Now - fileInfo.LastWriteTime < _cacheDuration;
    }

    public async Task<VersionManifestRoot?> LoadFromCacheAsync()
    {
        if (!File.Exists(_cacheFilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(_cacheFilePath);
            return System.Text.Json.JsonSerializer.Deserialize(json, _ctx.VersionManifestRoot);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveToCacheAsync(VersionManifestRoot manifest)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(manifest, _ctx.VersionManifestRoot);
        await File.WriteAllTextAsync(_cacheFilePath, json);
    }

    public void InvalidateCache()
    {
        if (File.Exists(_cacheFilePath))
            File.Delete(_cacheFilePath);
    }
}
