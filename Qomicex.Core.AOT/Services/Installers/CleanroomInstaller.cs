using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

internal class CleanroomInstaller : InstallerBase, IInstaller
{
    private readonly int _sourceId;
    private readonly string _gameDir;

    public CleanroomInstaller(int sourceId, string gameDir)
    {
        _sourceId = sourceId;
        _gameDir = gameDir;
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? javaPath, string? installerPath, string? para3, string? para4)
    {
        if (string.IsNullOrEmpty(installerPath))
            throw new ArgumentNullException(nameof(installerPath));

        await InstallCleanroom(versionId, installerPath);
    }

    private async Task InstallCleanroom(string versionId, string installerPath)
    {
        List<string> backFiles = [];
        List<string> backDirs = [];

        string versionJsonData;
        string installProfileData;
        try
        {
            versionJsonData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(installerPath, "version.json"));
            installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(installerPath, "install_profile.json"));
        }
        catch (Exception ex)
        {
            throw new Exception("读取 Cleanroom 安装器内容失败，请检查安装器文件是否正确", ex);
        }

        var versionJson = JsonNode.Parse(versionJsonData)!.AsObject();
        var installProfileJson = JsonNode.Parse(installProfileData)!.AsObject();

        versionJson["id"] = versionId;

        var versionDir = Path.Combine(_gameDir, "versions", versionId);
        if (!Directory.Exists(versionDir))
        {
            Directory.CreateDirectory(versionDir);
            backDirs.Add(versionDir);
        }
        string targetJsonPath = Path.Combine(versionDir, $"{versionId}.json");
        try
        {
            File.WriteAllText(targetJsonPath, versionJson.ToJsonString());
        }
        catch (Exception ex)
        {
            BackInstall(backFiles, backDirs);
            throw new Exception($"写出 Cleanroom 版本 Json 失败: {ex.Message}", ex);
        }
        backFiles.Add(targetJsonPath);

        var cleanroomMavenCoord = installProfileJson["path"]?.ToString();
        if (!string.IsNullOrEmpty(cleanroomMavenCoord))
        {
            var jarRelPath = MavenToPath(cleanroomMavenCoord);
            if (!string.IsNullOrEmpty(jarRelPath))
            {
                var jarEntryPath = $"maven/{jarRelPath.Replace('\\', '/')}";
                try
                {
                    var jarBytes = ReadSpecifyFileFromZip(installerPath, jarEntryPath);
                    var jarFullPath = Path.Combine(_gameDir, "libraries", jarRelPath);
                    var jarDir = Path.GetDirectoryName(jarFullPath);
                    if (!string.IsNullOrEmpty(jarDir) && !Directory.Exists(jarDir))
                    {
                        Directory.CreateDirectory(jarDir);
                        backDirs.Add(jarDir);
                    }
                    File.WriteAllBytes(jarFullPath, jarBytes);
                    backFiles.Add(jarFullPath);
                }
                catch (Exception ex)
                {
                    BackInstall(backFiles, backDirs);
                    throw new Exception($"提取 Cleanroom 核心 Jar 失败: {ex.Message}", ex);
                }
            }
        }

        var libs = GetMissCleanroomLibraries(installerPath, versionId);
        foreach (var lib in libs)
        {
            try
            {
                await DownloadFileAsync(CreateHttpClient(), lib.Url, lib.Path);
            }
            catch (Exception ex)
            {
                BackInstall(backFiles, backDirs);
                throw new Exception($"下载 Cleanroom 缺失库失败: {lib.Path}\n{ex.Message}", ex);
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
        return Task.FromResult(GetMissCleanroomLibraries(para1, para2!));
    }

    public List<MissFileData> GetMissCleanroomLibraries(string installerPath, string versionId)
    {
        string versionJsonData;
        string installProfileData;
        try
        {
            versionJsonData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(installerPath, "version.json"));
            installProfileData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(installerPath, "install_profile.json"));
        }
        catch
        {
            throw new Exception("读取 Cleanroom 安装器内容失败，请检查安装器文件是否正确");
        }

        var allLibs = new List<ForgeInstaller.LibInfo>();
        allLibs.AddRange(GetLibInfosFromJson(installProfileData));
        allLibs.AddRange(GetLibInfosFromJson(versionJsonData));
        allLibs = ForgeInstaller.CheckLibsVerStatic(allLibs);

        var missFiles = new List<MissFileData>();
        foreach (var lib in allLibs)
        {
            var libPath = Path.Combine(_gameDir, "libraries", lib.Path);
            if (File.Exists(libPath)) continue;

            var url = lib.Url;
            if (string.IsNullOrEmpty(url))
            {
                if (_sourceId == 1)
                    url = $"https://bmclapi2.bangbang93.com/maven/{lib.Path}";
                else
                    url = $"https://repo.maven.apache.org/maven2/{lib.Path}";
            }

            missFiles.Add(new MissFileData(
                $"{lib.Name}-{lib.Version}.jar",
                libPath,
                url,
                lib.Hash
            ));
        }
        return missFiles;
    }

    internal static List<ForgeInstaller.LibInfo> GetLibInfosFromJson(string jsonData)
    {
        var libs = new List<ForgeInstaller.LibInfo>();
        var data = JsonNode.Parse(jsonData)!.AsObject();
        if (!data.TryGetPropertyValue("libraries", out var librariesToken) || librariesToken is not JsonArray libraries)
            return libs;

        foreach (var item in libraries)
        {
            var libObj = item!.AsObject();
            var name = libObj["name"]?.ToString();
            if (string.IsNullOrEmpty(name)) continue;

            var info = new ForgeInstaller.LibInfo { FullName = name };
            if (libObj.TryGetPropertyValue("downloads", out var downloadsToken) && downloadsToken is JsonObject downloads)
            {
                var artifact = downloads["artifact"] as JsonObject;
                if (artifact != null)
                {
                    info.Hash = artifact["sha1"]?.ToString() ?? string.Empty;
                    info.Url = artifact["url"]?.ToString() ?? string.Empty;
                }
            }
            libs.Add(info);
        }
        return libs;
    }
}
