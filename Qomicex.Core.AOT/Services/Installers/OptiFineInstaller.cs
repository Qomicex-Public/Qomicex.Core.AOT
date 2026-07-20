using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

public class OptiFineInstaller : InstallerBase, IInstaller
{
    private readonly string _downloadSource;
    private readonly string _gameDir;
    private readonly string _gameVersion;

    public OptiFineInstaller(int sourceId, string gameDir, string gameVersion)
    {
        _downloadSource = sourceId == 0 ? "https://optifine.net/download" : "https://bmclapi2.bangbang93.com/optifine";
        _gameDir = gameDir;
        _gameVersion = gameVersion;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? modLoaderVersion, string? installerFilePath, string? javaPath, string? para4)
    {
        if (string.IsNullOrEmpty(modLoaderVersion))
            throw new ArgumentNullException(nameof(modLoaderVersion));
        if (string.IsNullOrEmpty(installerFilePath))
            throw new ArgumentNullException(nameof(installerFilePath));
        if (string.IsNullOrEmpty(javaPath))
            throw new ArgumentNullException(nameof(javaPath));

        var ofInfoParts = modLoaderVersion.Split('-');
        if (ofInfoParts.Length != 2)
            throw new ArgumentException("modLoaderVersion格式错误，需为\"Type-Patch\"", nameof(modLoaderVersion));

        var version = new OptiFineVersionInfo
        {
            McVersion = _gameVersion,
            Type = ofInfoParts[0],
            Patch = ofInfoParts[1],
            FileName = installerFilePath,
        };

        await InstallCoreAsync(versionId, version, javaPath, inheritsFromJson);
    }

    private async Task InstallCoreAsync(string versionId, OptiFineVersionInfo version, string javaPath, string? inheritsFromJson)
    {
        var installerFile = await DownloadInstallerAsync(version);
        if (!installerFile.Exists)
            throw new FileNotFoundException("OptiFine安装包下载失败", installerFile.FullName);

        try
        {
            string optiVersionId = versionId;
            string versionDir = Path.Combine(_gameDir, "versions", optiVersionId);
            if (!Directory.Exists(versionDir)) Directory.CreateDirectory(versionDir);

            bool jsonCreated = await CreateVersionJsonAsync(version, optiVersionId, versionDir, inheritsFromJson);
            if (!jsonCreated)
                throw new Exception("版本配置文件创建失败");

            bool installSuccess = await RunInstallerAsync(installerFile.FullName, javaPath, versionDir, optiVersionId);
            if (!installSuccess)
                throw new Exception("OptiFine安装程序执行失败");
        }
        finally
        {
            CleanupTempFiles(installerFile.FullName);
        }
    }

    public async Task<List<OptiFineVersionInfo>> GetAvailableVersionsAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        string url = $"{_downloadSource}/{_gameVersion}";
        string json = await client.GetStringAsync(url);
        if (string.IsNullOrEmpty(json)) return [];

#pragma warning disable IL2026, IL3050
        var versions = JsonSerializer.Deserialize<List<OptiFineVersionInfo>>(json);
#pragma warning restore IL2026, IL3050
        if (versions != null)
            versions.Sort((a, b) => string.Compare(b.Patch, a.Patch, StringComparison.Ordinal));
        return versions ?? [];
    }

    private async Task<FileInfo> DownloadInstallerAsync(OptiFineVersionInfo version)
    {
        if (!string.IsNullOrEmpty(version.FileName) && File.Exists(version.FileName))
            return new FileInfo(version.FileName);

        string url = $"{_downloadSource}/{_gameVersion}/{version.Type}/{version.Patch}";
        string fileName = $"{_gameVersion}_{version.Type}_{version.Patch}.jar";
        string savePath = Path.Combine(_gameDir, "temp", fileName);

        string tempDir = Path.GetDirectoryName(savePath)!;
        if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

        if (File.Exists(savePath))
            return new FileInfo(savePath);

        await DownloadFileAsync(CreateHttpClient(), url, savePath);
        return new FileInfo(savePath);
    }

    private async Task<bool> CreateVersionJsonAsync(OptiFineVersionInfo optiVersion, string versionId, string versionDir, string? inheritsFromJson)
    {
        var baseJsonPath = Path.Combine(_gameDir, "versions", _gameVersion, $"{_gameVersion}.json");
        if (!File.Exists(baseJsonPath))
            throw new FileNotFoundException("基础Minecraft客户端JAR文件不存在", baseJsonPath);

        var baseJsonContent = await File.ReadAllTextAsync(baseJsonPath);
        var baseJson = JsonNode.Parse(baseJsonContent)!.AsObject();

        var newJson = new JsonObject
        {
            ["id"] = versionId,
            ["inheritsFrom"] = _gameVersion,
            ["type"] = "release",
            ["time"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssZ"),
            ["releaseTime"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssZ"),
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["minecraftArguments"] = "--tweakClass optifine.OptiFineTweaker",
            ["libraries"] = new JsonArray(),
        };

        if (baseJson["libraries"] is JsonArray baseLibraries)
        {
            foreach (var lib in baseLibraries)
                ((JsonArray)newJson["libraries"]!).Add((JsonNode)lib!);
        }

        ((JsonArray)newJson["libraries"]!).Add((JsonNode)new JsonObject
        {
            ["name"] = $"optifine:OptiFine:{_gameVersion}_{optiVersion.Type}_{optiVersion.Patch}",
        });

        ((JsonArray)newJson["libraries"]!).Add((JsonNode)new JsonObject
        {
            ["name"] = "net.minecraft:launchwrapper:1.12",
        });

        if (!string.IsNullOrEmpty(inheritsFromJson))
            newJson = JsonNode.Parse(MergeVersionJson(inheritsFromJson, newJson.ToString(), versionId))!.AsObject();

        string jsonPath = Path.Combine(versionDir, $"{versionId}.json");
        await File.WriteAllTextAsync(jsonPath, newJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        string sourceJar = Path.Combine(_gameDir, "versions", _gameVersion, $"{_gameVersion}.jar");
        string targetJar = Path.Combine(versionDir, $"{versionId}.jar");
        if (!File.Exists(sourceJar))
            throw new FileNotFoundException("基础Minecraft客户端JAR文件不存在", sourceJar);
        File.Copy(sourceJar, targetJar, true);

        return true;
    }

    private async Task<bool> RunInstallerAsync(string installerPath, string javaPath, string versionDir, string versionId)
    {
        string clientJarPath = Path.Combine(_gameDir, "versions", _gameVersion, $"{_gameVersion}.jar");
        string outputJarPath = Path.Combine(versionDir, $"{versionId}.jar");

        var parts = versionId.Split('_');
        string libPath = Path.Combine(_gameDir, "libraries", "optifine", "OptiFine",
            $"{_gameVersion}_{parts[1]}_{parts[2]}",
            $"OptiFine-{_gameVersion}_{parts[1]}_{parts[2]}.jar");

        string libDir = Path.GetDirectoryName(libPath)!;
        if (!Directory.Exists(libDir)) Directory.CreateDirectory(libDir);

        string arguments = $"-cp \"{installerPath}\" optifine.Patcher \"{clientJarPath}\" \"{installerPath}\" \"{libPath}\"";

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(javaPath)
        {
            Arguments = arguments,
            WorkingDirectory = _gameDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) Trace.WriteLine($"OptiFine安装输出: {e.Data}");
        };
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data)) Trace.WriteLine($"OptiFine安装错误: {e.Data}");
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static void CleanupTempFiles(string installerPath)
    {
        try { if (File.Exists(installerPath)) File.Delete(installerPath); } catch { }
    }

    public class OptiFineVersionInfo
    {
        public string? Type { get; set; }
        public string? Patch { get; set; }
        public string? FileName { get; set; }
        public string? McVersion { get; set; }
    }
}
