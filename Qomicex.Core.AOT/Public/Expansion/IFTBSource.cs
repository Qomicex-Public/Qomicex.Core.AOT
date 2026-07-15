using Qomicex.Core.AOT.Models.Expansion.FeedTheBeast;

namespace Qomicex.Core.AOT.Public.Expansion;

public interface IFTBSource
{
    Task<List<ModpackInfo>> SearchAsync(
        string? query = null,
        List<string>? tags = null,
        string? mcVersion = null,
        string? loader = null,
        string sort = "featured",
        int limit = 20
    );

    Task<ModpackInfo?> GetPackDetailAsync(int id);

    Task<VersionDetail?> GetVersionDetailAsync(int packId, int versionId);

    Task<ChangelogResult?> GetChangelogAsync(int packId, int versionId);

    static VersionInfo? GetLatestVersion(ModpackInfo pack)
    {
        return pack.Versions?
            .Where(v => v.Type == "release")
            .MaxBy(v => v.Updated);
    }
}
