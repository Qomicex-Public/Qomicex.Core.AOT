using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Exceptions;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.ParamsMeta;
using Qomicex.Core.AOT.Models.VersionMetadata;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Utils;
using Qomicex.Core.AOT.Servicesl;
using System;
using System.Collections.Generic;
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
            string paramsStr = SelectParams(launchOptions);

            //return new LaunchResult();
            return null;//Temp
        }

        public Task<bool> KillAsync(int processId)
        {
            throw new NotImplementedException();
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
            string assetsIndex = config!.AssetIndex;
            if(string.IsNullOrEmpty(assetsIndex))
            {
                if (config!.InheritsFrom is null)
                    throw new ParamsException("获取AssetIndex错误");
                var inheritsFromConfig = ParseGameJson(options with { Version = config!.InheritsFrom });
                assetsIndex = inheritsFromConfig!.AssetIndex;
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
            

            return "";
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

                        }
                    }
                }
                else
                {
                    LibList.Add(lib);
                }
            }

            return LibList;
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
                return jvmList;//旧版兼容

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
            value = value.Trim();
            if (value.Contains(" ") && !value.StartsWith("\"") && !value.EndsWith("\""))
                value = $"\"{value}\"";
            return value;
        }
    }
}
