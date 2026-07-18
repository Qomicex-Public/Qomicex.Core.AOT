using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Public.Models
{
    public record ModLoaderResult(
        ModLoaderType Type,
        string Version,
        string GameVersion,
        string Url,
        string Sha1,
        bool isRecommand,
        DateTimeOffset ReleaseTime
        );

    public enum ModLoaderType
    {
        All,
        Forge,
        NeoForge,
        Fabric,
        Quilt,
        LiteLoader,
        OptiFine
    }
}
