using Qomicex.Core.AOT.Models.Expansion.Local;

namespace Qomicex.Core.AOT.Services.Expansion.Local;

public interface ILocalModsFactory
{
    Mods Create(string version, bool versionSegmented, string apiKey);
}
