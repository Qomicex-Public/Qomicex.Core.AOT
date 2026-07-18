using Microsoft.Win32;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Public.Services;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services
{
    internal sealed class JavaProvider : IJavaProvider
    {
        private readonly HttpClient _http;

        public JavaProvider(HttpClient http)
        {
            _http = http;
        }

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
        public Task<List<JavaResult>> Search(JavaSearchOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(options);
            if (options.Mode == JavaSearchMode.Custom && string.IsNullOrEmpty(options.CustomRootPath))
            {
                throw new ArgumentException("Custom模式必须提供CustomRootPath");
            }

            return options.Mode switch
            {
                JavaSearchMode.Quick => SearchQuick(options),
                JavaSearchMode.Deep => SearchDeep(options),
                JavaSearchMode.Custom => SearchCustom(options),
                _ => throw new ArgumentOutOfRangeException()
            };
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
            foreach (var diffItem in diff)
            {
                if (diffItem.diff < 0)
                    continue;
                else if (diffItem.diff == 0)
                    return Task.FromResult(diffItem.java);
                else
                {
                    if (require == 8)
            throw new Exception($"找不到合适的Java运行时 (需要 Java >= {require})");
                    return Task.FromResult(diffItem.java);
                }
            }
            throw new Exception($"找不到合适的Java运行时 (需要 Java >= {require})");
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

        public async Task<List<JavaPackageInfo>> GetPackages(int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType,
            JavaDownloadSource source = JavaDownloadSource.Adoptium)
        {
            if (majorVersion <= 0)
                throw new ArgumentException("Java major version must be greater than 0.", nameof(majorVersion));

            var result = source switch
            {
                JavaDownloadSource.Adoptium => await GetLatestFromAdoptiumAsync(majorVersion, platform, architecture, packageType),
                JavaDownloadSource.Zulu => await GetLatestFromZuluAsync(majorVersion, platform, architecture, packageType),
                JavaDownloadSource.BMCLAPI => await GetLatestFromBmclapiAsync(majorVersion, platform, architecture, packageType),
                _ => throw new ArgumentException($"Unsupported Java download source: {source}.", nameof(source))
            };

            return result is not null ? new List<JavaPackageInfo> { result } : new List<JavaPackageInfo>();
        }

        // ==================== Adoptium ====================

        private static string BuildAdoptiumUrl(int majorVersion)
        {
            return $"https://api.adoptium.net/v3/assets/latest/{majorVersion}/hotspot";
        }

        private async Task<JavaPackageInfo?> GetLatestFromAdoptiumAsync(
            int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            try
            {
                var json = await _http.GetStringAsync(BuildAdoptiumUrl(majorVersion));
                var assets = JsonNode.Parse(json)!.AsArray();
                var matched = assets.FirstOrDefault(asset => IsMatchingAdoptiumBinary(asset!, platform, architecture, packageType));
                return matched is null ? null : ToAdoptiumPackageInfo(matched, majorVersion, platform, architecture, packageType);
            }
            catch
            {
                return null;
            }
        }

        private static bool IsMatchingAdoptiumBinary(
            JsonNode asset,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            return string.Equals(asset["binary"]?["os"]?.ToString(), MapAdoptiumOs(platform), StringComparison.OrdinalIgnoreCase)
                && string.Equals(asset["binary"]?["architecture"]?.ToString(), MapAdoptiumArchitecture(architecture), StringComparison.OrdinalIgnoreCase)
                && string.Equals(asset["binary"]?["image_type"]?.ToString(), MapAdoptiumImageType(packageType), StringComparison.OrdinalIgnoreCase)
                && IsPortablePackage(asset["binary"]?["package"]?["name"]?.ToString(), platform);
        }

        private static JavaPackageInfo ToAdoptiumPackageInfo(
            JsonNode asset,
            int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            return new JavaPackageInfo
            {
                MajorVersion = majorVersion,
                FullVersion = asset["version"]?["openjdk_version"]?.ToString() ?? string.Empty,
                Build = asset["version"]?["build"]?.ToString() ?? string.Empty,
                Platform = platform,
                Architecture = architecture,
                PackageType = packageType,
                Source = JavaDownloadSource.Adoptium,
                FileName = asset["binary"]?["package"]?["name"]?.ToString() ?? string.Empty,
                DownloadUrl = asset["binary"]?["package"]?["link"]?.ToString() ?? string.Empty,
                Sha256 = asset["binary"]?["package"]?["checksum"]?.ToString() ?? string.Empty,
                Size = asset["binary"]?["package"]?["size"]?.GetValue<long>()
            };
        }

        private static string MapAdoptiumOs(JavaPlatform platform)
        {
            return platform switch
            {
                JavaPlatform.Windows => "windows",
                JavaPlatform.Linux => "linux",
                JavaPlatform.MacOS => "mac",
                _ => throw new ArgumentException($"Unsupported Java platform: {platform}.", nameof(platform))
            };
        }

        private static string MapAdoptiumArchitecture(JavaArchitecture architecture)
        {
            return architecture switch
            {
                JavaArchitecture.X64 => "x64",
                JavaArchitecture.Arm64 => "aarch64",
                _ => throw new ArgumentException($"Unsupported Java architecture: {architecture}.", nameof(architecture))
            };
        }

        private static string MapAdoptiumImageType(JavaPackageType packageType)
        {
            return packageType switch
            {
                JavaPackageType.JRE => "jre",
                JavaPackageType.JDK => "jdk",
                _ => throw new ArgumentException($"Unsupported Java package type: {packageType}.", nameof(packageType))
            };
        }

        private static bool IsPortablePackage(string? fileName, JavaPlatform platform)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var lower = fileName.Trim().ToLowerInvariant();

            return platform switch
            {
                JavaPlatform.Windows => lower.EndsWith(".zip", StringComparison.Ordinal),
                JavaPlatform.Linux => lower.EndsWith(".tar.gz", StringComparison.Ordinal),
                JavaPlatform.MacOS => lower.EndsWith(".tar.gz", StringComparison.Ordinal)
                    || lower.EndsWith(".zip", StringComparison.Ordinal),
                _ => false
            };
        }

        // ==================== Zulu ====================

        private static string BuildZuluMetadataUrl(
            int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            var archiveType = platform switch
            {
                JavaPlatform.Windows => "zip",
                JavaPlatform.Linux => "tar.gz",
                JavaPlatform.MacOS => "zip",
                _ => throw new ArgumentException($"Unsupported Java platform: {platform}.", nameof(platform))
            };

            var os = platform switch
            {
                JavaPlatform.Windows => "windows",
                JavaPlatform.Linux => "linux",
                JavaPlatform.MacOS => "macos",
                _ => throw new ArgumentException($"Unsupported Java platform: {platform}.", nameof(platform))
            };

            var arch = architecture switch
            {
                JavaArchitecture.X64 => "x86_64",
                JavaArchitecture.Arm64 => "arm64",
                _ => throw new ArgumentException($"Unsupported Java architecture: {architecture}.", nameof(architecture))
            };

            var javaPackageType = packageType switch
            {
                JavaPackageType.JRE => "jre",
                JavaPackageType.JDK => "jdk",
                _ => throw new ArgumentException($"Unsupported Java package type: {packageType}.", nameof(packageType))
            };

            return $"https://api.azul.com/metadata/v1/zulu/packages/?java_version={majorVersion}&os={os}&arch={arch}&archive_type={Uri.EscapeDataString(archiveType)}&java_package_type={javaPackageType}&release_status=ga&availability_types=CA&latest=true&page=1&page_size=20";
        }

        private async Task<JavaPackageInfo?> GetLatestFromZuluAsync(
            int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            try
            {
                var url = BuildZuluMetadataUrl(majorVersion, platform, architecture, packageType);
                var json = await _http.GetStringAsync(url);
                return ParseZuluResponse(json, majorVersion, platform, architecture, packageType);
            }
            catch
            {
                return null;
            }
        }

        private static JavaPackageInfo? ParseZuluResponse(
            string json,
            int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            var packages = JsonNode.Parse(json)!.AsArray();
            var matched = packages
                .Where(package => IsMatchingZuluPackage(package!, platform) && HasZuluOrderingFields(package!))
                .OrderByDescending(package => ToComparableVersion(package!["java_version"] as JsonArray))
                .ThenByDescending(package => ToComparableVersion(package!["distro_version"] as JsonArray))
                .ThenByDescending(package => package!["openjdk_build_number"]?.GetValue<int>() ?? int.MinValue)
                .FirstOrDefault();

            if (matched is null) return null;

            var javaVersion = matched["java_version"] is JsonArray versionParts
                ? string.Join('.', versionParts.Select(part => part!.ToString()))
                : string.Empty;

            return new JavaPackageInfo
            {
                MajorVersion = majorVersion,
                FullVersion = javaVersion,
                Build = matched["openjdk_build_number"]?.ToString() ?? string.Empty,
                Platform = platform,
                Architecture = architecture,
                PackageType = packageType,
                Source = JavaDownloadSource.Zulu,
                FileName = matched["name"]?.ToString() ?? string.Empty,
                DownloadUrl = matched["download_url"]?.ToString() ?? string.Empty,
                Sha256 = string.Empty,
                Size = null
            };
        }

        private static bool IsMatchingZuluPackage(JsonNode package, JavaPlatform platform)
        {
            var fileName = package["name"]?.ToString();
            if (!IsPortablePackage(fileName, platform))
                return false;

            var lower = fileName!.ToLowerInvariant();
            return !lower.Contains("-fx-", StringComparison.Ordinal)
                && !lower.Contains("-crac-", StringComparison.Ordinal)
                && !lower.Contains("_musl_", StringComparison.Ordinal);
        }

        private static bool HasZuluOrderingFields(JsonNode package)
        {
            return package["java_version"] is JsonArray { Count: > 0 }
                && package["distro_version"] is JsonArray { Count: > 0 }
                && !string.IsNullOrWhiteSpace(package["download_url"]?.ToString());
        }

        private static string ToComparableVersion(JsonArray? versionParts)
        {
            if (versionParts is null || versionParts.Count == 0)
                return string.Empty;

            return string.Join('.', versionParts.Select(part => $"{part!.GetValue<int>():D8}"));
        }

        // ==================== BMCLAPI ====================

        private async Task<JavaPackageInfo?> GetLatestFromBmclapiAsync(
            int majorVersion,
            JavaPlatform platform,
            JavaArchitecture architecture,
            JavaPackageType packageType)
        {
            try
            {
                var url = "https://bmclapi2.bangbang93.com/java/list";
                using var response = await _http.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    return null;

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var token = JsonNode.Parse(json)!;
                var packages = token switch
                {
                    JsonArray arr => arr,
                    JsonObject obj => obj["body"] as JsonArray,
                    _ => null
                };

                if (packages is null) return null;

                var hasDocumentedShape = packages.Any(pkg =>
                    !string.IsNullOrWhiteSpace(pkg?["title"]?.ToString())
                    && !string.IsNullOrWhiteSpace(pkg["file"]?.ToString()));

                if (!hasDocumentedShape) return null;

                return null;
            }
            catch
            {
                return null;
            }
        }

        #region 辅助方法

        private Task<List<JavaResult>> SearchQuick(JavaSearchOptions options)
        {
            var results = new ConcurrentBag<JavaResult>();
            var discoveredPaths = new ConcurrentDictionary<string, bool>();

            SearchEnvironmentVariables(results, discoveredPaths, options);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                SearchRegistry(results, discoveredPaths, options);

            SearchHighPriorityPaths(results, discoveredPaths, options);

            if (!string.IsNullOrEmpty(options.GameDir))
                SearchMinecraftRuntime(options.GameDir, results, discoveredPaths, options);

            SearchPathEnvironment(results, discoveredPaths, options);

            return Task.FromResult(ProcessResults(results, options));
        }

        private Task<List<JavaResult>> SearchDeep(JavaSearchOptions options)
        {
            var results = new ConcurrentBag<JavaResult>();
            var discoveredPaths = new ConcurrentDictionary<string, bool>();

            var quickResults = SearchQuick(options).Result;
            foreach (var java in quickResults)
            {
                discoveredPaths[Path.GetFullPath(java.Path)] = true;
                results.Add(java);
            }

            var drives = GetValidDrives(options.IncludeNetworkDrives);
            Parallel.ForEach(drives, new ParallelOptions { MaxDegreeOfParallelism = 4 }, drive =>
            {
                try
                {
                    BreadthFirstSearch(drive, results, discoveredPaths, options, ExcludedPaths);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"扫描驱动器 {drive} 失败: {ex.Message}");
                }
            });

            return Task.FromResult(ProcessResults(results, options));
        }
        private Task<List<JavaResult>> SearchCustom(JavaSearchOptions options)
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
        

        private int GetRequireMajroVersion(CompleteVersionMetadata metadata)
        {
            return metadata.JavaVersion?.MajorVersion ?? throw new InvalidOperationException("JavaVersion metadata missing");
        }

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
            catch
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
