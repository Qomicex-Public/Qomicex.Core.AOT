namespace Qomicex.Core.AOT.Utils;

public static class PathHelper
{
    public static string GetMinecraftPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ".minecraft"
        );
    }

    public static string GetVersionPath(string gameRoot, string versionId)
    {
        return Path.Combine(gameRoot, "versions", versionId);
    }

    public static string GetLibrariesPath(string gameRoot)
    {
        return Path.Combine(gameRoot, "libraries");
    }

    public static string GetAssetsPath(string gameRoot)
    {
        return Path.Combine(gameRoot, "assets");
    }

    public static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }
}
