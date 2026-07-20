namespace Qomicex.Core.AOT.Services.Installers;

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
}
