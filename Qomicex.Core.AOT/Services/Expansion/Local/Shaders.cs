using System.Security.Cryptography;
using Qomicex.Core.AOT.Models.Expansion.CurseForge;
using Qomicex.Core.AOT.Models.Expansion.Local;
using Qomicex.Core.AOT.Models.Expansion.Modrinth;

namespace Qomicex.Core.AOT.Services.Expansion.Local;

internal class Shaders : LocalResourceBase
{
    private readonly string _gameDirectory;
    private readonly string _version;
    private readonly bool _versionSegmented;
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public Shaders(HttpClient http, string gameDirectory, string version, bool versionSegmented, string apiKey)
    {
        _http = http;
        _gameDirectory = gameDirectory;
        _version = version;
        _versionSegmented = versionSegmented;
        _apiKey = apiKey;
    }

    #region 文件扫描

    private List<string> GetShaderFiles()
    {
        string shaderDirectory = _versionSegmented
            ? Path.Combine(_gameDirectory, "versions", _version, "shaderpacks")
            : Path.Combine(_gameDirectory, "shaderpacks");

        if (!Directory.Exists(shaderDirectory))
            return [];

        return Directory.GetFiles(shaderDirectory, "*.zip").ToList();
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

    #endregion

    #region 光影列表

    public async Task<List<ShaderInfo>> GetShaderList()
    {
        var files = GetShaderFiles();
        var sha1List = new List<string>();
        var mHashList = new List<long>();
        var shaderInfos = new List<ShaderInfo>();

        foreach (var file in files)
        {
            var (sha1, cfHash) = ComputeHashesForFile(file);
            sha1List.Add(sha1);
            mHashList.Add(cfHash);

            string fallbackName = Path.GetFileNameWithoutExtension(file);

            shaderInfos.Add(new ShaderInfo
            {
                FilePath = file,
                Sha1Hash = sha1,
                CFHash = cfHash,
                Name = fallbackName
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

        foreach (var shaderInfo in shaderInfos)
        {
            if (cfDict.TryGetValue(shaderInfo.CFHash, out var cfMeta))
            {
                shaderInfo.CurseForgeId = cfMeta.ModId;
            }

            if (mrDict.TryGetValue(shaderInfo.Sha1Hash, out var mrMeta))
            {
                shaderInfo.ModrinthId = mrMeta.ProjectId ?? "";
                if (!string.IsNullOrEmpty(mrMeta.Name))
                    shaderInfo.Name = mrMeta.Name;
                if (!string.IsNullOrEmpty(mrMeta.VersionNumber))
                    shaderInfo.Version = mrMeta.VersionNumber;
            }
        }

        return shaderInfos;
    }

    #endregion
}
