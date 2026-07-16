using Qomicex.Core.AOT.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Public.Models
{
    public record JavaResult(
        string Path,
        int MajorVersion,
        string Version,
        JavaState State,
        string Arch,
        JavaType Type,
        string DiscoveredBy,
        string Name
        );

    public record JavaSearchOptions(
        List<string> CustomExcludePaths,
        string? CustomRootPath,
        string? GameDir,
        JavaSearchMode Mode = JavaSearchMode.Quick,
        bool IncludeJRE = true,
        bool IncludeJDK = true,
        int MaxDepth = 5,
        int MaxResults = 100,
        bool ScanHiddenFolders = false,
        bool IncludeNetworkDrives = false
    );

    public enum JavaSearchMode { Quick, Deep, Custom }
    public enum JavaState
    {
        Valid,
        InvalidPath,
        MissingReleaseFile,
        CorruptedReleaseFile,
        UnknownError
    }
    public enum JavaType
    {
        Unknown,
        JDK,
        JRE
    }
}
