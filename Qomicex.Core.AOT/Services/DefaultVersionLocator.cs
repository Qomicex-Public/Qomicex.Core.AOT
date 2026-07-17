using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Utils;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services;

internal class DefaultVersionLocator : IVersionLocator
{
    private readonly string _versionsRootPath;
    private readonly string _gameRootPath;
    private readonly Dictionary<string, LocalVersionInfo> _versionCache = new();
    private readonly Dictionary<string, CompleteVersionMetadata> _metadataCache = new();
    private readonly VersionMetadataJsonContext _metadataCtx = VersionMetadataJsonContext.Default;
    private bool _isCacheDirty = true;
    private bool _isRefreshing;
    private DownloadSource _downloadSource = new();
    private readonly HttpClient _httpClient;
    private readonly AssetIndexDataJsonContext _assetIndexCtx = AssetIndexDataJsonContext.Default;

    private class DownloadSource
    {
        public string librariesSource = "https://libraries.minecraft.net/";
        public string mainJarSource = string.Empty;
        public string assetsIndexSource = string.Empty;
        public string assetsSource = "https://resources.download.minecraft.net/";
    }

    public DefaultVersionLocator(string gameRootPath, DownloadMirror? mirror = DownloadMirror.Official, HttpClient? httpClient = null)
    {
        _versionsRootPath = Path.Combine(gameRootPath, "versions");
        _gameRootPath = gameRootPath;
        _httpClient = httpClient ?? new HttpClient();
        Directory.CreateDirectory(_versionsRootPath);
        RefreshCache();
        if (mirror == DownloadMirror.BMCLAPI)
        {
            _downloadSource = new DownloadSource
            {
                librariesSource = "https://bmclapi2.bangbang93.com/maven/",
                mainJarSource = "https://bmclapi2.bangbang93.com/",
                assetsIndexSource = "https://bmclapi2.bangbang93.com/",
                assetsSource = "https://bmclapi2.bangbang93.com/assets/"
            };
        }
    }


    public List<LocalVersionInfo> GetAllVersions()
    {
        EnsureCacheFresh();
        return _versionCache.Values.ToList();
    }

    public CompleteVersionMetadata? GetVersionMetadata(string versionId)
    {
        if (string.IsNullOrEmpty(versionId))
            return null;

        EnsureCacheFresh();

        if (_metadataCache.TryGetValue(versionId, out var metadata))
            return metadata;

        var versionPath = GetVersionPath(versionId);
        var jsonPath = Path.Combine(versionPath, $"{versionId}.json");

        if (!File.Exists(jsonPath))
            return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            metadata = System.Text.Json.JsonSerializer.Deserialize(json, _metadataCtx.CompleteVersionMetadata);
            if (metadata != null)
                _metadataCache[versionId] = metadata;
            return metadata;
        }
        catch
        {
            return null;
        }
    }

    public bool IsVersionInstalled(string versionId)
    {
        EnsureCacheFresh();
        return _versionCache.ContainsKey(versionId);
    }

    public void RefreshCache()
    {
        EnsureCacheFresh();
    }

    public string GetVersionPath(string versionId)
    {
        return Path.Combine(_versionsRootPath, versionId);
    }

    private CompleteVersionMetadata GetMetaFromJson(string jsonData)
    {
        CompleteVersionMetadata? metadata;
        try
        {
            metadata = System.Text.Json.JsonSerializer.Deserialize(jsonData, _metadataCtx.CompleteVersionMetadata);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new ArgumentException("无效的版本 JSON 数据", nameof(jsonData), ex);
        }
        if (metadata == null)
            throw new ArgumentException("无效的版本 JSON 数据", nameof(jsonData));
        return metadata;
    }

    private List<Library> GetLibraries(CompleteVersionMetadata metadata)
    {
        var libList = new List<Library>();

        foreach (var lib in metadata.Libraries ?? [])
        {
            if (lib.Rules is not { Count: > 0 } || LibHelper.IsRulesSuitable(lib.Rules))
                libList.Add(lib);
        }

        if (!string.IsNullOrEmpty(metadata.InheritsFrom))
        {
            var parentJson = GetJsonData(metadata.InheritsFrom);
            if (parentJson != null)
            {
                try
                {
                    libList.AddRange(GetLibraries(GetMetaFromJson(parentJson)));
                }
                catch (ArgumentException)
                {
                }
            }
        }

        return LibHelper.CheckLibsVer(libList);
    }

    public Task<List<MissFileInfo>> GetMissLibrariesAsync(CompleteVersionMetadata meta)
    {
        var missFiles = new List<MissFileInfo>();
        foreach (var lib in GetLibraries(meta))
        {
            foreach (var item in GetLibraryCheckItems(lib))
            {
                var localPath = Path.Combine(_gameRootPath, "libraries", item.Path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(localPath) && (string.IsNullOrEmpty(item.Sha1) || FileHelper.ValidateFileHash(localPath, item.Sha1)))
                    continue;
                missFiles.Add(item with { Path = localPath });
            }
        }
        return Task.FromResult(missFiles);
    }

    public Task<List<MissFileInfo>> GetMissLibrariesAsync(string jsonData)
        => GetMissLibrariesAsync(GetMetaFromJson(jsonData));

    public Task<MissFileInfo?> GetMissMainJarAsync(CompleteVersionMetadata meta)
    {
        var client = meta.Downloads?.Client;
        if (client != null)
        {
            var jarPath = Path.Combine(_versionsRootPath, meta.Id, $"{meta.Id}.jar");
            if (File.Exists(jarPath) && FileHelper.ValidateFileHash(jarPath, client.Sha1))
                return Task.FromResult<MissFileInfo?>(null);

            return Task.FromResult<MissFileInfo?>(new MissFileInfo(
                $"{meta.Id}.jar",
                ReplaceMainJarUrl(client.Url),
                client.Sha1 ?? string.Empty,
                jarPath));
        }

        if (!string.IsNullOrEmpty(meta.InheritsFrom))
        {
            var parentMeta = GetVersionMetadata(meta.InheritsFrom);
            if (parentMeta != null)
                return GetMissMainJarAsync(parentMeta);
        }

        return Task.FromResult<MissFileInfo?>(null);
    }

    public Task<MissFileInfo?> GetMissMainJarAsync(string jsonData)
        => GetMissMainJarAsync(GetMetaFromJson(jsonData));

    public async Task<List<MissFileInfo>> GetMissAssetsAsync(CompleteVersionMetadata meta)
    {
        var assetIndex = meta.AssetIndex;
        if (assetIndex == null)
        {
            if (string.IsNullOrEmpty(meta.InheritsFrom))
                return new List<MissFileInfo>();
            var parentMeta = GetVersionMetadata(meta.InheritsFrom);
            if (parentMeta == null)
                return new List<MissFileInfo>();
            return await GetMissAssetsAsync(parentMeta);
        }

        var indexPath = Path.Combine(_gameRootPath, "assets", "indexes", $"{assetIndex.Id}.json");
        if (!File.Exists(indexPath) || !FileHelper.ValidateFileHash(indexPath, assetIndex.Sha1))
            await DownloadAssetIndexAsync(assetIndex, indexPath);

        var missFiles = new List<MissFileInfo>();
        var indexJson = await File.ReadAllTextAsync(indexPath);
        var indexData = System.Text.Json.JsonSerializer.Deserialize(indexJson, _assetIndexCtx.AssetIndexData);
        if (indexData?.Objects == null)
            return missFiles;

        foreach (var obj in indexData.Objects.Values)
        {
            var hash = obj.Hash;
            var localPath = Path.Combine(_gameRootPath, "assets", "objects", hash[..2], hash);
            if (File.Exists(localPath) && FileHelper.ValidateFileHash(localPath, hash))
                continue;
            var url = $"{_downloadSource.assetsSource}{hash[..2]}/{hash}".Replace("http://", "https://");
            missFiles.Add(new MissFileInfo(hash, url, hash, localPath));
        }
        return missFiles;
    }

    public Task<List<MissFileInfo>> GetMissAssetsAsync(string jsonData)
        => GetMissAssetsAsync(GetMetaFromJson(jsonData));

    private async Task DownloadAssetIndexAsync(AssetIndex assetIndex, string indexPath)
    {
        var url = assetIndex.Url;
        if (!string.IsNullOrEmpty(_downloadSource.assetsIndexSource))
        {
            url = url.Replace("https://piston-meta.mojang.com/", _downloadSource.assetsIndexSource)
                .Replace("https://launchermeta.mojang.com/", _downloadSource.assetsIndexSource)
                .Replace("https://launcher.mojang.com/", _downloadSource.assetsIndexSource)
                .Replace("http://", "https://");
        }

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"下载资源索引失败: {response.ReasonPhrase}");

        var content = await response.Content.ReadAsStringAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
        await File.WriteAllTextAsync(indexPath, content);
    }

    public async Task<List<MissFileInfo>> GetMissFilesAsync(CompleteVersionMetadata meta)
    {
        var missFiles = await GetMissLibrariesAsync(meta);
        var missMainJar = await GetMissMainJarAsync(meta);
        if (missMainJar != null)
            missFiles.Add(missMainJar);
        missFiles.AddRange(await GetMissAssetsAsync(meta));
        return missFiles;
    }

    public Task<List<MissFileInfo>> GetMissFilesAsync(string jsonData)
        => GetMissFilesAsync(GetMetaFromJson(jsonData));

    private string ReplaceMainJarUrl(string url)
    {
        if (string.IsNullOrEmpty(_downloadSource.mainJarSource))
            return url;
        return url.Replace("https://piston-meta.mojang.com/", _downloadSource.mainJarSource)
            .Replace("https://launchermeta.mojang.com/", _downloadSource.mainJarSource)
            .Replace("https://launcher.mojang.com/", _downloadSource.mainJarSource)
            .Replace("https://piston-data.mojang.com/", _downloadSource.mainJarSource);
    }

    private List<MissFileInfo> GetLibraryCheckItems(Library lib)
    {
        var items = new List<MissFileInfo>();

        var artifact = lib.Downloads?.Artifact;
        if (artifact != null)
        {
            items.Add(new MissFileInfo(lib.Name, ReplaceLibraryUrl(artifact.Url, artifact.Path), artifact.Sha1 ?? string.Empty, artifact.Path));
        }

        if (lib.Natives != null && lib.Downloads?.Classifiers != null)
        {
            var osName = SystemHelper.GetCurrentOsName();
            if (lib.Natives.TryGetValue(osName, out var nativeClassifier))
            {
                var classifierKey = nativeClassifier.Replace("${arch}", SystemHelper.GetCurrentArch());
                if (lib.Downloads.Classifiers.TryGetValue(classifierKey, out var nativeArtifact))
                    items.Add(new MissFileInfo($"{lib.Name}:{classifierKey}", ReplaceLibraryUrl(nativeArtifact.Url, nativeArtifact.Path), nativeArtifact.Sha1 ?? string.Empty, nativeArtifact.Path));
            }
        }

        if (lib.Downloads == null && !string.IsNullOrEmpty(lib.Name))
        {
            var path = LibHelper.MavenToPath(lib.Name);
            if (!string.IsNullOrEmpty(path))
                items.Add(new MissFileInfo(lib.Name, $"{_downloadSource.librariesSource}{path}", string.Empty, path));
        }

        return items;
    }

    private string ReplaceLibraryUrl(string? url, string path)
    {
        if (string.IsNullOrEmpty(url))
            return $"{_downloadSource.librariesSource}{path}";
        return url.Replace("https://libraries.minecraft.net/", _downloadSource.librariesSource);
    }

    private string? GetJsonData(string versionId)
    {
        var jsonPath = Path.Combine(_versionsRootPath, versionId, $"{versionId}.json");

        if (!File.Exists(jsonPath))
            return null;

        return File.ReadAllText(jsonPath);
    }

    private void EnsureCacheFresh()
    {
        if (!_isCacheDirty || _isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            _versionCache.Clear();
            _metadataCache.Clear();

            if (!Directory.Exists(_versionsRootPath))
                return;

            foreach (var versionDir in Directory.GetDirectories(_versionsRootPath))
            {
                var versionId = Path.GetFileName(versionDir);
                var jsonPath = Path.Combine(versionDir, $"{versionId}.json");

                if (!File.Exists(jsonPath))
                    continue;

                try
                {
                    var metadata = GetVersionMetadata(versionId);
                    if (metadata == null)
                        continue;

                    var isComplete = IsVersionComplete(versionId, metadata);
                    var totalSize = CalculateVersionSize(versionDir);

                    _versionCache[versionId] = new LocalVersionInfo(
                        Id: versionId,
                        Type: GetModloaderType(metadata),
                        ReleaseTime: metadata.ReleaseTime,
                        IsComplete: isComplete,
                        VersionPath: versionDir,
                        VanillaVersion: GetVanillaVersion(metadata,versionId),
                        TotalSize: totalSize
                    );
                }
                catch
                {
                    // ponytail: skip unparseable version dirs
                }
            }
        }
        finally
        {
            _isCacheDirty = false;
            _isRefreshing = false;
        }
    }
   
    internal string GetVanillaVersion(CompleteVersionMetadata meta,string versionId)
    {
        //从jar读版本
        var version = GameVersionHelper.FromJar(GetJarPath(versionId, meta));
        if (version != null)
            return version;

        //读json
        // --fml.mcVersion（新版Forge 1.13+ 写在 arguments.game 里）
        if (meta.Arguments is VersionArgumentsNew newArgs)
        {
            for (int i = 0; i < newArgs.Game.Count - 1; i++)
            {
                if (newArgs.Game[i] is ArgumentString s && s.Value == "--fml.mcVersion")
                {
                    if (newArgs.Game[i + 1] is ArgumentString next)
                        return next.Value;
                    break;
                }
            }
        }
        return "Unknown";
    }

    private List<ModloaderInfo> GetModloaderType(CompleteVersionMetadata meta)
    {
        var types = new List<ModloaderInfo>();
        bool isForgeFound = false;
        bool isNeoForgeFound = false;
        bool isFabricFound = false;
        bool isQuiltFound = false;
        bool isOptiFineFound = false;
        bool isLiteLoaderFound = false;

        if (meta != null) 
        {
            //检查libraries
            var libraries = meta.Libraries;
            foreach (var lib in libraries)
            {
                string name = lib.Name.ToLower();
                if (!string.IsNullOrEmpty(name))
                {
                    //识别OptiFine
                    if (name.Contains("optifine"))
                    {
                        var nameParts = name.Split(":");
                        if (nameParts.Length == 3)
                        {
                            if (nameParts[1] == "optifine")
                            {
                                isOptiFineFound = true;
                                string ver = string.Empty;
                                if (nameParts[2].Contains('-'))
                                {
                                    var verParts = nameParts[2].Split('-');
                                    if (verParts.Length == 2)
                                    {
                                        ver = verParts[1];
                                    }
                                    else
                                        ver = nameParts[2];
                                }
                                else
                                    ver = nameParts[2];
                                types.Add(new ModloaderInfo(ModloaderType.OptiFine, ver));
                            }
                        }
                    }
                    //识别LiteLoader
                    if (name.Contains("liteloader"))
                    {
                        var nameParts = name.Split(':');
                        if (nameParts.Length == 3)
                        {
                            if (nameParts[1] == "liteloader")
                            {
                                isLiteLoaderFound = true;
                                string ver = string.Empty;
                                if (nameParts[2].Contains('-'))
                                {
                                    var verParts = nameParts[2].Split('-');
                                    if (verParts.Length == 2)
                                    {
                                        ver = verParts[1];
                                    }
                                    else
                                        ver = nameParts[2];
                                }
                                else
                                    ver = nameParts[2];
                                types.Add(new ModloaderInfo(ModloaderType.LiteLoader, ver));
                            }
                        }
                    }
                    //识别旧版本Forge
                    if (name.Contains("forge"))
                    {
                        var nameParts = name.Split(':');
                        if (nameParts.Length == 3)
                        {
                            if (nameParts[1] == "forge")
                            {
                                isForgeFound = true;
                                string ver = string.Empty;
                                if (nameParts[2].Contains('-'))
                                {
                                    var verParts = nameParts[2].Split('-');
                                    if (verParts.Length == 2)
                                    {
                                        ver = verParts[1];
                                    }
                                    else
                                        ver = nameParts[2];
                                }
                                else
                                    ver = nameParts[2];
                                //types.Add($"Forge {ver}");
                                types.Add(new ModloaderInfo(ModloaderType.Forge, ver));
                            }
                        }
                    }
                    //识别新版本Forge
                    if (name.Contains("minecraftforge"))
                    {
                        var nameParts = name.Split(':');
                        if (nameParts.Length == 3)
                        {
                            if (nameParts[1] == "fmlloader")
                            {
                                isForgeFound = true;
                                string ver = string.Empty;
                                if (nameParts[2].Contains('-'))
                                {
                                    var verParts = nameParts[2].Split('-');
                                    if (verParts.Length == 2)
                                    {
                                        ver = verParts[1];
                                    }
                                    else
                                        ver = nameParts[2];
                                }
                                else
                                    ver = nameParts[2];
                                //types.Add($"Forge {ver}");
                                types.Add(new ModloaderInfo(ModloaderType.Forge, ver));
                            }
                        }
                    }
                    //识别Fabric
                    if (name.Contains("fabric"))
                    {
                        var nameParts = name.Split(':');
                        if (nameParts.Length == 3)
                        {
                            if (nameParts[1] == "fabric" || nameParts[1] == "fabric-loader")
                            {
                                isFabricFound = true;
                                types.Add(new ModloaderInfo(ModloaderType.Fabric, nameParts[2]));
                            }
                        }
                    }
                    //识别Quilt
                    if (name.Contains("quilt"))
                    {
                        var nameParts = name.Split(':');
                        if (nameParts.Length == 3)
                        {
                            if (nameParts[1] == "quilt" || nameParts[1] == "quilt-loader")
                            {
                                isQuiltFound = true;
                                //types.Add($"Quilt {nameParts[2]}");
                                types.Add(new ModloaderInfo(ModloaderType.Quilt, nameParts[2]));
                            }
                        }
                    }

                }
            }
            //检查arguments
            var arguments = meta.Arguments;
            if (arguments != null && arguments is VersionArgumentsNew newArgs) 
            {
                bool canGetVersion = false;
                foreach (var arg in newArgs.Game)
                {
                    string value = string.Empty;
                    if (arg is ArgumentString argStr)
                        value = argStr.Value;
                    if (value == "--fml.neoForgeVersion")
                    {
                        // 获取下一个元素作为版本号
                        canGetVersion = true;
                    }
                    if (value == "--fml.forgeVersion")
                    {
                        canGetVersion = true;
                    }
                    if(canGetVersion)
                    {
                        if (!string.IsNullOrEmpty(value))
                        {
                            types.Add(new ModloaderInfo(ModloaderType.NeoForge,value));
                            isNeoForgeFound = true;
                        }
                        else
                        {
                            //types.Add("NeoForge");
                            types.Add(new ModloaderInfo(ModloaderType.NeoForge, "Unknown"));
                            isNeoForgeFound = true;
                        }
                        break;
                    }
                }
            }
            //检查mainClass
            string mainClass = meta.MainClass.ToLower();
            if (mainClass == "net.minecraft.client.main.main")
            {
                return new List<ModloaderInfo> { new ModloaderInfo(ModloaderType.Vanilla,"")};
            }
            if (!isQuiltFound && mainClass == "org.quiltmc.loader.impl.launch.knot.knotclient")
            {
                isQuiltFound = true;
                //types.Add("Quilt");
                types.Add(new ModloaderInfo(ModloaderType.Quilt, "Unknown"));
            }
            if (!(isNeoForgeFound || isForgeFound) && mainClass == "cpw.mods.bootstraplauncher.bootstraplauncher")
            {
                isNeoForgeFound = true;
                //types.Add("NeoForge");
                types.Add(new ModloaderInfo(ModloaderType.NeoForge, "Unknown"));
            }
            if (!isFabricFound && mainClass == "net.fabricmc.loader.impl.launch.knot.knotclient")
            {
                isFabricFound = true;
                //types.Add("Fabric");
                types.Add(new ModloaderInfo(ModloaderType.Fabric, "Unknown"));
            }
            if (!isForgeFound && mainClass == "net.minecraftforge.bootstrap.bootstraplauncher")
            {
                isForgeFound = true;
                //types.Add("Forge");
                types.Add(new ModloaderInfo(ModloaderType.Forge, "Unknown"));
            }

            if (!(isOptiFineFound || isForgeFound || isNeoForgeFound || isLiteLoaderFound || isFabricFound || isQuiltFound))
            {
                if (mainClass == "net.minecraft.launchwrapper.Launch")
                {
                    return new List<ModloaderInfo> { new ModloaderInfo(ModloaderType.Vanilla, "") };
                }
            }
            if (types.Count == 0)
            {
                return new List<ModloaderInfo> { new ModloaderInfo(ModloaderType.Unknown, "Unknown") };
            }
        }

        return types; 
    }

    private string GetJarPath(string versionId, CompleteVersionMetadata metadata)
    {
        bool isVersionComplete = false;
        var clientPath = Path.Combine(GetVersionPath(versionId), $"{versionId}.jar");
        isVersionComplete = File.Exists(clientPath);
        if (!isVersionComplete)
        {
            if (string.IsNullOrEmpty(metadata.InheritsFrom))
                return "";
            clientPath = Path.Combine(GetVersionPath(metadata.InheritsFrom), $"{metadata.InheritsFrom}.jar");
        }
        return clientPath;
    }

    private bool IsVersionComplete(string versionId, CompleteVersionMetadata metadata)
    {
        bool isVersionComplete = false;
        isVersionComplete = File.Exists(GetJarPath(versionId,metadata));
        return isVersionComplete;
    }

    private static long CalculateVersionSize(string versionPath)
    {
        try
        {
            return new DirectoryInfo(versionPath)
                .GetFiles("*", SearchOption.AllDirectories)
                .Sum(f => f.Length);
        }
        catch
        {
            return 0;
        }
    }
}
