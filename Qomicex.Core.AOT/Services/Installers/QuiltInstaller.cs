using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

public class QuiltInstaller : InstallerBase, IInstaller
{
    private string _downloadSource;
    private readonly string _gameDir;

    public QuiltInstaller(int downloadSource, string gameDir)
    {
        _downloadSource = downloadSource == 1 ? "https://bmclapi2.bangbang93.com/quilt-meta/" : "https://meta.quiltmc.org/";
        _gameDir = gameDir;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? quiltVersion, string? gameVersion, string? para3, string? para4)
    {
        if (quiltVersion == null) throw new ArgumentNullException(nameof(quiltVersion));
        if (gameVersion == null) throw new ArgumentNullException(nameof(gameVersion));
        await InstallQuiltAsync(versionId, quiltVersion, gameVersion, inheritsFromJson);
    }

    public async Task<bool> InstallQuiltAsync(string versionId, string quiltVersion, string gameVersion, string? inheritsFromJson = null)
    {
        var jsonData = await BuildJson(versionId, quiltVersion, gameVersion, _gameDir);
        if (string.IsNullOrEmpty(jsonData))
            throw new Exception("构建JSON数据失败");

        var versionDir = $"{_gameDir}/versions/{versionId}";
        if (!Directory.Exists(versionDir))
            Directory.CreateDirectory(versionDir);

        if (!string.IsNullOrEmpty(inheritsFromJson))
            jsonData = MergeVersionJson(inheritsFromJson, jsonData, versionId);
        else if (File.Exists(Path.Combine(_gameDir, "versions", gameVersion, $"{gameVersion}.json")))
        {
            var mainVersionJson = await File.ReadAllTextAsync(Path.Combine(_gameDir, "versions", gameVersion, $"{gameVersion}.json"));
            jsonData = MergeVersionJson(mainVersionJson, jsonData, versionId);
        }
        else
            throw new Exception("主版本JSON文件不存在");

        await File.WriteAllTextAsync(Path.Combine(_gameDir, "versions", versionId, $"{versionId}.json"), jsonData);
        return true;
    }

    private async Task<string> BuildJson(string versionId, string quiltVersion, string gameVersion, string gameDir)
    {
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"{_downloadSource}v3/versions/loader/{gameVersion}/{quiltVersion}/profile/json");
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

                await DownloadFileAsync(client, $"{urlDomain}{MavenToPath(lib!["name"]?.ToString()!)}",
                    $"{gameDir}/libraries/{MavenToPath(lib!["name"]?.ToString()!)}");
            }
        }

        meta["id"] = versionId;
        return meta.ToJsonString();
    }

    public async Task<List<FabricInstaller.MissFileData>> GetMissQuiltLibraries(string quiltVersion, string gameVersion, string gameDir)
    {
        var missFiles = new List<FabricInstaller.MissFileData>();
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"{_downloadSource}v3/versions/loader/{gameVersion}/{quiltVersion}/profile/json");
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

                var libPath = Path.Combine(gameDir, "libraries", MavenToPath(lib!["name"]?.ToString()!));
                if (File.Exists(libPath))
                {
                    if (!string.IsNullOrEmpty(lib!["sha1"]?.ToString()) &&
                        FabricInstaller.VerifyFileSha1(libPath, lib!["sha1"]?.ToString()!))
                        continue;
                }

                missFiles.Add(new FabricInstaller.MissFileData
                {
                    Name = lib!["name"]?.ToString()!,
                    Path = $"{gameDir}/libraries/{MavenToPath(lib!["name"]?.ToString()!)}",
                    Url = $"{urlDomain}{MavenToPath(lib!["name"]?.ToString()!)}",
                    Sha1 = lib!["sha1"]?.ToString() ?? "",
                });
            }
        }
        return missFiles;
    }
}
