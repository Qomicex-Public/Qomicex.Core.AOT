using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;

namespace Qomicex.Core.AOT.Interfaces.Core;

public interface IVersionLocator
{
    List<LocalVersionInfo> GetAllVersions();

    CompleteVersionMetadata? GetVersionMetadata(string versionId);

    bool IsVersionInstalled(string versionId);

    void RefreshCache();

    string GetVersionPath(string versionId);

    Task<List<MissFileInfo>> GetMissFilesAsync(CompleteVersionMetadata meta);

    Task<List<MissFileInfo>> GetMissFilesAsync(string jsonData);

    Task<List<MissFileInfo>> GetMissLibrariesAsync(CompleteVersionMetadata meta);

    Task<List<MissFileInfo>> GetMissLibrariesAsync(string jsonData);

    Task<MissFileInfo?> GetMissMainJarAsync(CompleteVersionMetadata meta);

    Task<MissFileInfo?> GetMissMainJarAsync(string jsonData);

    Task<List<MissFileInfo>> GetMissAssetsAsync(CompleteVersionMetadata meta);

    Task<List<MissFileInfo>> GetMissAssetsAsync(string jsonData);
}
