namespace Qomicex.Core.AOT.Models.Expansion.CurseForge;

public static class ModLoaderType
{
    public const string Any = "Any";
    public const string Forge = "Forge";
    public const string LiteLoader = "LiteLoader";
    public const string Fabric = "Fabric";
    public const string Quilt = "Quilt";
    public const string NeoForge = "NeoForge";

    public static readonly string[] All = [Forge, LiteLoader, Fabric, Quilt, NeoForge];
}
