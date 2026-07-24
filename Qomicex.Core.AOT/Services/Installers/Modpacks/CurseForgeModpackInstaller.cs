using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers.Modpacks;

internal sealed class CurseForgeModpackInstaller : InstallerBase, IInstaller
{
    private readonly string _gameDir;
    private readonly bool _versionIsolation;
    private readonly string _modpackFilePath;

    internal CurseForgeModpackInstaller(string gameDir, bool versionIsolation, string modpackFilePath)
    {
        _gameDir = gameDir;
        _versionIsolation = versionIsolation;
        _modpackFilePath = modpackFilePath;
    }

    Task IInstaller.InstallAsync(string versionId, string inheritsFromJson, string? para1, string? para2, string? para3, string? para4)
    {
        ReleaseFiles(versionId);
        return Task.CompletedTask;
    }

    public CurseForgeModpackInfo GetModpackInfo()
    {
        var info = new CurseForgeModpackInfo();
        var jsonData = Encoding.UTF8.GetString(InstallerBase.ReadSpecifyFileFromZip(_modpackFilePath, "manifest.json"));
        var json = JsonNode.Parse(jsonData)!.AsObject();

        if ((string?)json["manifestType"] != "minecraftModpack")
            throw new InvalidOperationException("Only Minecraft modpacks are supported.");

        info.Name = (string?)json["name"] ?? string.Empty;
        info.Version = (string?)json["version"] ?? string.Empty;
        info.GameVersion = (string?)json["minecraft"]?["version"] ?? string.Empty;

        var loaders = json["minecraft"]?.AsObject()?["modLoaders"]?.AsArray();
        var loaderType = string.Empty;
        if (loaders != null)
        {
            foreach (var loader in loaders.OfType<JsonObject>())
            {
                if ((bool?)loader["primary"] == true)
                {
                    var rawId = (string?)loader["id"];
                    if (!string.IsNullOrEmpty(rawId))
                    {
                        var idx = rawId.IndexOf('-');
                        if (idx >= 0 && idx < rawId.Length - 1)
                        {
                            loaderType = rawId[..idx];
                            info.ModLoaderVersion = rawId[(idx + 1)..];
                        }
                    }
                    break;
                }
            }
        }
        info.ModLoader = loaderType switch
        {
            "quilt" => ModpackLoaderType.Quilt,
            "fabric" => ModpackLoaderType.Fabric,
            "forge" => ModpackLoaderType.Forge,
            "neoforge" => ModpackLoaderType.NeoForge,
            _ => ModpackLoaderType.Unknown
        };

        var filesArray = json["files"]?.AsArray();
        if (filesArray != null)
        {
            foreach (var file in filesArray.OfType<JsonObject>())
            {
                if ((bool?)file["required"] != true)
                    continue;

                var fileInfo = new CurseForgeModpackFileInfo
                {
                    ProjectId = (int?)file["projectID"] ?? 0,
                    FileId = (int?)file["fileID"] ?? 0,
                };
                info.Files.Add(fileInfo);
            }
        }

        return info;
    }

    private void ReleaseFiles(string versionId)
    {
        var versionDir = _versionIsolation ? Path.Combine(_gameDir, "versions", versionId) : _gameDir;
        if (!Directory.Exists(versionDir))
            Directory.CreateDirectory(versionDir);

        var jsonData = Encoding.UTF8.GetString(InstallerBase.ReadSpecifyFileFromZip(_modpackFilePath, "manifest.json"));
        var json = JsonNode.Parse(jsonData)!.AsObject();

        if ((string?)json["manifestType"] != "minecraftModpack")
            throw new InvalidOperationException("Only Minecraft modpacks are supported.");

        var overrideName = (string?)json["overrides"] ?? string.Empty;

        using var archive = ZipFile.OpenRead(_modpackFilePath);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith($"{overrideName}/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = entry.FullName.Substring($"{overrideName}/".Length);
                var destinationPath = Path.Combine(versionDir, relativePath);
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }
        }
    }
}
