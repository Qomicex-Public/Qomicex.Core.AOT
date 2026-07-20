using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

internal class LiteloaderInstaller : InstallerBase, IInstaller
{
    private string _baseRepoUrl;
    private readonly int _sourceId;
    private readonly string _gameDir;
    private readonly string _gameVersion;

    public LiteloaderInstaller(int sourceId, string gameDir, string gameVersion)
    {
        _sourceId = sourceId;
        _gameDir = gameDir;
        _gameVersion = gameVersion;
        _baseRepoUrl = sourceId == 1 ? "https://bmclapi2.bangbang93.com/maven/" : "https://dl.liteloader.com/versions";
    }

    public async Task InstallAsync(string versionId, string inheritsFromJson, string? modLoaderVersion, string? gameVersion, string? para3, string? para4)
    {
        if (string.IsNullOrEmpty(modLoaderVersion))
            throw new ArgumentNullException(nameof(modLoaderVersion));
        if (string.IsNullOrEmpty(gameVersion))
            throw new ArgumentNullException(nameof(gameVersion));

        bool installResult = await InstallLiteLoaderCoreAsync(versionId, gameVersion, modLoaderVersion);
        if (!installResult)
            throw new Exception($"LiteLoader安装失败 - 版本ID: {versionId}");
    }

    private async Task<bool> InstallLiteLoaderCoreAsync(string versionId, string mcVersion, string liteVersion)
    {
        var remoteVersion = await GetRemoteVersionByVersionsAsync(mcVersion, liteVersion);
        if (remoteVersion == null)
            throw new Exception($"无法获取LiteLoader {liteVersion}（对应MC {mcVersion}）的版本信息");

        var baseVersion = GetBaseMcVersion(mcVersion);
        if (baseVersion == null)
            throw new Exception($"未找到MC {mcVersion}的基础版本配置");

        var coreLibrary = CreateCoreLibrary(remoteVersion);
        if (coreLibrary.DownloadInfo == null || string.IsNullOrEmpty(coreLibrary.DownloadInfo.Path))
            throw new Exception("核心库信息构建异常");

        var mergedLibraries = MergeLibraries(remoteVersion.Libraries, coreLibrary);

        foreach (var lib in mergedLibraries)
        {
            if (lib.DownloadInfo?.Path == null || lib.DownloadInfo.Url == null || lib.Artifact == null)
                continue;

            string localPath = Path.Combine(_gameDir, "libraries", lib.DownloadInfo.Path);
            if (File.Exists(localPath)) continue;

            string directory = Path.GetDirectoryName(localPath)!;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            await DownloadFileAsync(CreateHttpClient(), lib.DownloadInfo.Url, localPath);
        }

        var versionJson = BuildVersionJson(versionId, baseVersion, remoteVersion, mergedLibraries);
        if (string.IsNullOrEmpty(versionJson))
            throw new Exception("构建版本配置失败");

        SaveVersionJson(versionId, _gameDir, versionJson);
        return true;
    }

    public Task<List<MissFileData>> GetMissLibrariesAsync(string? para1, string? para2, string? para3)
        => Task.FromResult(new List<MissFileData>());

    private async Task<LiteLoaderRemoteVersion?> GetRemoteVersionByVersionsAsync(string mcVersion, string liteVersion)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        string jsonUrl;
        if (_sourceId == 0)
            jsonUrl = "https://dl.liteloader.com/versions/versions.json";
        else
            jsonUrl = "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json";

        string jsonContent;
        try
        {
            jsonContent = await client.GetStringAsync(jsonUrl);
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(jsonContent)) return null;

        var root = JsonNode.Parse(jsonContent)!.AsObject();
        if (root["versions"] is not JsonObject versionsObj) return null;
        if (!versionsObj.TryGetPropertyValue(mcVersion, out var mcVersionNode) || mcVersionNode is not JsonObject mcObj)
            return null;

        JsonObject? liteLoaderVersions = null;
        foreach (string nodeName in new[] { "snapshots", "artefacts" })
        {
            if (!mcObj.TryGetPropertyValue(nodeName, out var node) || node is not JsonObject nodeObj)
                continue;
            if (nodeObj.TryGetPropertyValue("com.mumfrey:liteloader", out var liteNode) && liteNode is JsonObject liteObj)
            {
                liteLoaderVersions = liteObj;
                break;
            }
        }

        if (liteLoaderVersions == null) return null;

        JsonObject? targetVersion = null;
        foreach (var prop in liteLoaderVersions)
        {
            if (prop.Value is not JsonObject versionObj) continue;
            string? version = versionObj["version"]?.GetValue<string>();
            if (string.Equals(version, liteVersion, StringComparison.Ordinal))
            {
                targetVersion = versionObj;
                break;
            }
        }

        if (targetVersion == null) return null;

        string? fileName = targetVersion["file"]?.GetValue<string>();
        if (string.IsNullOrEmpty(fileName)) return null;

        string mainUrl = $"{_baseRepoUrl.TrimEnd('/')}/com/mumfrey/liteloader/{liteVersion}/{fileName}";

        var libraries = new List<Library>();
        if (targetVersion.TryGetPropertyValue("libraries", out var libsNode) && libsNode is JsonArray libsArray)
        {
            foreach (var libItem in libsArray)
            {
                if (libItem is not JsonObject libObj) continue;
                string? libName = libObj["name"]?.GetValue<string>();
                if (string.IsNullOrEmpty(libName)) continue;

                string mavenPath = MavenToPath(libName);
                if (string.IsNullOrEmpty(mavenPath)) continue;

                string libUrl = libObj["url"]?.GetValue<string>() ?? _baseRepoUrl;
                if (_sourceId == 0 && string.IsNullOrEmpty(libObj["url"]?.GetValue<string>()))
                    libUrl = "https://repo.spongepowered.org/maven";

                libraries.Add(new Library
                {
                    Artifact = ParseMavenCoordinate(libName),
                    Url = libUrl,
                    DownloadInfo = new LibraryDownloadInfo
                    {
                        Path = mavenPath,
                        Url = $"{libUrl.TrimEnd('/')}/{mavenPath}",
                    },
                });
            }
        }

        string tweakClass = targetVersion["tweakClass"]?.GetValue<string>() ?? "com.mumfrey.liteloader.launch.LiteLoaderTweaker";

        return new LiteLoaderRemoteVersion
        {
            GameVersion = mcVersion,
            SelfVersion = liteVersion,
            Urls = [mainUrl],
            TweakClass = tweakClass,
            Libraries = libraries,
        };
    }

    private static Artifact? ParseMavenCoordinate(string maven)
    {
        if (string.IsNullOrWhiteSpace(maven)) return null;
        string[] parts = maven.Split(':');
        if (parts.Length < 3) return null;
        string group = parts[0].Trim();
        string artifact = parts[1].Trim();
        string version = parts[2].Trim();
        if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(artifact) || string.IsNullOrEmpty(version))
            return null;
        return new Artifact { GroupId = group, ArtifactId = artifact, Version = version };
    }

    private LiteLoaderRemoteVersion? GetBaseMcVersion(string mcVersion)
    {
        var localVersionPath = Path.Combine(_gameDir, "versions", mcVersion, $"{mcVersion}.json");
        if (!File.Exists(localVersionPath)) return null;
        try
        {
            var jsonContent = File.ReadAllText(localVersionPath);
            return new LiteLoaderRemoteVersion { GameVersion = mcVersion, Json = jsonContent };
        }
        catch { return null; }
    }

    private Library CreateCoreLibrary(LiteLoaderRemoteVersion remoteVersion)
    {
        string liteVersion = remoteVersion.SelfVersion;
        string mavenCoord = $"com.mumfrey:liteloader:{liteVersion}";
        string mavenPath = MavenToPath(mavenCoord);
        if (string.IsNullOrEmpty(mavenPath))
            mavenPath = $"com/mumfrey/liteloader/{liteVersion}/liteloader-{liteVersion}.jar";

        string downloadUrl = remoteVersion.Urls.FirstOrDefault() ?? $"{_baseRepoUrl.TrimEnd('/')}/{mavenPath}";

        return new Library
        {
            Artifact = new Artifact { GroupId = "com.mumfrey", ArtifactId = "liteloader", Version = liteVersion },
            Url = _baseRepoUrl,
            DownloadInfo = new LibraryDownloadInfo
            {
                Path = mavenPath,
                Url = downloadUrl,
            },
        };
    }

    private static List<Library> MergeLibraries(List<Library> baseLibraries, Library coreLibrary)
    {
        if (coreLibrary.Artifact == null) return [.. baseLibraries, coreLibrary];
        string coreLibCoord = coreLibrary.Artifact.ToString();
        bool coreLibExists = baseLibraries.Any(lib => lib.Artifact?.ToString() == coreLibCoord);
        return coreLibExists ? [.. baseLibraries] : [.. baseLibraries, coreLibrary];
    }

    private string BuildVersionJson(string versionId, LiteLoaderRemoteVersion baseVersion, LiteLoaderRemoteVersion remoteVersion, List<Library> libraries)
    {
        var liteJson = new JsonObject
        {
            ["id"] = versionId,
            ["inheritsFrom"] = baseVersion.GameVersion,
            ["type"] = "release",
            ["arguments"] = new JsonObject
            {
                ["game"] = new JsonArray("--tweakClass", remoteVersion.TweakClass ?? "com.mumfrey.liteloader.launch.LiteLoaderTweaker"),
            },
            ["mainClass"] = "net.minecraft.launchwrapper.Launch",
            ["libraries"] = new JsonArray(libraries.Select(lib =>
            {
                var obj = new JsonObject
                {
                    ["name"] = lib.Artifact?.ToString(),
                };
                if (!string.IsNullOrEmpty(lib.Url))
                    obj["url"] = lib.Url;
                if (lib.DownloadInfo != null)
                {
                    obj["downloads"] = new JsonObject
                    {
                        ["artifact"] = new JsonObject
                        {
                            ["path"] = lib.DownloadInfo.Path,
                            ["url"] = lib.DownloadInfo.Url,
                        },
                    };
                }
                return obj;
            }).ToArray()),
            ["logging"] = new JsonObject(),
        };

        string liteJsonStr = liteJson.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        string mergedJson = MergeVersionJson(baseVersion.Json ?? "{}", liteJsonStr, versionId);
        return mergedJson;
    }

    private static void SaveVersionJson(string versionId, string gameDir, string jsonContent)
    {
        var versionDir = Path.Combine(gameDir, "versions", versionId);
        var jsonPath = Path.Combine(versionDir, $"{versionId}.json");
        if (!Directory.Exists(versionDir)) Directory.CreateDirectory(versionDir);
        File.WriteAllText(jsonPath, jsonContent);
    }

    public class Library
    {
        public Artifact? Artifact { get; set; }
        public string? Url { get; set; }
        public LibraryDownloadInfo? DownloadInfo { get; set; }
    }

    public class Artifact
    {
        public string GroupId { get; set; } = string.Empty;
        public string ArtifactId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public override string ToString() => $"{GroupId}:{ArtifactId}:{Version}";
    }

    public class LibraryDownloadInfo
    {
        public string? Path { get; set; }
        public string? Url { get; set; }
    }

    public class LiteLoaderRemoteVersion
    {
        public string GameVersion { get; set; } = string.Empty;
        public string SelfVersion { get; set; } = string.Empty;
        public List<string> Urls { get; set; } = [];
        public string TweakClass { get; set; } = string.Empty;
        public List<Library> Libraries { get; set; } = [];
        public string? Json { get; set; }
    }
}
