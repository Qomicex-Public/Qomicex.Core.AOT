namespace Qomicex.Core.AOT.Services.Installers;

public interface IInstallerFactory
{
    IInstaller CreateFabric(int downloadSource, string gameDir);
    IInstaller CreateQuilt(int downloadSource, string gameDir);
    IInstaller CreateForge(int downloadSource, string gameDir, string gameVersion);
    IInstaller CreateNeoForge(int downloadSource, string gameDir, string gameVersion);
    IInstaller CreateLiteLoader(int downloadSource, string gameDir, string gameVersion);
    IInstaller CreateOptiFine(int downloadSource, string gameDir, string gameVersion);
}
