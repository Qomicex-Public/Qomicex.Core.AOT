using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Models.Expansion.CurseForge;
using Qomicex.Core.AOT.Models.Expansion.Local;
using Qomicex.Core.AOT.Models.Expansion.Modrinth;
using Tomlyn;
using Tomlyn.Model;

namespace Qomicex.Core.AOT.Services.Expansion.Local;

public class Mods : LocalResourceBase
{
    private readonly string _gameDirectory;
    private readonly string _version;
    private readonly bool _versionSegmented;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public Mods(HttpClient http, string gameDirectory, string version, bool versionSegmented, string apiKey)
    {
        _http = http;
        _gameDirectory = gameDirectory;
        _version = version;
        _versionSegmented = versionSegmented;
        _apiKey = apiKey;
    }

    private string ModDirectory => _versionSegmented
        ? Path.Combine(_gameDirectory, "versions", _version, "mods")
        : Path.Combine(_gameDirectory, "mods");

    private List<string> GetModFiles()
    {
        if (!Directory.Exists(ModDirectory))
            return [];

        var files = new List<string>();
        files.AddRange(Directory.GetFiles(ModDirectory, "*.jar"));
        files.AddRange(Directory.GetFiles(ModDirectory, "*.disabled"));
        return files;
    }

    private static string[] ExtractFabricAuthors(JsonNode? authorsToken)
    {
        if (authorsToken is not JsonArray arr) return [];
        return arr.Select(a =>
        {
            if (a is JsonObject obj && obj["name"] != null)
                return obj["name"]!.ToString();
            return a?.ToString() ?? "";
        }).ToArray();
    }

    private static string? ReadZipEntry(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry == null) return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string ExtractIconFromArchive(ZipArchive archive, string iconPath)
    {
        var entry = archive.GetEntry(iconPath);
        if (entry == null) return string.Empty;
        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.Length > 0 ? Convert.ToBase64String(ms.ToArray()) : string.Empty;
    }

    public async Task<List<ModInfo>> GetModList(Action<int, int>? onProgress = null)
    {
        var modFiles = GetModFiles();
        Trace.WriteLine($"Fetching mod list: {_version}, dir: {ModDirectory}, count: {modFiles.Count}");

        var hashBag = new ConcurrentBag<(string hash, long cfHash)>();
        var modBag = new ConcurrentBag<ModInfo>();
        int processedCount = 0;
        var totalCount = modFiles.Count;

        onProgress?.Invoke(0, totalCount);

        Parallel.ForEach(modFiles, mod =>
        {
            byte[] fileBytes = File.ReadAllBytes(mod);
            var hash = Convert.ToHexString(SHA1.HashData(fileBytes)).ToLowerInvariant();
            var cfHash = CurseForgeFingerprint(fileBytes);

            var modInfo = new ModInfo
            {
                FilePath = mod,
                Sha1Hash = hash,
                CFHash = cfHash,
            };

            try
            {
                using var archive = new ZipArchive(new MemoryStream(fileBytes), ZipArchiveMode.Read);

                var fabricContent = ReadZipEntry(archive, "fabric.mod.json");
                if (fabricContent != null)
                {
                    var json = JsonNode.Parse(fabricContent)!.AsObject();
                    modInfo.Name = json["name"]?.ToString() ?? "Unknown";
                    modInfo.Version = json["version"]?.ToString() ?? "";
                    modInfo.Description = json["description"]?.ToString() ?? "No description available";
                    modInfo.Authors = ExtractFabricAuthors(json["authors"]);

                    var iconPath = json["icon"]?.ToString();
                    if (!string.IsNullOrEmpty(iconPath))
                        modInfo.Icon = ExtractIconFromArchive(archive, iconPath);
                }
                else
                {
                    var tomlContent = ReadZipEntry(archive, "META-INF/mods.toml")
                        ?? ReadZipEntry(archive, "META-INF/neoforge.mods.toml");
                    if (tomlContent != null)
                    {
                        var model = Toml.ToModel(tomlContent);
                        var mods = (TomlTableArray)model["mods"];
                        var firstMod = (TomlTable)mods[0];

                        modInfo.Name = firstMod.TryGetValue("displayName", out var dn)
                            ? dn?.ToString() ?? "Unknown" : "Unknown";
                        modInfo.Description = firstMod.TryGetValue("description", out var desc)
                            ? desc?.ToString() ?? "" : "";
                        modInfo.Version = firstMod.TryGetValue("version", out var ver)
                            ? ver?.ToString() ?? "" : "";

                        if (modInfo.Version == "${file.jarVersion}")
                        {
                            var metaData = ReadZipEntry(archive, "META-INF/MANIFEST.MF") ?? "";
                            foreach (var item in metaData.Split(["\r\n", "\n"], StringSplitOptions.None))
                            {
                                if (item.StartsWith("Implementation-Version:", StringComparison.OrdinalIgnoreCase))
                                {
                                    modInfo.Version = item["Implementation-Version:".Length..].Trim();
                                    break;
                                }
                            }
                        }

                        if (firstMod.TryGetValue("authors", out var aut) && aut is string autStr)
                            modInfo.Authors = autStr.Split(',').Select(a => a.Trim()).ToArray();

                        if (firstMod.TryGetValue("logoFile", out var lf) && lf is string logoFile && !string.IsNullOrEmpty(logoFile))
                            modInfo.Icon = ExtractIconFromArchive(archive, logoFile);
                    }
                    else
                    {
                        var mcmodContent = ReadZipEntry(archive, "mcmod.info");
                        if (mcmodContent != null)
                        {
                            var mcmodArray = JsonNode.Parse(mcmodContent)!.AsArray();
                            if (mcmodArray.Count > 0)
                            {
                                var firstEntry = mcmodArray[0]!.AsObject();
                                modInfo.Name = firstEntry["name"]?.ToString() ?? "Unknown";
                                modInfo.Description = firstEntry["description"]?.ToString() ?? "";
                                modInfo.Version = firstEntry["version"]?.ToString() ?? "";
                                if (firstEntry["authors"] is JsonArray authorsArray)
                                    modInfo.Authors = authorsArray.Select(a => a!.ToString()).ToArray();
                                else if (firstEntry["authors"] is JsonValue jv)
                                    modInfo.Authors = firstEntry["authors"]!.ToString().Split(',').Select(a => a.Trim()).ToArray();
                            }
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(modInfo.Name))
                modInfo.Name = Path.GetFileNameWithoutExtension(mod);

            hashBag.Add((hash, cfHash));
            modBag.Add(modInfo);

            var current = Interlocked.Increment(ref processedCount);
            onProgress?.Invoke(current, totalCount);
        });

        var hashList = hashBag.Select(x => x.hash).ToList();
        var mHashList = hashBag.Select(x => x.cfHash).ToList();
        var modInfos = modBag.ToList();

        var cfDict = new Dictionary<long, object>();
        var mrDict = new Dictionary<string, object>();

        if (hashList.Count > 0)
        {
            try
            {
                var cf = new CurseForge.CurseForgeBase(_http, _apiKey);
                var cfResult = await cf.GetInfoFromHashesDictAsync(mHashList);
                foreach (var (key, value) in cfResult)
                    cfDict[key] = value;
            }
            catch (Exception ex) { Trace.WriteLine($"[Mods] CF hash lookup failed: {ex.GetType().Name}: {ex.Message}"); }

            try
            {
                var mr = new Modrinth.ModrinthBase(_http);
                var mrResult = await mr.GetProjectVersionsFromHashesDictAsync(hashList);
                foreach (var (key, value) in mrResult)
                    mrDict[key] = value;
            }
            catch (Exception ex) { Trace.WriteLine($"[Mods] MR hash lookup failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        foreach (var modInfo in modInfos)
        {
            if (cfDict.TryGetValue(modInfo.CFHash, out var cfObj) && cfObj is FingerprintsFilesMeta { ModId: > 0 } cfMeta)
                modInfo.CurseForgeId = cfMeta.ModId;
            if (mrDict.TryGetValue(modInfo.Sha1Hash, out var mrObj) && mrObj is ProjectVersionInfo mrMeta)
                modInfo.ModrinthId = mrMeta.ProjectId ?? "";
        }

        var iconTasks = modInfos
            .Where(m => string.IsNullOrEmpty(m.Icon))
            .Select(async modInfo =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(modInfo.ModrinthId))
                    {
                        var mr = new Modrinth.Mods(_http);
                        var project = await mr.GetProjectInfoAsync(modInfo.ModrinthId);
                        if (!string.IsNullOrEmpty(project?.IconUrl))
                        {
                            modInfo.Icon = await DownloadIconAsBase64(project.IconUrl);
                            return;
                        }
                    }
                }
                catch { }

                try
                {
                    if (modInfo.CurseForgeId > 0)
                    {
                        var cf = new CurseForge.Mods(_http, _apiKey);
                        var info = await cf.GetModInfoAsync(modInfo.CurseForgeId.ToString());
                    }
                }
                catch { }
            });

        await Task.WhenAll(iconTasks);
        return modInfos;
    }

    private async Task<string> DownloadIconAsBase64(string url)
    {
        var bytes = await _http.GetByteArrayAsync(url);
        if (bytes.Length == 0) return string.Empty;
        return Convert.ToBase64String(bytes);
    }

    public void DisableMod(string modFilePath)
    {
        if (File.Exists(modFilePath))
            File.Move(modFilePath, modFilePath + ".disabled");
    }

    public void EnableMod(string modFilePath)
    {
        if (File.Exists(modFilePath) && modFilePath.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
            File.Move(modFilePath, modFilePath[..^".disabled".Length]);
    }
}
