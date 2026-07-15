namespace Qomicex.Core.AOT.Models.Expansion.Local;

public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string[] Authors { get; set; } = [];
    public string FilePath { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public int CurseForgeId { get; set; }
    public string ModrinthId { get; set; } = string.Empty;
    public bool Active => Path.GetExtension(FilePath).Equals(".jar", StringComparison.OrdinalIgnoreCase);
    public string Sha1Hash { get; set; } = string.Empty;
    public long CFHash { get; set; }
}
