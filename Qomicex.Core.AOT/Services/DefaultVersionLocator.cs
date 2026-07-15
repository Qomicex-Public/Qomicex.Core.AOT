using Qomicex.Core.AOT.Interfaces.Core;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Utils;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services;

internal class DefaultVersionLocator : IVersionLocator
{
    private readonly string _versionsRootPath;
    private readonly Dictionary<string, LocalVersionInfo> _versionCache = new();
    private readonly Dictionary<string, CompleteVersionMetadata> _metadataCache = new();
    private readonly VersionMetadataJsonContext _metadataCtx = VersionMetadataJsonContext.Default;
    private bool _isCacheDirty = true;
    private bool _isRefreshing;

    public DefaultVersionLocator(string gameRootPath)
    {
        _versionsRootPath = Path.Combine(gameRootPath, "versions");
        Directory.CreateDirectory(_versionsRootPath);
        RefreshCache();
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
   
    private string GetVanillaVersion(CompleteVersionMetadata meta,string versionId)
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
