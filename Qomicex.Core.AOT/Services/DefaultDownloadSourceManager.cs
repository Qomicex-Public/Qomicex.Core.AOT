using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.Models.Download;

namespace Qomicex.Core.AOT.Services;

internal class DefaultDownloadSourceManager : IDownloadSourceManager
{
    private readonly List<DownloadSource> _sources;
    private readonly HttpClient _httpClient;

    public DefaultDownloadSourceManager()
        : this(DownloadMirror.BMCLAPI)
    {
    }

    public DefaultDownloadSourceManager(DownloadMirror preferredMirror)
    {
        _httpClient = new HttpClient();
        _sources =
        [
            new(DownloadSourceType.Official, "Mojang官方", "https://launcher.mojang.com/", true, 100),
            new(DownloadSourceType.BMCLAPI, "BMCLAPI镜像", "https://bmclapi2.bangbang93.com/", true, 1),
        ];

        if (preferredMirror == DownloadMirror.Official)
            SetPreferredSource(DownloadSourceType.Official);
    }

    private void SetPreferredSource(DownloadSourceType type)
    {
        var ordered = _sources.OrderBy(s => s.Priority).ToList();
        var target = ordered.FirstOrDefault(s => s.Type == type);
        if (target == null) return;

        var minPriority = ordered[0].Priority;
        if (target.Priority != minPriority)
        {
            target = target with { Priority = minPriority - 1 };
            var index = _sources.FindIndex(s => s.Type == type);
            if (index >= 0)
                _sources[index] = target;
        }
    }

    public IReadOnlyList<DownloadSource> GetAvailableSources(Models.ResourceType resourceType)
    {
        return _sources.Where(s => s.IsEnabled).ToList();
    }

    public IEnumerable<string> GenerateMirrorUrls(string originalUrl, Models.ResourceType resourceType)
    {
        yield return originalUrl;

        foreach (var source in _sources.Where(s => s.IsEnabled).OrderBy(s => s.Priority))
        {
            var mirrorUrl = ConvertToMirrorUrl(originalUrl, source);
            if (mirrorUrl != null && mirrorUrl != originalUrl)
                yield return mirrorUrl;
        }
    }

    private static string? ConvertToMirrorUrl(string originalUrl, DownloadSource source)
    {
        var baseUrl = source.BaseUrl.TrimEnd('/');

        if (source.Type != DownloadSourceType.BMCLAPI)
            return null;

        if (originalUrl.Contains("launcher.mojang.com/maven"))
            return $"{baseUrl}/maven/{originalUrl.Split("maven/")[^1]}";
        if (originalUrl.Contains("resources.download.minecraft.net"))
            return $"{baseUrl}/assets/{originalUrl.Split("assets/")[^1]}";
        if (originalUrl.Contains("piston-meta.mojang.com"))
            return $"{baseUrl}/meta/{originalUrl.Split("meta/")[^1]}";

        return null;
    }

    public async Task<bool> TestSourceAsync(DownloadSource source)
    {
        try
        {
            var testUrl = $"{source.BaseUrl.TrimEnd('/')}/";
            using var request = new HttpRequestMessage(HttpMethod.Head, testUrl);
            using var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void AddCustomSource(DownloadSource source)
    {
        if (_sources.Any(s => s.Type == source.Type))
            throw new InvalidOperationException($"已存在类型为 {source.Type} 的下载源");
        _sources.Add(source);
    }

    public DownloadSource? GetPreferredSource(Models.ResourceType resourceType)
    {
        return _sources.Where(s => s.IsEnabled).MinBy(s => s.Priority);
    }
}
