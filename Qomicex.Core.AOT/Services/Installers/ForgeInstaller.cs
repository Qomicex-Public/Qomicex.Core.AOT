using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Services.Expansion.Local;

namespace Qomicex.Core.AOT.Services.Installers;

internal class ForgeInstaller : ForgeInstallerBase, IInstaller
{
    public ForgeInstaller(int sourceId, string gameDir, string gameVersion)
    {
        SourceId = sourceId;
        if (sourceId == 1)
        {
            BaseUrl = "https://bmclapi2.bangbang93.com/maven";
            SourceMappings =
            [
                new SourcesList { Original = "https://maven.minecraftforge.net", Default = BaseUrl },
                new SourcesList { Original = "https://files.minecraftforge.net/maven", Default = BaseUrl },
                new SourcesList { Original = "https://libraries.minecraft.net", Default = BaseUrl },
            ];
        }
        else
        {
            BaseUrl = "https://maven.minecraftforge.net";
        }
        this.gameDir = gameDir;
        this.gameVersion = gameVersion;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? javaPath, string? forgeInstallerPath, string? para3, string? para4)
    {
        if (string.IsNullOrEmpty(javaPath))
            throw new ArgumentNullException(nameof(javaPath));
        if (string.IsNullOrEmpty(forgeInstallerPath))
            throw new ArgumentNullException(nameof(forgeInstallerPath));

        _installerPath = forgeInstallerPath;
        _mainJarPath = Path.Combine("versions", this.gameVersion, $"{this.gameVersion}.jar");
        if (IsLegacyForgeInstaller(forgeInstallerPath))
            await InstallLegacyForge(versionId, inheritsFromJson, javaPath, forgeInstallerPath);
        else
            await InstallForge(versionId, inheritsFromJson, javaPath, forgeInstallerPath);
    }

    private async Task InstallForge(string versionId, string inheritsFromJson, string javaPath, string forgeInstallerPath)
    {
        List<string> backFiles = [];
        List<string> backDirs = [];

        var jsonData = string.Empty;
        var installProfileData = string.Empty;
        byte[] clientLzma;
        try
        {
            jsonData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "version.json"));
            installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "install_profile.json"));
            clientLzma = ReadSpecifyFileFromZip(forgeInstallerPath, "data/client.lzma");
        }
        catch (Exception e)
        {
            throw new Exception("读取Forge安装器内容失败，请检查安装器文件是否正确", e);
        }

        var installProfileJson = JsonNode.Parse(installProfileData!)!.AsObject();

        string profileName = string.IsNullOrEmpty(installProfileJson["profile"]?.ToString())
            ? installProfileJson["install"]?["profileName"]?.ToString() ?? string.Empty
            : installProfileJson["profile"]?.ToString()!;
        if (profileName != "forge")
            throw new Exception("安装器版本不正确，请检查安装器文件是否正确");

        var versionData = JsonNode.Parse(jsonData!)!.AsObject();
        versionData["id"] = versionId;
        versionData["inheritsFrom"] = this.gameVersion;
        jsonData = versionData.ToString();

        if (!string.IsNullOrEmpty(inheritsFromJson))
            jsonData = MergeVersionJson(inheritsFromJson, jsonData, versionId);


        var versionDir = Path.Combine(this.gameDir, "versions", versionId);
        if (!Directory.Exists(versionDir))
        {
            Directory.CreateDirectory(versionDir);
            backDirs.Add(versionDir);
        }
        string targetJsonPath = Path.Combine(versionDir, $"{versionId}.json");
        File.WriteAllText(targetJsonPath, jsonData);
        backFiles.Add(targetJsonPath);

        var lzmaDir = Path.Combine(this.gameDir, "libraries", "net", "minecraftforge", "forge", $"{this.gameVersion}-{versionId}");
        if (!Directory.Exists(lzmaDir))
        {
            Directory.CreateDirectory(lzmaDir);
            backDirs.Add(lzmaDir);
        }

        string clientLzmaPath = Path.Combine(lzmaDir, "client.lzma");
        backFiles.Add(clientLzmaPath);
        try
        {
            File.WriteAllBytes(clientLzmaPath, clientLzma);
        }
        catch (Exception ex)
        {
            BackInstall(backFiles, backDirs);
            throw new Exception($"写出LZMA失败: {ex.Message}");
        }

        string binPatchPath = $"\"{Path.Combine(this.gameDir, "libraries", "net", "minecraftforge", "forge", $"{this.gameVersion}-{versionId}", "client.lzma")}\"";
        installProfileJson["data"]!["BINPATCH"]!["client"] = binPatchPath;

        var path = installProfileJson["path"]?.ToString()!;
        var jarMavenPath = string.Empty;
        if (!string.IsNullOrEmpty(path))
            jarMavenPath = MavenToPath(path);
        if (!string.IsNullOrEmpty(jarMavenPath))
        {
            var forgeJar = ReadSpecifyFileFromZip(forgeInstallerPath, $@"maven/{jarMavenPath}");
            var jarFullPath = Path.Combine(this.gameDir, "libraries", jarMavenPath);
            var jarDir = Path.GetDirectoryName(jarFullPath);
            if (!Directory.Exists(jarDir))
            {
                Directory.CreateDirectory(jarDir!);
                backDirs.Add(jarDir!);
            }
            backFiles.Add(jarFullPath);
            File.WriteAllBytes(jarFullPath, forgeJar);
        }

        var libs = GetMissForgeLibraries(forgeInstallerPath, versionId);
        foreach (var lib in libs)
        {
            try
            {
                await DownloadFileAsync(CreateHttpClient(), lib.Url, lib.Path);
            }
            catch (Exception e)
            {
                BackInstall(backFiles, backDirs);
                throw new Exception($"下载缺失的库文件失败: {lib.Path}\n{e.Message}");
            }
        }

        var processors = installProfileJson["processors"] as JsonArray;
        if (processors != null && processors.Count > 0)
        {
            Trace.WriteLine($"开始执行Processor后处理，共 {processors.Count} 个处理器");
            foreach (var processor in processors)
            {
                var processorObj = processor!.AsObject();

                string processorJar = processorObj["jar"]?.ToString() ?? "未知";
                Trace.WriteLine($"处理Processor: {processorJar}");

                if (!ShouldRunProcessor(processorObj, "client")) continue; Trace.WriteLine("该Processor不适用于当前side=client，跳过执行");
                try
                {
                    await RunProcessor(installProfileJson, processorObj, versionId, this.gameDir, javaPath);
                }
                catch (Exception ex)
                {
                    BackInstall(backFiles, backDirs);
                    throw new Exception($"处理器执行失败: {processorObj["jar"]}\n原因：{ex.Message}");
                }
            }
        }
    }

    private async Task InstallLegacyForge(string versionId, string inheritsFromJson, string javaPath, string forgeInstallerPath)
    {
        List<string> backFiles = [];
        List<string> backDirs = [];

        var jsonData = string.Empty;
        var installProfileData = string.Empty;
        try
        {
            installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "install_profile.json"));
            try { jsonData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "version.json")); } catch { }
        }
        catch
        {
            throw new Exception("读取Forge安装器内容失败，请检查安装器文件是否正确");
        }

        var installProfileJson = JsonNode.Parse(installProfileData!)!.AsObject();

        if (string.IsNullOrEmpty(jsonData))
            jsonData = installProfileJson["versionInfo"]?.ToString() ?? throw new Exception("无法找到版本Json信息");

        string profileName = string.IsNullOrEmpty(installProfileJson["profile"]?.ToString())
            ? installProfileJson["install"]?["profileName"]?.ToString() ?? string.Empty
            : installProfileJson["profile"]?.ToString()!;
        if (profileName != "forge")
            throw new Exception("安装器版本不正确，请检查安装器文件是否正确");

        var versionData = JsonNode.Parse(jsonData!)!.AsObject();
        versionData["id"] = versionId;
        versionData["inheritsFrom"] = this.gameVersion;
        jsonData = versionData.ToString();

        if (!string.IsNullOrEmpty(inheritsFromJson))
            jsonData = MergeVersionJson(inheritsFromJson, jsonData, versionId);

        var versionDir = Path.Combine(this.gameDir, "versions", versionId);
        if (!Directory.Exists(versionDir))
        {
            Directory.CreateDirectory(versionDir);
            backDirs.Add(versionDir);
        }
        string targetJsonPath = Path.Combine(versionDir, $"{versionId}.json");
        File.WriteAllText(targetJsonPath, jsonData);
        backFiles.Add(targetJsonPath);

        var jarMavenPath = MavenToPath(installProfileJson!["install"]?["path"]?.ToString()! ?? installProfileJson!["path"]?.ToString()!);
        var filePath = installProfileJson!["install"]?["filePath"]?.ToString() ?? $@"maven/{MavenToPath(installProfileJson!["path"]?.ToString()!)}";
        var forgeJar = ReadSpecifyFileFromZip(forgeInstallerPath, filePath!);
        var jarFullPath = Path.Combine(this.gameDir, "libraries", jarMavenPath);
        var jarDir = Path.GetDirectoryName(jarFullPath);
        if (!Directory.Exists(jarDir))
        {
            Directory.CreateDirectory(jarDir!);
            backDirs.Add(jarDir!);
        }
        backFiles.Add(jarFullPath);
        File.WriteAllBytes(jarFullPath, forgeJar);

        var libs = GetMissForgeLibraries(forgeInstallerPath, versionId);
        foreach (var lib in libs)
        {
            try
            {
                await DownloadFileAsync(CreateHttpClient(), lib.Url, lib.Path);
            }
            catch (Exception e)
            {
                BackInstall(backFiles, backDirs);
                throw new Exception($"下载缺失的库文件失败: {lib.Path}\n{e.Message}");
            }
        }
    }

    public bool IsLegacyForgeInstaller(string forgeInstallerPath)
    {
        var installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "install_profile.json"));
        var installProfileJson = JsonNode.Parse(installProfileData)!.AsObject();
        string profileName = string.IsNullOrEmpty(installProfileJson["profile"]?.ToString())
            ? installProfileJson["install"]?["profileName"]?.ToString() ?? string.Empty
            : installProfileJson["profile"]?.ToString()!;
        if (profileName != "forge")
            throw new Exception("安装器版本不正确");
        bool hasProcessors = installProfileJson.ContainsKey("processors") && installProfileJson["processors"]!.AsArray().Count > 0;
        return !hasProcessors;
    }

    private static void BackInstall(List<string> files, List<string> dirs)
    {
        foreach (var file in files)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
        foreach (var dir in dirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    public Task<List<MissFileData>> GetMissLibrariesAsync(string? para1, string? para2, string? para3)
    {
        if (para1 == null) return Task.FromResult(new List<MissFileData>());
        return Task.FromResult(GetMissForgeLibraries(para1, para2!));
    }

    public List<MissFileData> GetMissForgeLibraries(string forgeInstallerPath, string versionId)
    {
        var versionData = string.Empty;
        var installProfileData = string.Empty;
        installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "install_profile.json"));
        try { versionData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(forgeInstallerPath, "version.json")); } catch { }

        var installProfileJson = JsonNode.Parse(installProfileData)!.AsObject();
        var libs = new List<LibInfo>();
        var profileLibraries = installProfileJson.ContainsKey("libraries")
            ? installProfileJson["libraries"] as JsonArray
            : installProfileJson["versionInfo"]?["libraries"] as JsonArray;

        foreach (var lib in profileLibraries!)
        {
            var libObj = lib!.AsObject();
            if (libObj.ContainsKey("clientreq") && libObj["clientreq"]?.ToString() == "false")
                continue;
            var libInfo = new LibInfo { FullName = libObj["name"]?.ToString() ?? string.Empty };
            var libPath = Path.Combine(this.gameDir, "libraries", libInfo.Path);
            if (File.Exists(libPath))
            {
                if (!string.IsNullOrEmpty(libInfo.Hash) && VerifyFileSha1(libPath, libInfo.Hash))
                    continue;
            }
            libs.Add(libInfo);
        }

        libs = CheckLibsVerStatic(libs);

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

    public class LibInfo
    {
        private string _fullName = string.Empty;
        private string _name = string.Empty;
        private string _path = string.Empty;
        private string _version = string.Empty;

        public string FullName
        {
            get => _fullName;
            set
            {
                _fullName = value ?? string.Empty;
                if (string.IsNullOrEmpty(_fullName)) return;
                string[] temp = _fullName.Split(':');
                if (temp.Length >= 3)
                {
                    _version = temp[2];
                    _name = $"{temp[0]}.{temp[1]}";
                    _path = InstallerBase.MavenToPath(_fullName);
                }
            }
        }

        public string Name => _name;
        public string Path => _path;
        public string Version => _version;
        public string Hash = string.Empty;
        public string Url = string.Empty;
    }
    internal static List<LibInfo> CheckLibsVerStatic(List<LibInfo> libs)
    {
        return libs
            .GroupBy(lib => lib.Name)
            .Select(group =>
            {
                LibInfo newest = group.First();
                foreach (var lib in group.Skip(1))
                {
                    if (string.Compare(lib.Version, newest.Version, StringComparison.Ordinal) > 0)
                        newest = lib;
                }
                return newest;
            })
            .ToList();
    }
}
