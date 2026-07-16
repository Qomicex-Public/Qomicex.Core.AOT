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
    public enum DownloadSource
    {
        BMCLAPI,
        Adoptium,
        Zulu
    }

    public enum JavaPlatform
    {
        Windows,
        Linux,
        MacOS
    }

    public enum JavaArchitecture
    {
        X64,
        Arm64
    }

    public enum JavaPackageType
    {
        JRE,
        JDK
    }
    public record JavaPackageInfo
    {
        public int MajorVersion { get; set; }
        public string FullVersion { get; set; } = string.Empty;
        public string Build { get; set; } = string.Empty;
        public JavaPlatform Platform { get; set; }
        public JavaArchitecture Architecture { get; set; }
        public JavaPackageType PackageType { get; set; }
        public DownloadSource Source { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public long? Size { get; set; }
    }
}
