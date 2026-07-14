using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Exceptions;
using Qomicex.Core.AOT.Interfaces;
using Qomicex.Core.AOT.JsonContext;
using Qomicex.Core.AOT.Models.ParamsMeta;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Utils;
using System.Text.Json;

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

            paramList.AddRange(GetJVMParams(options));

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

            paramList.Add(config!.MainClass);

            paramList.AddRange(GetGameParams(options));

            //处理参数


            return "";
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
            }

            //处理InheritsFrom
            if (!string.IsNullOrEmpty(config!.InheritsFrom))
            {
                var inheritsFromOptions = options with { Version = config.InheritsFrom };

                jvmList.AddRange(GetJVMParams(inheritsFromOptions, false));
            }

            //处理当前json的jvm
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
                            if (rule.Action == "allow")
                            {
                                if (rule.Os is not null && rule.Os.Name is not null)
                                {
                                    if (SystemHelper.IsOsMatch(rule.Os))
                                    {
                                        shouldAdd = true;
                                        break;
                                    }
                                }
                                else
                                {
                                    shouldAdd = true;
                                    break;
                                }
                            }
                            else if (rule.Action == "disallow")
                            {
                                if (rule.Os is not null && rule.Os.Name is not null)
                                {
                                    if (SystemHelper.IsOsMatch(rule.Os))
                                    {
                                        break;
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }

                    if(shouldAdd)
                    {
                        if (entry?.Value.ValueKind == JsonValueKind.String)
                            jvmList.Add(NormalizeArg(entry.Value.GetString()));
                        else if (entry?.Value.ValueKind == JsonValueKind.Array)
                            foreach (var v in entry.Value.EnumerateArray())
                                jvmList.Add(NormalizeArg(v.GetString()));
                    }
                }
                else if (element.ValueKind == JsonValueKind.String)
                {
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
