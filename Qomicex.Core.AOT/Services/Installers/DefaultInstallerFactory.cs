namespace Qomicex.Core.AOT.Services.Installers;

using Qomicex.Core.AOT.Services.Installers.Modpacks;

internal sealed class DefaultInstallerFactory : IInstallerFactory
{
    public IInstaller CreateFabric(int downloadSource, string gameDir)
        => new FabricInstaller(downloadSource, gameDir);

    public IInstaller CreateQuilt(int downloadSource, string gameDir)
        => new QuiltInstaller(downloadSource, gameDir);

    public IInstaller CreateForge(int downloadSource, string gameDir, string gameVersion)
        => new ForgeInstaller(downloadSource, gameDir, gameVersion);

    public IInstaller CreateNeoForge(int downloadSource, string gameDir, string gameVersion)
        => new NeoForgeInstaller(downloadSource, gameDir, gameVersion);

    public IInstaller CreateLiteLoader(int downloadSource, string gameDir, string gameVersion)
        => new LiteloaderInstaller(downloadSource, gameDir, gameVersion);

    public IInstaller CreateOptiFine(int downloadSource, string gameDir, string gameVersion)
        => new OptiFineInstaller(downloadSource, gameDir, gameVersion);

    public IInstaller CreateCurseForgeModpack(string gameDir, bool versionIsolation, string modpackFilePath)
        => new CurseForgeModpackInstaller(gameDir, versionIsolation, modpackFilePath);

    public IInstaller CreateModrinthModpack(string gameDir, bool versionIsolation, string modpackFilePath)
        => new ModrinthModpackInstaller(gameDir, versionIsolation, modpackFilePath);
}
