using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Exceptions;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.ParamsMeta;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Services;
using Qomicex.Core.AOT.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services
{
    internal sealed class LaunchExecutor:ILaunchExecutor
    {
        private string _launchName;
        private string _gameDir;
        public LaunchExecutor(string launchName, string gameDir)
        {
            _launchName = launchName;
            _gameDir = gameDir;
        }

        public Task<LaunchResult> LaunchAsync(LaunchOptions launchOptions)
        {
            return Task.Run(() =>
            {
                var locator = new DefaultVersionLocator(_gameDir);
                var meta = locator.GetVersionMetadata(launchOptions.Version);

                try
                {
                    // 解压 natives
                    UnzipNatives(launchOptions);

                    // 拼接参数
                    string paramsStr = SelectParams(launchOptions);

                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = NormalizeArg(launchOptions.JavaOptions.JavaPath),
                            Arguments = paramsStr,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        },
                        EnableRaisingEvents = true
                    };

                    process.Start();

                    var result = new LaunchResult
                    {
                        Success = true,
                        ProcessId = process.Id,
                        Message = $"进程已启动: {process.Id}",
                        Exception = null,
                        OnOutput = line => Trace.WriteLine($"[OUT] {line}"),
                        OnError = line => Trace.WriteLine($"[ERR] {line}"),
                        OnExit = code => Trace.WriteLine($"进程退出，代码: {code}")
                    };

                    process.OutputDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            result.OnOutput?.Invoke(e.Data);
                    };

                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            result.OnError?.Invoke(e.Data);
                    };

                    process.Exited += (_, _) =>
                    {
                        result.OnExit?.Invoke(process.ExitCode);
                        process.Dispose();
                    };

                    
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    return result;
                }
                catch (Exception ex)
                {
                    return new LaunchResult
                    {
                        Success = false,
                        ProcessId = -1,
                        Message = $"启动失败,{ex.Message}",
                        Exception = ex
                    };
                }
            });
        }

        public Task<bool> KillAsync(int processId)
        {
            return Task.Run(() =>
            {
                try
                {
                    var process = Process.GetProcessById(processId);
                    if (!process.HasExited)
                    {
                        process.Kill(true); // true 表示杀掉整个进程树
                        process.WaitForExit(); // 可选，确保退出
                    }
                    return true;
                }
                catch (ArgumentException)
                {
                    // 进程不存在
                    return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"KillAsync 出错: {ex.Message}");
                    return false;
                }
            });
        }

        private bool UnzipNatives(LaunchOptions options)
        {
            var natives = GetNatives(options);
            string nativesDir = Path.Combine(_gameDir, "versions", options.Version, $"{options.Version}-natives");

            if (natives.Count != 0)
            {
                // 逐个解压natives JAR到natives目录（保留JAR内部目录结构）
                foreach (var native in natives)
                {
                    string zipFilePath = Path.Combine(_gameDir, "libraries", LibHelper.MavenToPath(native.Name));
                    if (System.IO.File.Exists(zipFilePath))
                    {
                        FileHelper.Unzip(zipFilePath, nativesDir);
                        Trace.WriteLine($"已解压Natives: {native.Name}");
                    }
                    else
                    {
                        Trace.WriteLine($"Natives文件不存在: {zipFilePath}");
                    }
                }
            }
            else
            {
                Trace.WriteLine("没有需要解压的Natives文件");
            }

            // 同时解压到版本JSON中 java.library.path 指定的子目录（如果有）
            string javaLibDir = ParseJavaLibraryPath(options, nativesDir);
            if (!string.IsNullOrEmpty(javaLibDir) && javaLibDir != nativesDir)
            {
                // 清空后重新解压，避免上次残留（如错误架构）的库因“不覆盖”逻辑而无法被正确库替换
                if (Directory.Exists(javaLibDir))
                {
                    try { Directory.Delete(javaLibDir, true); } catch { }
                }
                Directory.CreateDirectory(javaLibDir);
                foreach (var native in natives)
                {
                    string zipFilePath = Path.Combine(_gameDir, "libraries", LibHelper.MavenToPath(native.Name));
                    if (System.IO.File.Exists(zipFilePath))
                    {
                        FileHelper.Unzip(zipFilePath, javaLibDir);
                    }
                }
                // 扁平化 java.library.path 子目录中的原生库文件
                string keepExt = OperatingSystem.IsWindows() ? ".dll" : OperatingSystem.IsMacOS() ? ".dylib" : ".so";
                FlattenNatives(javaLibDir, keepExt);
                Trace.WriteLine($"已解压Natives到java.library.path子目录: {javaLibDir}");
            }

            return true;
        }

        /// <summary>
        /// 判断目录名是否为“非当前主机架构”的架构目录。
        /// 新版 LWJGL natives jar 内按架构分目录打包（如 windows/x64、windows/arm64、windows/x86），
        /// 需要跳过非本机架构目录，否则扁平化时会把错误架构的库放入 java.library.path 导致无法加载。
        /// </summary>
        private static bool IsForeignArchDir(string name)
        {
            // 当前主机架构的所有别名
            HashSet<string> hostAliases = SystemHelper.GetArch() switch
            {
                "x64" => new(StringComparer.OrdinalIgnoreCase) { "x64", "x86_64", "x86-64", "amd64" },
                "arm64" => new(StringComparer.OrdinalIgnoreCase) { "arm64", "aarch64" },
                "x86" => new(StringComparer.OrdinalIgnoreCase) { "x86", "i386", "i686" },
                _ => new(StringComparer.OrdinalIgnoreCase)
            };

            // 所有已知架构目录名
            HashSet<string> knownArch = new(StringComparer.OrdinalIgnoreCase)
            {
                "x64", "x86_64", "x86-64", "amd64",
                "arm64", "aarch64",
                "x86", "i386", "i686",
                "arm", "arm32"
            };

            // 是已知架构目录，但不属于当前主机架构 → 视为异构目录，需跳过
            return knownArch.Contains(name) && !hostAliases.Contains(name);
        }

        /// <summary>
        /// 将嵌套目录中的原生库文件（.so/.dll/.dylib）扁平化到其所在子目录的根
        /// </summary>
        private static void FlattenNatives(string dir, string keepExt)
        {
            if (!Directory.Exists(dir))
                return;

            // 递归遍历所有子目录（跳过非当前主机架构的目录，避免错误架构的库覆盖正确架构）
            foreach (string subDir in Directory.GetDirectories(dir))
            {
                if (IsForeignArchDir(Path.GetFileName(subDir)))
                    continue;
                FlattenNatives(subDir, keepExt);
            }

            // 将当前目录子目录中的原生库文件移动到当前目录
            foreach (string subDir in Directory.GetDirectories(dir))
            {
                if (IsForeignArchDir(Path.GetFileName(subDir)))
                    continue;
                foreach (string filePath in Directory.GetFiles(subDir))
                {
                    string ext = Path.GetExtension(filePath);
                    if (string.Equals(ext, keepExt, StringComparison.OrdinalIgnoreCase))
                    {
                        string destPath = Path.Combine(dir, Path.GetFileName(filePath));
                        if (!System.IO.File.Exists(destPath))
                        {
                            File.Move(filePath, destPath);
                        }
                    }
                }
                // 如果子目录为空则删除
                if (Directory.GetFileSystemEntries(subDir).Length == 0)
                {
                    try { Directory.Delete(subDir); } catch { }
                }
            }
        }
        private string ParseJavaLibraryPath(LaunchOptions options, string nativesDir)
        {
            var jvms = GetJVMParams(options);
            foreach (var jvm in jvms)
            {
                if(jvm.Contains("java.library.path", StringComparison.OrdinalIgnoreCase))
                {
                    int eqIdx = jvm.IndexOf('=');
                    if (eqIdx >= 0)
                    {
                        string libPath = jvm.Substring(eqIdx + 1).Trim();
                        libPath = libPath.Replace("${natives_directory}", nativesDir);
                        if (!string.IsNullOrEmpty(libPath) && libPath != nativesDir)
                            return libPath;
                    }
                }
            }
            return "";
        }

        private List<Library> GetNatives(LaunchOptions options)
        {
            List<Library> LibList = new List<Library>();

            var locator = new DefaultVersionLocator(_gameDir);
            var meta = locator.GetVersionMetadata(options.Version);

            foreach (var lib in meta.Libraries)
            {
                if (lib.Rules is { Count: > 0 })
                {
                    foreach (var rule in lib.Rules)
                    {
                        if (LibHelper.IsRuleSuitable(rule))
                        {
                            if (LibHelper.IsNatives(lib))
                                if (!LibList.Contains(lib))
                                    LibList.Add(lib);
                        }
                    }
                }
                else
                {
                    if (LibHelper.IsNatives(lib))
                        if (!LibList.Contains(lib))
                            LibList.Add(lib);
                }
            }

            if (!string.IsNullOrEmpty(meta.InheritsFrom))
                LibList.AddRange(GetNatives(options with { Version = meta.InheritsFrom }));

            return LibHelper.CheckLibsVer(LibList);
        }

        private string SelectParams(LaunchOptions options)
        {
            List<string> paramList = new List<string>();
            Config? config = ParseGameJson(options);

            //拼接JVM
            paramList.AddRange(GetJVMParams(options));

            //拼接mainClass
            paramList.Add(GetMainClass(options));

            //拼接Game参数
            paramList.AddRange(GetGameParams(options));

            //处理参数
            // 获取 assetIndex
            string assetsIndex = config!.AssetIndex.Id;
            if(string.IsNullOrEmpty(assetsIndex))
            {
                if (config!.InheritsFrom is null)
                    throw new ParamsException("获取AssetIndex错误");
                var inheritsFromConfig = ParseGameJson(options with { Version = config!.InheritsFrom });
                assetsIndex = inheritsFromConfig!.AssetIndex.Id;
            }
            //处理账户
            string loginMode = "Legacy";
            if (options.AuthOptions.Mode != AuthMode.Offline)
                loginMode = "Microsoft";
            if (loginMode == "Legacy")
            {
                var auth = options.AuthOptions with { Uuid = NameToUuid(options.AuthOptions.Name) };
                options = options with { AuthOptions = auth };
            }
            //处理ClassPath
            var cpLibs = GetClassPath(options);
            string mainJarPath = Path.Combine(_gameDir, "versions", options.Version, $"{options.Version}.jar");
            if (!File.Exists(mainJarPath))
            {
                mainJarPath = Path.Combine(_gameDir, "versions", config!.InheritsFrom, $"{config!.InheritsFrom}.jar");
            }
            string cpLibsStr = "";
            var sb = new StringBuilder();
            foreach (var cp in cpLibs)
            {
                var path = Path.Combine(_gameDir, "libraries", $"{LibHelper.MavenToPath(cp.Name)}{SystemHelper.GetSeparator()}");
                sb.Append(path);
            }
            sb.Append(mainJarPath);
            cpLibsStr = sb.ToString();

            // 处理版本隔离路径
            var gameVersionDir = string.Empty;
            if (options.VersionIsolation)
            {
                gameVersionDir = Path.Combine(_gameDir, "versions", options.Version);
            }
            else
            {
                gameVersionDir = _gameDir;
            }

            // 处理OptiFine与Forge兼容
            if (paramList.Contains("optifine.OptiFineTweaker"))
            {
                int index = paramList.IndexOf("optifine.OptiFineTweaker");
                paramList.RemoveAt(index);
                paramList.RemoveAt(index - 1);
                paramList.Add("--tweakClass");
                paramList.Add("optifine.OptiFineTweaker");
            }
            else if (paramList.Contains("optifine.OptiFineForgeTweaker"))
            {
                int index = paramList.IndexOf("optifine.OptiFineForgeTweaker");
                paramList.RemoveAt(index);
                paramList.RemoveAt(index - 1);
                paramList.Add("--tweakClass");
                paramList.Add("optifine.OptiFineForgeTweaker");
            }

            // 替换参数
            string paramString = string.Join(" ", paramList);
            paramString = paramString.Replace("${max_memory}", options.JavaOptions.MaxMemoryMB.ToString())
                .Replace("${natives_directory}", NormalizeArg(FileHelper.FormatDirPath(Path.Combine(_gameDir, "versions", options.Version, $"{options.Version}-natives").TrimEnd(SystemHelper.GetSeparator()).ToString())))
                .Replace("${launcher_name}", NormalizeArg(_launchName))
                .Replace("${classpath_separator}", SystemHelper.GetSeparator())
                .Replace("${game_assets}", NormalizeArg(FileHelper.FormatDirPath(Path.Combine(_gameDir, "assets").TrimEnd(SystemHelper.GetSeparator()).ToString())))
                .Replace("${uuid}", options.AuthOptions.Uuid)
                .Replace("${user_properties}", "{}")
                .Replace("${version_type}", NormalizeArg(_launchName))
                .Replace("${user_type}", loginMode)
                .Replace("${auth_access_token}", NormalizeArg(options.AuthOptions.AccessToken))
                .Replace("${assets_index_name}", NormalizeArg(assetsIndex))
                .Replace("${assets_root}", FileHelper.FormatDirPath(Path.Combine(_gameDir, "assets")))
                .Replace("${classpath}", NormalizeArg(cpLibsStr))
                .Replace("${game_directory}", NormalizeArg(FileHelper.FormatDirPath(gameVersionDir.TrimEnd(SystemHelper.GetSeparator()).ToString())))
                .Replace("${version_name}", $"\"{options.Version}\"")
                .Replace("${auth_uuid}", options.AuthOptions.Uuid)
                .Replace("${auth_player_name}", options.AuthOptions.Name)
                .Replace("${library_directory}", FileHelper.FormatDirPath(Path.Combine(_gameDir, "libraries").TrimEnd(SystemHelper.GetSeparator()).ToString()))
                .Replace("${launcher_version}", "23")
                .Replace("${authlib_injector_param}", options.AuthOptions.AuthlibInjectorParam);
            return paramString;
        }

        private List<Library> GetClassPath(LaunchOptions options)
        {
            List<Library> LibList = new List<Library>();

            var locator = new DefaultVersionLocator(_gameDir);
            var meta = locator.GetVersionMetadata(options.Version);

            foreach(var lib in meta.Libraries)
            {
                if (lib.Rules is { Count: > 0 })
                {
                    foreach (var rule in lib.Rules)
                    {
                        if (LibHelper.IsRuleSuitable(rule))
                        {
                            if (LibHelper.IsClassPath(lib))
                                if (!LibList.Contains(lib))
                                    LibList.Add(lib);
                        }
                    }
                }
                else
                {
                    if (LibHelper.IsClassPath(lib))
                        if(!LibList.Contains(lib))
                            LibList.Add(lib);
                }
            }

            if (!string.IsNullOrEmpty(meta.InheritsFrom))
                LibList.AddRange(GetClassPath(options with { Version = meta.InheritsFrom }));

            return LibHelper.CheckLibsVer(LibList);
        }

        public static string NameToUuid(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;
            using (MD5 md5 = MD5.Create())
            {
                string input = $"OfflinePlayer:{name}";
                byte[] hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
                string md5Str = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                string bit6 = ((Convert.ToByte(md5Str.Substring(12, 2), 16) & 15) | 48).ToString("x2");
                string bit8 = ((Convert.ToByte(md5Str.Substring(16, 2), 16) & 63) | 128).ToString("x2");

                string uuid = md5Str.Substring(0, 12) + bit6 + md5Str.Substring(14, 2) + bit8 + md5Str.Substring(18);

                return uuid;
            }
        }

        private string GetMainClass(LaunchOptions options)
        {
            var locator = new DefaultVersionLocator(_gameDir);
            var meta = locator.GetVersionMetadata(options.Version);

            string mainClass = meta?.MainClass;
            if (string.IsNullOrEmpty(mainClass))
            {
                if (!string.IsNullOrEmpty(meta!.InheritsFrom))
                    mainClass = GetMainClass(options with { Version = meta!.InheritsFrom });
                else
                    throw new ParamsException("MainClass键不存在");
            }
            return mainClass;
        }

        private Config ParseGameJson(LaunchOptions options)
        {
            string json = File.ReadAllText(Path.Combine(_gameDir, "versions", options.Version, $"{options.Version}.json"));

            var jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = ParamsJsonContent.Default,
                WriteIndented = true
            };

            Config? config = JsonSerializer.Deserialize<Config>(json, jsonOptions);

            if (config is null)
            {
                throw new ParamsException("版本Json解析失败");
            }

            return config;
        }

        private List<string> GetJVMParams(LaunchOptions options)
        {
            return GetJVMParams(options, true);
        }

        private List<string> GetJVMParams(LaunchOptions options,bool addDefaultParams)
        {
            var jvmList = new List<string>();
            if (string.IsNullOrEmpty(options.Version))
            {
                throw new ArgumentException("Version cannot be null or empty.", nameof(options.Version));
            }

            string json = File.ReadAllText(Path.Combine(_gameDir, "versions", options.Version, $"{options.Version}.json"));

            var jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = ParamsJsonContent.Default,
                WriteIndented = true
            };

            Config? config = JsonSerializer.Deserialize<Config>(json, jsonOptions);

            if (config is null)
            {
                throw new ParamsException("版本Json解析失败");
            }

            if (addDefaultParams)
            {
                //添加默认参数
                jvmList.Add("-XX:+UseG1GC");
                jvmList.Add("-XX:-UseAdaptiveSizePolicy");
                jvmList.Add("-XX:-OmitStackTraceInFastThrow");
                jvmList.Add("-Dfml.ignoreInvalidMinecraftCertificates=True");
                jvmList.Add("-Dfml.ignorePatchDiscrepancies=True");
                jvmList.Add("-Dlog4j2.formatMsgNoLookups=true");

                if (options.JavaOptions.ExtraJvmArgs is not null)
                {
                    jvmList.AddRange(options.JavaOptions.ExtraJvmArgs);
                }

                // Windows适配
                if (OperatingSystem.IsWindows())
                {
                    System.Version os_ver = Environment.OSVersion.Version;
                    if (os_ver.Major >= 10)
                    {
                        jvmList.Add("-Dos.name=\"Windows 10\"");
                        jvmList.Add("-Dos.version=\"10.0\"");
                    }
                }

                jvmList.Add($"-Dminecraft.launcher.brand=\"{_launchName}\"");
                jvmList.Add("-Dminecraft.launcher.version=23");
            }

            //处理InheritsFrom
            if (!string.IsNullOrEmpty(config!.InheritsFrom))
            {
                var inheritsFromOptions = options with { Version = config.InheritsFrom };

                jvmList.AddRange(GetJVMParams(inheritsFromOptions, false));
            }

            //处理当前json的jvm
            if (config!.Arguments?.Jvm is null)
            {
                //适配老版本Modloader Json (例如LiteLoader)
                jvmList.Add("-Djava.library.path=${natives_directory}");
                jvmList.Add("-cp");
                jvmList.Add("${classpath}");
                jvmList.Add("${authlib_injector_param}");
                jvmList.Add("-Xmn256m");
                jvmList.Add("-Xmx${max_memory}m");
                return jvmList;//旧版兼容
            }

            foreach (var element in config!.Arguments.Jvm)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    bool shouldAdd = false;
                    var entry = JsonSerializer.Deserialize<ParamEntry>(element, jsonOptions);

                    if (entry?.Rules is { Count: > 0 })
                    {
                        foreach (var rule in entry.Rules)
                        {
                            if (LibHelper.IsRuleSuitable(rule))
                            {
                                shouldAdd = true;
                                break;
                            }
                        }
                    }

                    if(shouldAdd)
                    {
                        if (entry?.Value.ValueKind == JsonValueKind.String)
                        {
                            if(!entry.Value.GetString().Contains("-Dos.version=") && !entry.Value.GetString().Contains("-Dos.name="))
                            {
                                jvmList.Add(NormalizeArg(entry.Value.GetString()));
                            }
                        }
                        else if (entry?.Value.ValueKind == JsonValueKind.Array)
                            foreach (var v in entry.Value.EnumerateArray())
                                if (!v.GetString().Contains("-Dos.version=") && !v.GetString().Contains("-Dos.name="))
                                {
                                    jvmList.Add(NormalizeArg(v.GetString()));
                                }
                    }
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    if (!element.GetString().Contains("-Dos.version=") && !element.GetString().Contains("-Dos.name="))
                        jvmList.Add(NormalizeArg(element.GetString()));
                }
            }
            return jvmList;
        }

        private List<string> GetGameParams(LaunchOptions options)
        {
            var gameList = new List<string>();
            if (string.IsNullOrEmpty(options.Version))
            {
                throw new ArgumentException("Version cannot be null or empty.", nameof(options.Version));
            }

            string json = File.ReadAllText(Path.Combine(_gameDir, "versions", options.Version, $"{options.Version}.json"));

            var jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = ParamsJsonContent.Default,
                WriteIndented = true
            };

            Config? config = JsonSerializer.Deserialize<Config>(json, jsonOptions);

            if (config is null)
            {
                throw new ParamsException("版本Json解析失败");
            }

            //处理InheritsFrom
            if (!string.IsNullOrEmpty(config!.InheritsFrom))
            {
                var inheritsFromOptions = options with { Version = config.InheritsFrom };

                gameList.AddRange(GetJVMParams(inheritsFromOptions, false));
            }
            if (config!.Arguments is null)
            {
                gameList.AddRange(config!.MinecraftArguments.Split(' '));
                return gameList;
            }

            //处理当前json的game
            foreach (var element in config!.Arguments.Game)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var entry = JsonSerializer.Deserialize<ParamEntry>(element, jsonOptions);
                    if (entry?.Rules is { Count: > 0 })
                        continue;

                    if (entry?.Value.ValueKind == JsonValueKind.String)
                        gameList.Add(NormalizeArg(entry.Value.GetString()));
                    else if (entry?.Value.ValueKind == JsonValueKind.Array)
                        foreach (var v in entry.Value.EnumerateArray())
                            gameList.Add(NormalizeArg(v.GetString()));
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
                    gameList.Add(NormalizeArg(element.GetString()));
                }
            }

            if (!string.IsNullOrEmpty(options.JoinServer))
            {
                gameList.Add("--quickPlayMultiplayer");
                gameList.Add(options.JoinServer); 
            }
            if (!string.IsNullOrEmpty(NormalizeArg(options.JoinWorld)))
            {
                gameList.Add("--quickPlaySingleplayer");
                gameList.Add(NormalizeArg(options.JoinWorld));
            }


            return gameList;
        }

        string NormalizeArg(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Trim();
            if (value.Contains(" ") && !value.StartsWith("\"") && !value.EndsWith("\""))
                value = $"\"{value}\"";
            return value;
        }
    }
}
