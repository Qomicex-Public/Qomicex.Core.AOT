using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Qomicex.Core.AOT.Models.Expansion.CurseForge;
using Qomicex.Core.AOT.Models.Expansion.Local;
using Qomicex.Core.AOT.Models.Expansion.Modrinth;

namespace Qomicex.Core.AOT.Services.Expansion.Local;

internal class DataPacks : LocalResourceBase
{
    private readonly string _gameDirectory;
    private readonly string _version;
    private readonly bool _versionSegmented;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public DataPacks(HttpClient http, string gameDirectory, string version, bool versionSegmented, string apiKey)
    {
        _http = http;
        _gameDirectory = gameDirectory;
        _version = version;
        _versionSegmented = versionSegmented;
        _apiKey = apiKey;
    }

    #region 文件扫描

    private List<string> GetDataPackFiles()
    {
        string datapackDirectory = _versionSegmented
            ? Path.Combine(_gameDirectory, "versions", _version, "datapacks")
            : Path.Combine(_gameDirectory, "datapacks");

        if (!Directory.Exists(datapackDirectory))
            return [];

        var entries = new List<string>();
        entries.AddRange(Directory.GetFiles(datapackDirectory, "*.zip"));

        foreach (var dir in Directory.GetDirectories(datapackDirectory))
        {
            if (File.Exists(Path.Combine(dir, "pack.mcmeta")))
                entries.Add(dir);
        }

        return entries;
    }

    #endregion

    #region 元数据解析

    private static JsonNode? ReadMcmetaFromZip(string zipPath)
    {
        var bytes = TryReadFileFromZip(zipPath, "pack.mcmeta");
        if (bytes == null)
            return null;

        try
        {
            return JsonNode.Parse(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static JsonNode? ReadMcmetaFromFolder(string folderPath)
    {
        string mcmetaPath = Path.Combine(folderPath, "pack.mcmeta");
        if (!File.Exists(mcmetaPath))
            return null;

        try
        {
            string jsonContent = File.ReadAllText(mcmetaPath);
            return JsonNode.Parse(jsonContent);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadIconFromZip(string zipPath)
    {
        var bytes = TryReadFileFromZip(zipPath, "pack.png");
        if (bytes == null || bytes.Length == 0)
            return string.Empty;
        return Convert.ToBase64String(bytes);
    }

    private static string ReadIconFromFolder(string folderPath)
    {
        string iconPath = Path.Combine(folderPath, "pack.png");
        if (!File.Exists(iconPath))
            return string.Empty;

        try
        {
            byte[] bytes = File.ReadAllBytes(iconPath);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    #endregion

    #region 哈希计算

    private static (string sha1, long cfHash) ComputeHashesForFile(string filePath)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);
        string sha1Hash = Convert.ToHexString(SHA1.HashData(fileBytes)).ToLowerInvariant();
        long cfHash = CurseForgeFingerprint(fileBytes);
        return (sha1Hash, cfHash);
    }

    private static (string sha1, long cfHash) ComputeHashesForFolder(string folderPath)
    {
        using var memStream = new MemoryStream();
        using (var archive = new ZipArchive(memStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(folderPath, file).Replace('\\', '/');
                archive.CreateEntryFromFile(file, relativePath);
            }
        }

        memStream.Position = 0;
        byte[] zipBytes = memStream.ToArray();
        string sha1Hash = Convert.ToHexString(SHA1.HashData(zipBytes)).ToLowerInvariant();
        long cfHash = CurseForgeFingerprint(zipBytes);
        return (sha1Hash, cfHash);
    }

    #endregion

    #region 数据包列表

    public async Task<List<DataPackInfo>> GetDataPackList()
    {
        var entries = GetDataPackFiles();
        var sha1List = new List<string>();
        var mHashList = new List<long>();
        var packInfos = new List<DataPackInfo>();

        foreach (var entry in entries)
        {
            bool isDirectory = Directory.Exists(entry);

            JsonNode? mcmeta = isDirectory
                ? ReadMcmetaFromFolder(entry)
                : ReadMcmetaFromZip(entry);

            string description = mcmeta?["pack"]?["description"]?.ToString() ?? "";
            int packFormat = mcmeta?["pack"]?["pack_format"]?.GetValue<int>() ?? 0;

            string icon = isDirectory
                ? ReadIconFromFolder(entry)
                : ReadIconFromZip(entry);

            string sha1;
            long cfHash;
            if (isDirectory)
                (sha1, cfHash) = ComputeHashesForFolder(entry);
            else
                (sha1, cfHash) = ComputeHashesForFile(entry);

            sha1List.Add(sha1);
            mHashList.Add(cfHash);

            string fallbackName = Path.GetFileNameWithoutExtension(entry);

            packInfos.Add(new DataPackInfo
            {
                FilePath = entry,
                IsDirectory = isDirectory,
                Sha1Hash = sha1,
                CFHash = cfHash,
                Name = fallbackName,
                Description = description,
                PackFormat = packFormat,
                Icon = icon
            });
        }

        var cfDict = new Dictionary<long, FingerprintsFilesMeta>();
        var mrDict = new Dictionary<string, ProjectVersionInfo>();

        if (sha1List.Count > 0)
        {
            try
            {
                var cf = new CurseForge.CurseForgeBase(_http, _apiKey);
                cfDict = await cf.GetInfoFromHashesDictAsync(mHashList);
            }
            catch { }

            try
            {
                var mr = new Modrinth.ModrinthBase(_http);
                mrDict = await mr.GetProjectVersionsFromHashesDictAsync(sha1List);
            }
            catch { }
        }

        foreach (var packInfo in packInfos)
        {
            if (cfDict.TryGetValue(packInfo.CFHash, out var cfMeta))
            {
                packInfo.CurseForgeId = cfMeta.ModId;
            }

            if (mrDict.TryGetValue(packInfo.Sha1Hash, out var mrMeta))
            {
                packInfo.ModrinthId = mrMeta.ProjectId ?? "";
                if (!string.IsNullOrEmpty(mrMeta.Name))
                    packInfo.Name = mrMeta.Name;
                if (!string.IsNullOrEmpty(mrMeta.VersionNumber))
                    packInfo.Version = mrMeta.VersionNumber;
            }
        }

        return packInfos;
    }

    #endregion
}
