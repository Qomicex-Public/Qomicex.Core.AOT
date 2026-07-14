using Qomicex.Core.AOT.Models.VersionMetadata;
using System;
using System.Collections.Generic;
using System.Text;

namespace Qomicex.Core.AOT.Utils
{
    public sealed class SystemHelper
    {
        public static bool IsOsMatch(OsRequirement os)
        {
            if (os.Name != GetCurrentOsName())
                return false;

            if (!string.IsNullOrEmpty(os.Version) && !Environment.OSVersion.VersionString.Contains(os.Version))
                return false;

            if (!string.IsNullOrEmpty(os.Arch) && os.Arch != GetCurrentArch())
                return false;

            return true;
        }

        public static string GetCurrentOsName() =>
            OperatingSystem.IsWindows() ? "windows" :
            OperatingSystem.IsLinux() ? "linux" :
            OperatingSystem.IsMacOS() ? "osx" : "unknown";

        public static string GetCurrentArch() =>
            Environment.Is64BitOperatingSystem ? "64" : "32";
    }
}
