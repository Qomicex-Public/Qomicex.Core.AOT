using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers.Modpacks;

internal sealed class ModrinthModpackInstaller : InstallerBase, IInstaller
{
    private readonly string _gameDir;
    private readonly bool _versionIsolation;
    private readonly string _modpackFilePath;

    internal ModrinthModpackInstaller(string gameDir, bool versionIsolation, string modpackFilePath)
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

    internal ModrinthModpackInfo GetModpackInfo(string versionId)
    {
        var info = new ModrinthModpackInfo();
        var jsonData = Encoding.UTF8.GetString(ReadSpecifyFileFromZip(_modpackFilePath, "modrinth.index.json"));

        var json = JsonNode.Parse(jsonData)!.AsObject();

        if ((string?)json["game"] != "minecraft")
            throw new InvalidOperationException("Only Minecraft modpacks are supported.");

        info.Name = (string?)json["name"] ?? string.Empty;
        info.Description = (string?)json["summary"] ?? string.Empty;
        info.Version = (string?)json["versionId"] ?? string.Empty;

        if (json["dependencies"] is JsonObject deps)
        {
            foreach (var (key, value) in deps)
            {
                string loaderVersion = (string?)value ?? string.Empty;

                if (key == "minecraft")
                    info.GameVersion = loaderVersion;
                else if (key is "quilt-loader" or "fabric-loader" or "forge" or "neoforge")
                {
                    info.ModLoader = key switch
                    {
                        "quilt-loader" => ModpackLoaderType.Quilt,
                        "fabric-loader" => ModpackLoaderType.Fabric,
                        "forge" => ModpackLoaderType.Forge,
                        "neoforge" => ModpackLoaderType.NeoForge,
                        _ => ModpackLoaderType.Unknown
                    };
                    info.ModLoaderVersion = loaderVersion;
                }
            }
        }

        var filesArray = json["files"]?.AsArray();
        if (filesArray != null)
        {
            var basePath = _versionIsolation ? Path.Combine(_gameDir, "versions", versionId) : _gameDir;
            foreach (var file in filesArray.OfType<JsonObject>())
            {
                string clientEnv = (string?)file["env"]?["client"] ?? "required";
                if (clientEnv != "required")
                    continue;

                var downloads = file["downloads"]?.AsArray();
                var fileInfo = new ModrinthModpackFileInfo
                {
                    Path = Path.Combine(basePath, (string?)file["path"] ?? string.Empty),
                    Hash = (string?)file["hashes"]?["sha1"] ?? string.Empty,
                    Url = downloads is { Count: > 0 } ? (string?)downloads[0] ?? string.Empty : string.Empty,
                    Size = (long?)file["fileSize"] ?? 0
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

        using var archive = ZipFile.OpenRead(_modpackFilePath);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("override/", StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = entry.FullName.Substring("override/".Length);
                string destinationPath = Path.Combine(versionDir, relativePath);

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
