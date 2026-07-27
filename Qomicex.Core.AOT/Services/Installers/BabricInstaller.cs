using System.Diagnostics;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Utils;

namespace Qomicex.Core.AOT.Services.Installers;

internal class BabricInstaller : InstallerBase, IInstaller
{
    private readonly string _gameDir;

    public BabricInstaller(int downloadSource, string gameDir)
    {
        _gameDir = gameDir;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? babricVersion, string? gameVersion, string? para3, string? para4)
    {
        if (babricVersion == null) throw new ArgumentNullException(nameof(babricVersion));
        if (gameVersion == null) throw new ArgumentNullException(nameof(gameVersion));
        await InstallBabricAsync(versionId, babricVersion, gameVersion, inheritsFromJson);
    }

    public async Task<bool> InstallBabricAsync(string versionId, string babricVersion, string gameVersion, string? inheritsFromJson = null)
    {
        Trace.WriteLine($"Babric 安装开始: versionId={versionId}, babricVersion={babricVersion}, gameVersion={gameVersion}");

        var jsonData = await BuildJson(versionId, babricVersion, gameVersion, _gameDir);
        if (string.IsNullOrEmpty(jsonData))
            throw new Exception("构建JSON数据失败");

        var versionDir = $"{_gameDir}/versions/{versionId}";
        if (!Directory.Exists(versionDir))
            Directory.CreateDirectory(versionDir);

        if (!string.IsNullOrEmpty(inheritsFromJson))
        {
            Trace.WriteLine("合并版本 JSON...");
            jsonData = MergeVersionJson(inheritsFromJson, jsonData, versionId);
        }
        else
            throw new Exception("主版本JSON文件不存在");

        var jsonPath = Path.Combine(_gameDir, "versions", versionId, $"{versionId}.json");
        await File.WriteAllTextAsync(jsonPath, jsonData);
        Trace.WriteLine($"版本 JSON 已写入: {jsonPath}");
        Trace.WriteLine($"Babric 安装完成: {versionId}");
        return true;
    }

    private async Task<string> BuildJson(string versionId, string babricVersion, string gameVersion, string gameDir)
    {
        using var client = CreateHttpClient();
        var url = $"https://meta.babric.glass-launcher.net/v2/versions/loader/{gameVersion}/{babricVersion}/profile/json";
        Trace.WriteLine($"获取 Babric Meta: {url}");

        var result = await client.GetAsync(url);
        if (!result.IsSuccessStatusCode)
        {
            Trace.WriteLine($"Babric Meta 请求失败: {result.StatusCode}");
            throw new Exception("获取Launcher Meta失败");
        }

        var metaStr = await result.Content.ReadAsStringAsync();
        var meta = JsonNode.Parse(metaStr)!.AsObject();
        Trace.WriteLine("Babric Meta 获取成功");

        var libs = meta["libraries"] as JsonArray;
        if (libs != null)
        {
            Trace.WriteLine($"处理 {libs.Count} 个库文件...");
            var downloadCount = 0;
            var skipCount = 0;
            foreach (var lib in libs)
            {
                downloadCount++;
                var libName = lib!["name"]?.ToString() ?? "未知";
                var urlDomain = "https://meta.babric.glass-launcher.net/";
                if (!string.IsNullOrEmpty(lib!["url"]?.ToString()))
                    urlDomain = lib!["url"]?.ToString()!;

                var mavenName = GetMavenNameWithClassifier(lib);
                var destPath = $"{gameDir}/libraries/{MavenToPath(mavenName)}";
                if (File.Exists(destPath))
                {
                    skipCount++;
                    Trace.WriteLine($"[{downloadCount}/{libs.Count}] 已存在: {libName}");
                    continue;
                }
                Trace.WriteLine($"[{downloadCount}/{libs.Count}] 下载库: {libName}");
                await DownloadFileAsync(client, $"{urlDomain}{MavenToPath(mavenName)}", destPath);
                Trace.WriteLine($"[{downloadCount}/{libs.Count}] 完成: {libName}");
            }
            if (skipCount > 0)
                Trace.WriteLine($"跳过 {skipCount} 个已存在文件, 下载 {libs.Count - skipCount} 个");
            Trace.WriteLine("所有库文件处理完成");
        }
        else
            Trace.WriteLine("无库文件需要下载");

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
                Trace.WriteLine($"检测到原生库: {name}, 分类器: {classifier}");
                return $"{name}:{classifier}";
            }
        }
        return name;
    }

    public Task<List<MissFileData>> GetMissLibrariesAsync(string? para1, string? para2, string? para3)
    {
        if (para1 == null || para2 == null || para3 == null) return Task.FromResult(new List<MissFileData>());
        return GetMissBabricLibraries(para1, para2, para3);
    }

    public async Task<List<MissFileData>> GetMissBabricLibraries(string babricVersion, string gameVersion, string gameDir)
    {
        Trace.WriteLine($"Babric 缺失库检查: babricVersion={babricVersion}, gameVersion={gameVersion}");
        var missFiles = new List<MissFileData>();
        using var client = CreateHttpClient();
        var result = await client.GetAsync($"https://meta.babric.glass-launcher.net/v2/versions/loader/{gameVersion}/{babricVersion}/profile/json");
        if (!result.IsSuccessStatusCode)
        {
            Trace.WriteLine($"Babric Meta 请求失败: {result.StatusCode}");
            throw new Exception("获取Launcher Meta失败");
        }

        var metaStr = await result.Content.ReadAsStringAsync();
        var meta = JsonNode.Parse(metaStr)!.AsObject();

        var libs = meta["libraries"] as JsonArray;
        if (libs != null)
        {
            Trace.WriteLine($"检查 {libs.Count} 个库文件...");
            foreach (var lib in libs)
            {
                var urlDomain = "https://meta.babric.glass-launcher.net/";
                if (!string.IsNullOrEmpty(lib!["url"]?.ToString()))
                    urlDomain = lib!["url"]?.ToString()!;

                var mavenName = GetMavenNameWithClassifier(lib);
                var libPath = Path.Combine(gameDir, "libraries", MavenToPath(mavenName));
                if (File.Exists(libPath))
                {
                    if (!string.IsNullOrEmpty(lib["sha1"]?.ToString()) &&
                        FabricInstaller.VerifyFileSha1(libPath, lib["sha1"]?.ToString()!))
                    {
                        Trace.WriteLine($"  已存在 (SHA1匹配): {lib["name"]}");
                        continue;
                    }
                    else
                        Trace.WriteLine($"  SHA1不匹配，需重下: {lib["name"]}");
                }
                else
                    Trace.WriteLine($"  缺失: {lib["name"]}");

                missFiles.Add(new MissFileData(
                    lib["name"]?.ToString()!,
                    libPath,
                    $"{urlDomain}{MavenToPath(mavenName)}",
                    lib["sha1"]?.ToString() ?? ""
                ));
            }
        }
        Trace.WriteLine($"Babric 缺失库检查完成: {missFiles.Count} 个缺失");
        return missFiles;
    }
}
