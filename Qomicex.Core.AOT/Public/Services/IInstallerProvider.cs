using Qomicex.Core.AOT.Public.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Public.Services
{
    public interface IInstallerProvider
    {
        Task<List<ModLoaderResult>> GetAvailableModLoaders(string gameVersion,ModLoaderType type = ModLoaderType.All);
    }
}
