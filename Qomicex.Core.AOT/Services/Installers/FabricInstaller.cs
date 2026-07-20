using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

internal class FabricInstaller : InstallerBase, IInstaller
{
    private string _downloadSource;
    private readonly string _gameDir;

    public FabricInstaller(int downloadSource, string gameDir)
    {
        _downloadSource = downloadSource == 1 ? "https://bmclapi2.bangbang93.com/fabric-meta/" : "https://meta.fabricmc.net/";
        _gameDir = gameDir;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? fabricVersion, string? gameVersion, string? para3, string? para4)
    {
        if (fabricVersion == null) throw new ArgumentNullException(nameof(fabricVersion));
        if (gameVersion == null) throw new ArgumentNullException(nameof(gameVersion));
        await InstallFabricAsync(versionId, fabricVersion, gameVersion, inheritsFromJson);
    }

    public async Task<bool> InstallFabricAsync(string versionId, string fabricVersion, string gameVersion, string? inheritsFromJson = null)
    {
        var jsonData = await BuildJson(versionId, fabricVersion, gameVersion, _gameDir);
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

    private async Task<string> BuildJson(string versionId, string fabricVersion, string gameVersion, string gameDir)
    {
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"{_downloadSource}v2/versions/loader/{gameVersion}/{fabricVersion}/profile/json");
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

    public async Task<List<MissFileData>> GetMissFabricLibraries(string fabricVersion, string gameVersion, string gameDir)
    {
        var missFiles = new List<MissFileData>();
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"{_downloadSource}v2/versions/loader/{gameVersion}/{fabricVersion}/profile/json");
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
                        VerifyFileSha1(libPath, lib!["sha1"]?.ToString()!))
                        continue;
                }

                var file = new MissFileData
                {
                    Name = lib!["name"]?.ToString()!,
                    Path = libPath,
                    Url = Path.Combine(urlDomain, MavenToPath(lib!["name"]?.ToString()!)),
                    Sha1 = lib!["sha1"]?.ToString() ?? "",
                };

                if (_downloadSource != "https://meta.fabricmc.net/")
                    file.Url = file.Url
                        .Replace("https://meta.fabricmc.net/", "https://bmclapi2.bangbang93.com/fabric-meta")
                        .Replace("https://maven.fabricmc.net/", "https://bmclapi2.bangbang93.com/maven");

                missFiles.Add(file);
            }
        }
        return missFiles;
    }

    internal static bool VerifyFileSha1(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath)) return false;
        using var stream = File.OpenRead(filePath);
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        byte[] hashBytes = sha1.ComputeHash(stream);
        string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        return actualHash.Trim().Equals(expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public class MissFileData
    {
        public string Name = string.Empty;
        public string Path = string.Empty;
        public string Url = string.Empty;
        public string Sha1 = string.Empty;
    }
}
