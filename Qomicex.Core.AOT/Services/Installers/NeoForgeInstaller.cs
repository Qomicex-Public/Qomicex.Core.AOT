using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

internal class NeoForgeInstaller : ForgeInstallerBase, IInstaller
{
    public NeoForgeInstaller(int sourceId, string gameDir, string gameVersion)
    {
        SourceId = sourceId;
        if (sourceId == 1)
        {
            BaseUrl = "https://bmclapi2.bangbang93.com/maven";
            SourceMappings =
            [
                new SourcesList { Original = "https://maven.neoforged.net/releases/net/neoforged/forge", Default = $"{BaseUrl}/net/neoforged/forge" },
                new SourcesList { Original = "https://maven.neoforged.net/releases/net/neoforged/neoforge", Default = $"{BaseUrl}/net/neoforged/neoforge" },
            ];
        }
        else
        {
            BaseUrl = "https://maven.neoforged.net/releases";
        }
        this.gameDir = gameDir;
        this.gameVersion = gameVersion;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? javaPath, string? neoForgeInstallerPath, string? para3, string? para4)
    {
        if (string.IsNullOrEmpty(javaPath))
            throw new ArgumentNullException(nameof(javaPath));
        if (string.IsNullOrEmpty(neoForgeInstallerPath))
            throw new ArgumentNullException(nameof(neoForgeInstallerPath));

        _installerPath = neoForgeInstallerPath;
        _mainJarPath = Path.Combine("versions", this.gameVersion, $"{this.gameVersion}.jar");
        await InstallNeoForge(versionId, inheritsFromJson, javaPath, neoForgeInstallerPath);
    }

    private async Task InstallNeoForge(string versionId, string inheritsFromJson, string javaPath, string neoForgeInstallerPath)
    {
        List<string> backFiles = [];
        List<string> backDirs = [];
        string jsonData;
        string installProfileData;
        byte[] clientLzma;

        try
        {
            jsonData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(neoForgeInstallerPath, "version.json"));
            installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(neoForgeInstallerPath, "install_profile.json"));
            clientLzma = ReadSpecifyFileFromZip(neoForgeInstallerPath, "data/client.lzma");
        }
        catch (Exception ex)
        {
            throw new Exception("读取NeoForge安装器内容失败，请检查安装器文件是否正确", ex);
        }

        var installProfileJson = JsonNode.Parse(installProfileData!)!.AsObject();
        string profileName = installProfileJson["profile"]?.ToString().ToLower() ?? string.Empty;
        if (profileName != "neoforge" && !(gameVersion == "1.20.1" && profileName == "forge"))
            throw new Exception("安装器版本不正确，请检查安装器文件是否正确");

        var versionData = JsonNode.Parse(jsonData!)!.AsObject();
        versionData["id"] = versionId;
        versionData["inheritsFrom"] = this.gameVersion;
        jsonData = versionData.ToJsonString();

        if (!string.IsNullOrEmpty(inheritsFromJson))
            jsonData = MergeVersionJson(inheritsFromJson, jsonData, versionId);

        var versionDir = Path.Combine(this.gameDir, "versions", versionId);
        if (!Directory.Exists(versionDir))
        {
            Directory.CreateDirectory(versionDir);
            backDirs.Add(versionDir);
        }
        string targetJsonPath = Path.Combine(versionDir, $"{versionId}.json");
        try
        {
            File.WriteAllText(targetJsonPath, jsonData);
        }
        catch (Exception ex)
        {
            BackInstall(backFiles, backDirs);
            throw new Exception($"写出NeoForge版本Json失败: {ex.Message}", ex);
        }
        backFiles.Add(targetJsonPath);

        var lzmaDir = Path.Combine(this.gameDir, "libraries", "net", "neoforged", "neoforge", versionId);
        if (!Directory.Exists(lzmaDir))
        {
            Directory.CreateDirectory(lzmaDir);
            backDirs.Add(lzmaDir);
        }
        string clientLzmaPath = Path.Combine(lzmaDir, "client.lzma");
        try
        {
            File.WriteAllBytes(clientLzmaPath, clientLzma);
        }
        catch (Exception ex)
        {
            BackInstall(backFiles, backDirs);
            throw new Exception($"写出NeoForge LZMA失败: {ex.Message}", ex);
        }
        backFiles.Add(clientLzmaPath);

        installProfileJson["data"]!["BINPATCH"]!["client"] = $"\"{clientLzmaPath}\"";

        var libs = GetMissNeoForgeLibraries(neoForgeInstallerPath, versionId);
        foreach (var lib in libs)
        {
            try
            {
                await DownloadFileAsync(CreateHttpClient(), lib.Url, lib.Path);
            }
            catch (Exception ex)
            {
                BackInstall(backFiles, backDirs);
                throw new Exception($"下载NeoForge缺失库失败: {lib.Path}\n{ex.Message}", ex);
            }
        }

        var processors = installProfileJson["processors"] as JsonArray;
        if (processors != null && processors.Count > 0)
        {
            foreach (var processor in processors)
            {
                var processorObject = processor!.AsObject();
                if (!ShouldRunProcessor(processorObject, "client")) continue;
                try
                {
                    await RunProcessor(installProfileJson, processorObject, versionId, this.gameDir, javaPath);
                }
                catch (Exception ex)
                {
                    BackInstall(backFiles, backDirs);
                    throw new Exception($"处理NeoForge处理器失败: {processorObject["jar"]}\n{ex.Message}", ex);
                }
            }
        }
    }

    private static void BackInstall(List<string> files, List<string> dirs)
    {
        foreach (var file in files)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
        var dirList = dirs.Distinct().OrderByDescending(d => d.Length).ToList();
        foreach (var dir in dirList)
        {
            try { if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir, false); } catch { }
        }
    }

    public Task<List<MissFileData>> GetMissLibrariesAsync(string? para1, string? para2, string? para3)
    {
        if (para1 == null) return Task.FromResult(new List<MissFileData>());
        return Task.FromResult(GetMissNeoForgeLibraries(para1, para2!));
    }

    public List<MissFileData> GetMissNeoForgeLibraries(string neoForgeInstallerPath, string versionId)
    {
        var versionData = string.Empty;
        var installProfileData = string.Empty;
        try
        {
            versionData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(neoForgeInstallerPath, "version.json"));
            installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(neoForgeInstallerPath, "install_profile.json"));
        }
        catch
        {
            throw new Exception("读取NeoForge安装器内容失败，请检查安装器文件是否正确");
        }

        var libs = GetLibrariesFromJson(installProfileData!);
        libs.AddRange(GetLibrariesFromJson(versionData!));
        foreach (var coordinate in ExtractMavenCoordinatesFromProcessors(JsonNode.Parse(installProfileData!)!.AsObject()))
        {
            libs.Add(new ForgeInstaller.LibInfo { FullName = coordinate });
        }
        libs = ForgeInstaller.CheckLibsVerStatic(libs);

        var missFiles = new List<MissFileData>();
        foreach (var lib in libs)
        {
            var libPath = Path.Combine(this.gameDir, "libraries", lib.Path);
            if (!File.Exists(libPath))
            {
                var url = string.Empty;
                if (!string.IsNullOrEmpty(lib.Url))
                    url = SourceId != 0 ? ResolveUrl(lib.Url) : lib.Url;
                else
                    url = $"{BaseUrl}/{lib.Path}";

                missFiles.Add(new MissFileData(
                    $"{lib.Name}-{lib.Version}.jar",
                    libPath,
                    url,
                    lib.Hash
                ));
            }
        }
        return missFiles;
    }

    internal static List<ForgeInstaller.LibInfo> GetLibrariesFromJson(string jsonData)
    {
        var libs = new List<ForgeInstaller.LibInfo>();
        var data = JsonNode.Parse(jsonData)!.AsObject();
        if (!data.TryGetPropertyValue("libraries", out var librariesToken) || librariesToken is not JsonArray libraries)
            throw new Exception("libraries字段不存在或格式错误");

        foreach (var item in libraries)
        {
            var libObj = item!.AsObject();
            if (libObj.ContainsKey("name"))
            {
                var name = libObj["name"]!.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    var info = new ForgeInstaller.LibInfo { FullName = name };
                    if (libObj.ContainsKey("downloads"))
                    {
                        var artifact = libObj["downloads"]?["artifact"];
                        info.Hash = artifact?["sha1"]?.ToString() ?? string.Empty;
                        info.Url = artifact?["url"]?.ToString() ?? string.Empty;
                    }
                    libs.Add(info);
                }
            }
        }
        return libs;
    }
}
