namespace Qomicex.Core.AOT.Services.Installers.Modpacks;

public enum ModpackLoaderType
{
    Forge,
    Fabric,
    Quilt,
    NeoForge,
    Unknown
}

public sealed class CurseForgeModpackInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public ModpackLoaderType ModLoader { get; set; }
    public string ModLoaderVersion { get; set; } = string.Empty;
    public List<CurseForgeModpackFileInfo> Files { get; set; } = [];
}

public sealed class CurseForgeModpackFileInfo
{
    public int ProjectId { get; set; }
    public int FileId { get; set; }
}

public sealed class ModrinthModpackInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public ModpackLoaderType ModLoader { get; set; }
    public string ModLoaderVersion { get; set; } = string.Empty;
    public List<ModrinthModpackFileInfo> Files { get; set; } = [];
}

public sealed class ModrinthModpackFileInfo
{
    public string Path { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public long Size { get; set; }
}
