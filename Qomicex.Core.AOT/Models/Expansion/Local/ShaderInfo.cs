namespace Qomicex.Core.AOT.Models.Expansion.Local;

public class ShaderInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int CurseForgeId { get; set; }
    public string ModrinthId { get; set; } = string.Empty;
    public string Sha1Hash { get; set; } = string.Empty;
    public long CFHash { get; set; }
}
