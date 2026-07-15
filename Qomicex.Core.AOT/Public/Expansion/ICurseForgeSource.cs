using Qomicex.Core.AOT.Models.Expansion.CurseForge;

namespace Qomicex.Core.AOT.Public.Expansion;

public interface ICurseForgeSource
{
    Task<List<CurseForgeSearchResult>> SearchAsync(
        string searchFilter,
        string[]? gameVersions,
        int?[]? categories,
        string[]? modLoaderTypes,
        int? sortField = 1,
        int? page = 1,
        int? pageSize = 25
    );

    Task<CurseForgeInfo> GetModInfoAsync(string id);

    Task<CurseForgeFileInfo> GetFileInfoAsync(string modId, string fileId);

    Task<string> GetDownloadUrlAsync(string id, string fileId);

    Task<List<FingerprintsFilesMeta>> GetInfoFromHashesAsync(List<long> hashes);

    Task<Dictionary<long, FingerprintsFilesMeta>> GetInfoFromHashesDictAsync(List<long> hashes);
}
