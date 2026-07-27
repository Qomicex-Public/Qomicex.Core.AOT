using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Utils;

namespace Qomicex.Core.AOT.Services.Installers;

internal class LegacyFabricInstaller : InstallerBase, IInstaller
{
    private readonly string _downloadSource;
    private readonly string _gameDir;

    public LegacyFabricInstaller(int downloadSource, string gameDir)
    {
        _downloadSource = "https://meta.legacyfabric.net/";
        _gameDir = gameDir;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? lfVersion, string? gameVersion, string? para3, string? para4)
    {
        if (lfVersion == null) throw new ArgumentNullException(nameof(lfVersion));
        if (gameVersion == null) throw new ArgumentNullException(nameof(gameVersion));
        await InstallLegacyFabricAsync(versionId, lfVersion, gameVersion, inheritsFromJson);
    }

    public async Task<bool> InstallLegacyFabricAsync(string versionId, string lfVersion, string gameVersion, string? inheritsFromJson = null)
    {
        var jsonData = await BuildJson(versionId, lfVersion, gameVersion, _gameDir);
        if (string.IsNullOrEmpty(jsonData))
            throw new Exception("构建JSON数据失败");

        var versionDir = $"{_gameDir}/versions/{versionId}";
        if (!Directory.Exists(versionDir))
            Directory.CreateDirectory(versionDir);

        if (!string.IsNullOrEmpty(inheritsFromJson))
            jsonData = MergeVersionJson(inheritsFromJson, jsonData, versionId);
        else
            throw new Exception("主版本JSON文件不存在");

        await File.WriteAllTextAsync(Path.Combine(_gameDir, "versions", versionId, $"{versionId}.json"), jsonData);
        return true;
    }

    private async Task<string> BuildJson(string versionId, string lfVersion, string gameVersion, string gameDir)
    {
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"{_downloadSource}v2/versions/loader/{gameVersion}/{lfVersion}/profile/json");
        if (!result.IsSuccessStatusCode)
            throw new Exception("获取Launcher Meta失败");

        var metaStr = await result.Content.ReadAsStringAsync();
        var meta = JsonNode.Parse(metaStr)!.AsObject();

        var libs = meta["libraries"] as JsonArray;
        if (libs != null)
        {
            foreach (var lib in libs)
            {
                var urlDomain = _downloadSource;
                if (!string.IsNullOrEmpty(lib!["url"]?.ToString()))
                    urlDomain = lib!["url"]?.ToString()!;

                var mavenName = GetMavenNameWithClassifier(lib);
                await DownloadFileAsync(client, $"{urlDomain}{MavenToPath(mavenName)}",
                    $"{gameDir}/libraries/{MavenToPath(mavenName)}");
            }
        }

        meta["id"] = versionId;
        return meta.ToJsonString();
    }

    private static string GetMavenNameWithClassifier(JsonNode lib)
    {
        var name = lib["name"]?.ToString();
        if (string.IsNullOrEmpty(name)) return "";
        var natives = lib["natives"]?.AsObject();
        if (natives != null)
        {
            var osName = SystemHelper.GetCurrentOsName();
            if (natives.TryGetPropertyValue(osName, out var classifierNode) && classifierNode != null)
            {
                var classifier = classifierNode.ToString().Replace("${arch}", SystemHelper.GetCurrentArch());
                return $"{name}:{classifier}";
            }
        }
        return name;
    }

    public Task<List<MissFileData>> GetMissLibrariesAsync(string? para1, string? para2, string? para3)
    {
        if (para1 == null || para2 == null || para3 == null) return Task.FromResult(new List<MissFileData>());
        return GetMissLegacyFabricLibraries(para1, para2, para3);
    }

    public async Task<List<MissFileData>> GetMissLegacyFabricLibraries(string lfVersion, string gameVersion, string gameDir)
    {
        var missFiles = new List<MissFileData>();
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"{_downloadSource}v2/versions/loader/{gameVersion}/{lfVersion}/profile/json");
        if (!result.IsSuccessStatusCode)
            throw new Exception("获取Launcher Meta失败");

        var metaStr = await result.Content.ReadAsStringAsync();
        var meta = JsonNode.Parse(metaStr)!.AsObject();

        var libs = meta["libraries"] as JsonArray;
        if (libs != null)
        {
            foreach (var lib in libs)
            {
                var urlDomain = _downloadSource;
                if (!string.IsNullOrEmpty(lib!["url"]?.ToString()))
                    urlDomain = lib!["url"]?.ToString()!;

                var mavenName = GetMavenNameWithClassifier(lib);
                var libPath = Path.Combine(gameDir, "libraries", MavenToPath(mavenName));
                if (File.Exists(libPath))
                {
                    if (!string.IsNullOrEmpty(lib["sha1"]?.ToString()) &&
                        FabricInstaller.VerifyFileSha1(libPath, lib["sha1"]?.ToString()!))
                        continue;
                }

                missFiles.Add(new MissFileData(
                    lib["name"]?.ToString()!,
                    libPath,
                    $"{urlDomain}{MavenToPath(mavenName)}",
                    lib["sha1"]?.ToString() ?? ""
                ));
            }
        }
        return missFiles;
    }
}
