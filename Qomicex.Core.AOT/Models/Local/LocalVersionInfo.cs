namespace Qomicex.Core.AOT.Models.Local;

public record LocalVersionInfo(
    string Id,
    List<ModloaderInfo> Type,
    DateTime ReleaseTime,
    bool IsComplete,
    string VersionPath,
    string VanillaVersion,
    long TotalSize
);

public record ModloaderInfo(
    ModloaderType Type,
    string Version
);

public enum ModloaderType
{
    Unknown,
    Vanilla,
    Forge,
    NeoForge,
    Fabric,
    Quilt,
    LiteLoader,
    OptiFine
}