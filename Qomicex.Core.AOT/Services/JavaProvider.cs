using Microsoft.Win32;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Public.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Qomicex.Core.AOT.Services
{
    internal sealed class JavaProvider : IJavaProvider
    {
        #region 排除列表和路径常量
        private static readonly HashSet<string> ExcludedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "ProgramData", "$Recycle.Bin", "System32", "SysWOW64",
            "WinSxS", "node_modules", ".git", ".svn", ".hg", "target", "build",
            "dist", ".gradle", ".m2", ".nuget", ".vscode", ".idea", "__pycache__",
            ".venv", "venv", "env", ".tox", ".pytest_cache", ".cargo", ".rustup",
            ".npm", ".yarn", ".pnpm-store", ".next", ".nuxt", "out", ".output",
            ".parcel-cache", ".webpack", ".cache", ".angular", ".svelte-kit",
            ".nyc_output", ".coverage", ".sonarqube", ".scannerwork", ".vs",
            ".vscode-test", "obj",
            "Steam", "Epic Games", "Origin", "EA Games", "Battle.net",
            "Ubisoft Game Launcher", "GOG Galaxy",
            "Temp", "tmp", "temp", "Downloads", "Prefetch", "Recent",
            "Cookies", "History", "INetCache",
            "Docker", "containerd"
        };

        private static List<string> HighPriorityPaths => _highPriorityPaths.Value;
        private static readonly Lazy<List<string>> _highPriorityPaths = new(() =>
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? @"C:\";
            return new List<string>
            {
                Path.Combine(pf, "Java"),
                Path.Combine(pf, "Eclipse Adoptium"),
                Path.Combine(pf, "Eclipse Foundation"),
                Path.Combine(pf, "Amazon Corretto"),
                Path.Combine(pf, "Microsoft", "jdk"),
                Path.Combine(pf, "BellSoft"),
                Path.Combine(pf, "Semeru"),
                Path.Combine(pf, "Zulu"),
                Path.Combine(pf, "SapMachine"),
                Path.Combine(pf, "RedHat"),
                Path.Combine(pf, "ojdkbuild"),
                Path.Combine(pf, "GraalVM"),
                Path.Combine(pf, "Liberica"),
                Path.Combine(pf, "Temurin"),
                Path.Combine(pf86, "Java"),
                Path.Combine(pf86, "Eclipse Adoptium"),
                Path.Combine(localAppData, "JetBrains"),
                Path.Combine(pf, "JetBrains"),
                Path.Combine(pf, "Android"),
                Path.Combine(systemDrive, "Android"),
                Path.Combine(userProfile, ".jdks"),
                Path.Combine(localAppData, "Programs", "Java"),
                Path.Combine(userProfile, "scoop", "apps"),
                Path.Combine(systemDrive, "tools", "java"),
                Path.Combine(commonAppData, "chocolatey", "lib")
            };
        });

        private static readonly List<string> LinuxPaths = new()
        {
            "/usr/lib/jvm",
            "/usr/java",
            "/opt/java",
            "/usr/local/java",
            "/snap",
            "/var/snap",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman/candidates/java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jabba/jdk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".asdf/installs/java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jenv/versions"),
            "/usr/lib64/jvm",
            "/usr/local/lib/jvm",
            "/opt/jdk",
            "/opt/jre",
            "/usr/local/jdk",
            "/usr/local/jre"
        };

        private static readonly List<string> MacOSPaths = new()
        {
            "/Library/Java/JavaVirtualMachines",
            "/System/Library/Java/JavaVirtualMachines",
            "/opt/homebrew/opt",
            "/usr/local/opt",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman/candidates/java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jabba/jdk"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".asdf/installs/java"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".jenv/versions"),
            "/usr/local/Cellar/openjdk",
            "/opt/local/Library/Java",
            "/usr/libexec/java_home"
        };
        #endregion
        public Task<List<JavaResult>> Search(JavaSearchOptions options)
        {
            if (options.Mode == JavaSearchMode.Custom && string.IsNullOrEmpty(options.CustomRootPath))
            {
                throw new ArgumentException("Custom模式必须提供CustomRootPath");
            }

            if (!Directory.Exists(options.CustomRootPath))
                return Task.FromResult(new List<JavaResult>());

            var excludes = new HashSet<string>(ExcludedPaths, StringComparer.OrdinalIgnoreCase);
            foreach (var path in options.CustomExcludePaths)
                excludes.Add(path);

            var results = new ConcurrentBag<JavaResult>();
            var discoveredPaths = new ConcurrentDictionary<string, bool>();

            BreadthFirstSearch(options.CustomRootPath!, results, discoveredPaths, options, excludes);

            return Task.FromResult(ProcessResults(results, options));
        }
        public Task<JavaResult> Recommand(List<JavaResult> javaResults, CompleteVersionMetadata metadata)
        {
            var diff = new List<JavaDiff>();
            if (javaResults.Count == 0)
                throw new ArgumentException("Java列表为空");

            var require = GetRequireMajroVersion(metadata);
            foreach (var javaResult in javaResults)
            {
                diff.Add(new JavaDiff { java = javaResult, diff = javaResult.MajorVersion - require });
            }
            diff.Sort((a, b) => a.diff.CompareTo(b.diff));
            foreach(var diffItem in diff)
            {
                if (diffItem.diff < 0)
                    continue;
                else if (diffItem.diff == 0)
                    return Task.FromResult(diffItem.java);
                else
                {
                    if (require == 8)
                        throw new Exception("找不到合适的Java");
                    return Task.FromResult(diffItem.java);
                }
            }
            throw new Exception("找不到合适的Java");
        }

        private struct JavaDiff
        {
            public int diff;
            public JavaResult java;
        }

        public bool Check(JavaResult java, CompleteVersionMetadata metadata)
        {
            if (java.State != JavaState.Valid)
                return false;
            bool useful = java.MajorVersion >= GetRequireMajroVersion(metadata);
            if (GetRequireMajroVersion(metadata) == 8 && useful && java.MajorVersion != 8)
                useful = false;
            return useful;
        }

        private int GetRequireMajroVersion(CompleteVersionMetadata metadata)
        {
            return metadata.JavaVersion.MajorVersion;
        }

        #region 辅助方法
        private static List<JavaResult> ProcessResults(
            ConcurrentBag<JavaResult> results,
            JavaSearchOptions options)
        {
            return results
                .OrderBy(j => j.State != JavaState.Valid)
                .ThenByDescending(j => j.MajorVersion)
                .Take(options.MaxResults)
                .ToList();
        }

        private static void BreadthFirstSearch(
            string rootPath,
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options,
            HashSet<string> excludes)
        {
            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((rootPath, 0));

            while (queue.Count > 0 && results.Count < options.MaxResults)
            {
                var (currentPath, depth) = queue.Dequeue();

                if (depth > options.MaxDepth) continue;

                try
                {
                    var javaPath = GetJavaExecutablePath(currentPath);
                    if (!string.IsNullOrEmpty(javaPath))
                    {
                        AddJavaIfValid(javaPath, results, discoveredPaths, options, $"BFS:{rootPath}");
                        continue;
                    }

                    foreach (var subDir in Directory.GetDirectories(currentPath))
                    {
                        var dirName = Path.GetFileName(subDir);

                        if (ShouldExclude(subDir, dirName, excludes))
                            continue;

                        if (!options.ScanHiddenFolders)
                        {
                            var attr = File.GetAttributes(subDir);
                            if ((attr & FileAttributes.Hidden) == FileAttributes.Hidden)
                                continue;
                        }

                        queue.Enqueue((subDir, depth + 1));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"BFS 扫描 {currentPath} 失败: {ex.Message}");
                }
            }
        }

        private static bool ShouldExclude(string fullPath, string dirName, HashSet<string> excludes)
        {
            if (excludes.Contains(dirName))
                return true;

            foreach (var exclude in excludes)
            {
                if (fullPath.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            if (OperatingSystem.IsWindows() && fullPath.StartsWith("\\\\"))
                return true;

            if (dirName.StartsWith(".") &&
                !dirName.Equals(".jdks", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals(".sdkman", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals(".jenv", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals(".jabba", StringComparison.OrdinalIgnoreCase) &&
                !dirName.Equals(".asdf", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static void SearchEnvironmentVariables(
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options)
        {
            var envVars = new[] { "JAVA_HOME", "JDK_HOME", "JRE_HOME" };
            foreach (var envVar in envVars)
            {
                var path = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    var javaPath = GetJavaExecutablePath(path);
                    if (!string.IsNullOrEmpty(javaPath))
                        AddJavaIfValid(javaPath, results, discoveredPaths, options, envVar);
                }
            }
        }

        [SupportedOSPlatform("windows")]
        private static void SearchRegistry(
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options)
        {
            var registryKeys = new[]
            {
                @"SOFTWARE\JavaSoft\Java Runtime Environment",
                @"SOFTWARE\JavaSoft\Java Development Kit",
                @"SOFTWARE\JavaSoft\JDK",
                @"SOFTWARE\WOW6432Node\JavaSoft\Java Runtime Environment",
                @"SOFTWARE\WOW6432Node\JavaSoft\Java Development Kit",
                @"SOFTWARE\WOW6432Node\JavaSoft\JDK",
                @"SOFTWARE\Eclipse Adoptium\JDK",
                @"SOFTWARE\Eclipse Adoptium\JRE",
                @"SOFTWARE\Microsoft\JDK",
                @"SOFTWARE\Amazon\Corretto",
                @"SOFTWARE\BellSoft\Liberica",
                @"SOFTWARE\Azul Systems\Zulu",
                @"SOFTWARE\AdoptOpenJDK\JDK",
                @"SOFTWARE\AdoptOpenJDK\JRE",
                @"SOFTWARE\Semeru\JDK",
                @"SOFTWARE\Semeru\JRE"
            };

            foreach (var keyPath in registryKeys)
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(keyPath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var subKey = key.OpenSubKey(subKeyName);
                        if (subKey == null) continue;

                        var javaHome = subKey.GetValue("JavaHome")?.ToString();
                        if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome))
                        {
                            var javaPath = GetJavaExecutablePath(javaHome);
                            if (!string.IsNullOrEmpty(javaPath))
                                AddJavaIfValid(javaPath, results, discoveredPaths, options, $"Registry:{keyPath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"读取注册表 {keyPath} 失败: {ex.Message}");
                }
            }
        }

        private static void SearchHighPriorityPaths(
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options)
        {
            List<string> pathsToSearch;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                pathsToSearch = HighPriorityPaths;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                pathsToSearch = LinuxPaths;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                pathsToSearch = MacOSPaths;
            else
                return;

            Parallel.ForEach(pathsToSearch, new ParallelOptions { MaxDegreeOfParallelism = 4 }, basePath =>
            {
                if (!Directory.Exists(basePath)) return;

                try
                {
                    foreach (var dir in Directory.GetDirectories(basePath))
                    {
                        var javaPath = GetJavaExecutablePath(dir);
                        if (!string.IsNullOrEmpty(javaPath))
                            AddJavaIfValid(javaPath, results, discoveredPaths, options, $"HighPriority:{basePath}");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"扫描高优先级路径 {basePath} 失败: {ex.Message}");
                }
            });
        }

        private static void SearchMinecraftRuntime(
            string gameDir,
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options)
        {
            var runtimePath = Path.Combine(gameDir, "runtime");
            if (!Directory.Exists(runtimePath)) return;

            try
            {
                Parallel.ForEach(Directory.GetDirectories(runtimePath), new ParallelOptions { MaxDegreeOfParallelism = 4 }, platformDir =>
                {
                    try
                    {
                        foreach (var versionDir in Directory.GetDirectories(platformDir))
                        {
                            var javaPath = GetJavaExecutablePath(versionDir);
                            if (!string.IsNullOrEmpty(javaPath))
                                AddJavaIfValid(javaPath, results, discoveredPaths, options, "MinecraftRuntime");
                        }
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"扫描 Minecraft runtime {platformDir} 失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"扫描 Minecraft runtime 失败: {ex.Message}");
            }
        }

        private static void SearchPathEnvironment(
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options)
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return;

            var paths = pathVar.Split(Path.PathSeparator);

            Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 4 }, pathEntry =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(pathEntry) || !Directory.Exists(pathEntry))
                        return;

                    var fullPath = Path.GetFullPath(pathEntry);
                    var javaName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
                    var javaPath = Path.Combine(fullPath, javaName);

                    if (File.Exists(javaPath))
                    {
                        AddJavaIfValid(javaPath, results, discoveredPaths, options, "PATH");
                    }
                    else if (fullPath.EndsWith("bin", StringComparison.OrdinalIgnoreCase))
                    {
                        var parentDir = Directory.GetParent(fullPath)?.FullName;
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            var parentJavaPath = Path.Combine(fullPath, javaName);
                            if (File.Exists(parentJavaPath))
                                AddJavaIfValid(parentJavaPath, results, discoveredPaths, options, "PATH");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"扫描 PATH 条目 {pathEntry} 失败: {ex.Message}");
                }
            });
        }

        private static void AddJavaIfValid(
            string javaPath,
            ConcurrentBag<JavaResult> results,
            ConcurrentDictionary<string, bool> discoveredPaths,
            JavaSearchOptions options,
            string discoveredBy)
        {
            try
            {
                var normalizedPath = Path.GetFullPath(javaPath);

                if (discoveredPaths.ContainsKey(normalizedPath))
                    return;

                discoveredPaths[normalizedPath] = true;

                var javaInfo = GetJavaInfo(normalizedPath, discoveredBy);
                if (javaInfo == null) return;

                if (!options.IncludeJRE && javaInfo.Type == JavaType.JRE == true)
                    return;
                if (!options.IncludeJDK && javaInfo.Type == JavaType.JDK == true)
                    return;

                results.Add(javaInfo);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"添加 Java {javaPath} 失败: {ex.Message}");
            }
        }

        private static JavaResult? GetJavaInfo(string javaPath, string discoveredBy)
        {
            var javaInfo = new JavaResult(javaPath,0,"",JavaState.UnknownError,"",JavaType.Unknown,discoveredBy,"Java");

            if (!File.Exists(javaPath))
            {
                javaInfo = javaInfo with { State = JavaState.InvalidPath };
                return javaInfo;
            }

            var javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaPath));
            if (string.IsNullOrEmpty(javaHome))
            {
                javaInfo = javaInfo with { State = JavaState.InvalidPath };
                return javaInfo;
            }

            var releaseFile = Path.Combine(javaHome, "release");
            if (!File.Exists(releaseFile))
            {
                javaInfo = javaInfo with { State = JavaState.MissingReleaseFile };
                TryGetVersionFromCommand(javaInfo);
                return javaInfo;
            }

            try
            {
                var lines = File.ReadAllLines(releaseFile);
                foreach (var line in lines)
                {
                    if (line.StartsWith("JAVA_VERSION="))
                    {
                        javaInfo = javaInfo with { Version = line.Split('=')[1].Trim('"'), MajorVersion = GetNormalizedMajorVersion(javaInfo.Version), Name = $"Java {javaInfo.Version}" };
                    }
                    else if (line.StartsWith("JAVA_RUNTIME_NAME="))
                    {
                        var runtimeName = line.Split('=')[1].Trim('"');
                        if (runtimeName.Contains("JDK"))
                            javaInfo = javaInfo with { Type = JavaType.JDK };

                        else if (runtimeName.Contains("JRE"))
                            javaInfo = javaInfo with { Type = JavaType.JRE };
                    }
                    else if (line.StartsWith("OS_ARCH="))
                    {
                        javaInfo = javaInfo with { Arch = line.Split('=')[1].Trim('"') };
                    }
                    else if (line.StartsWith("IMPLEMENTOR="))
                    {
                        var implementor = line.Split('=')[1].Trim('"');
                        if (!string.IsNullOrEmpty(implementor) && implementor != "Oracle Corporation")
                            javaInfo = javaInfo with { Name = $"{implementor} {javaInfo.Name}" }; ;
                    }
                }

                if (javaInfo.Type == JavaType.Unknown)
                {
                    if (Directory.Exists(Path.Combine(javaHome, "jre")) ||
                        Directory.Exists(Path.Combine(javaHome, "include")))
                        javaInfo = javaInfo with { Type = JavaType.JDK };
                    else
                        javaInfo = javaInfo with { Type = JavaType.JRE };
                }
                javaInfo = javaInfo with { Type = JavaType.JDK ,State = JavaState.Valid };
            }
            catch (Exception ex)
            {
                javaInfo = javaInfo with { State = JavaState.CorruptedReleaseFile };
                TryGetVersionFromCommand(javaInfo);
            }

            return javaInfo;
        }

        private static int GetNormalizedMajorVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version)) return -1;

            var parts = version.Split('.');
            if (parts[0] == "1" && parts.Length > 1 && int.TryParse(parts[1], out int legacyMajor))
                return legacyMajor;

            if (int.TryParse(parts[0], out int major))
                return major;

            return -1;
        }

        private static void TryGetVersionFromCommand(JavaResult javaInfo)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = javaInfo.Path,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                if (!string.IsNullOrEmpty(output))
                {
                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("version"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"""(\d+(:?\.\d+)*)""");
                            if (match.Success)
                            {
                                javaInfo = javaInfo with { Version = match.Groups[1].Value, MajorVersion = GetNormalizedMajorVersion(javaInfo.Version), Name = $"Java {javaInfo.Version} (未验证)" };
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"通过命令获取 Java 版本失败: {ex.Message}");
            }
        }

        private static string? GetJavaExecutablePath(string javaHome)
        {
            var binDir = Path.Combine(javaHome, "bin");
            if (!Directory.Exists(binDir))
                return null;

            var javaName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "java.exe" : "java";
            var javaPath = Path.Combine(binDir, javaName);

            return File.Exists(javaPath) ? javaPath : null;
        }

        private static List<string> GetValidDrives(bool includeNetworkDrives)
        {
            var drives = new List<string>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (!includeNetworkDrives && drive.DriveType == DriveType.Network) continue;
                    if (drive.DriveType == DriveType.CDRom || drive.DriveType == DriveType.Removable) continue;

                    drives.Add(drive.RootDirectory.FullName);
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                drives.Add("/");
                drives.Add("/home");
                drives.Add("/opt");
                drives.Add("/usr");
            }

            return drives;
        }
        #endregion
    }
}
