using Qomicex.Core.AOT.Models.Expansion.Local;

namespace Qomicex.Core.AOT.Services.Expansion.Local;

public interface ILocalResourcesFactory
{
    Mods CreateMods(string version, bool versionSegmented, string apiKey);
    Saves CreateSaves(string version, bool versionSegmented, string apiKey);
    Resourcepack CreateResourcepack(string version, bool versionSegmented, string apiKey);
    Shaders CreateShaders(string version, bool versionSegmented, string apiKey);
    Screenshots CreateScreenshots(string version, bool versionSegmented, string apiKey);
    DataPacks CreateDataPacks(string version, bool versionSegmented, string apiKey);
}
