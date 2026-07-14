using Qomicex.Core.AOT.Models.Local;
using Qomicex.Core.AOT.Models.VersionMetadata;

namespace Qomicex.Core.AOT.Interfaces.Core;

public interface IVersionLocator
{
    List<LocalVersionInfo> GetAllVersions();

    CompleteVersionMetadata? GetVersionMetadata(string versionId);

    bool IsVersionInstalled(string versionId);

    void RefreshCache();

    string GetVersionPath(string versionId);
}
