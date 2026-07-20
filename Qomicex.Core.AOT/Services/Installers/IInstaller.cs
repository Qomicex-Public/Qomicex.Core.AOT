namespace Qomicex.Core.AOT.Services.Installers;

public interface IInstaller
{
    Task InstallAsync(string versionId, string inheritsFromJson, string? para1, string? para2, string? para3, string? para4);

    Task<List<MissFileData>> GetMissLibrariesAsync(string? para1, string? para2, string? para3);
}
