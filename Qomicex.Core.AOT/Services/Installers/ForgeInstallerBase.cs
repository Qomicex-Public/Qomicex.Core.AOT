using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Text;

namespace Qomicex.Core.AOT.Services.Installers;

internal class ForgeInstallerBase : InstallerBase
{
    internal string BaseUrl = string.Empty;
    internal int SourceId;
    internal string gameDir = string.Empty;
    internal string gameVersion = string.Empty;
    internal string _installerPath = string.Empty;
    internal string _mainJarPath = string.Empty;

    internal string ResolveUrl(string originalUrl)
    {
        var mapping = SourceMappings.FirstOrDefault(m => m.Original == originalUrl);
        return mapping.Default ?? originalUrl;
    }

    internal struct SourcesList
    {
        public string Original;
        public string Default;
    }

    internal List<SourcesList> SourceMappings = [];

    internal async Task RunProcessor(JsonObject ipObj, JsonObject processor, string versionId, string gameDir, string javaPath)
    {
        if (processor == null) return;
        string separator = OperatingSystem.IsWindows() ? ";" : ":";

        string processorJar = processor["jar"]?.ToString() ?? "未知Jar";

        var outputPaths = ReplaceOutputs(ipObj, processor, gameDir);
        foreach (var output in outputPaths)
        {
            string filePath = ResolveProcessorOutputPath(output.Key);
            string fileSha1 = output.Value.Trim('\'');
            if (File.Exists(filePath) && VerifyFileSha1(filePath, fileSha1))
                return;
        }

        var jar = processor["jar"]?.ToString();
        if (string.IsNullOrEmpty(jar))
            throw new Exception("Processor Jar路径未定义");

        var jarParts = jar.Split(':');
        if (jarParts.Length < 3)
            throw new Exception($"Processor Jar格式错误: {jar}");

        string jarPath = ResolveLibraryPath(gameDir, jar);
        if (!File.Exists(jarPath))
        {
            string downloadUrl = $"{BaseUrl}/{jarParts[0].Replace(".", "/")}/{jarParts[1]}/{jarParts[2]}/{jarParts[1]}-{jarParts[2]}.jar";
            await DownloadFileAsync(CreateHttpClient(), downloadUrl, jarPath);
        }

        string cps = string.Empty;
        var classpathArr = processor["classpath"] as JsonArray;
        if (classpathArr != null)
        {
            foreach (var cp in classpathArr)
            {
                string cpStr = cp!.ToString();
                var cpParts = cpStr.Split(':');
                if (cpParts.Length < 3)
                    throw new Exception($"Classpath格式错误: {cpStr}");

                string cpJarPath = ResolveLibraryPath(gameDir, cpStr);
                if (!File.Exists(cpJarPath))
                {
                    string downloadUrl = $"{BaseUrl}/{cpParts[0].Replace(".", "/")}/{cpParts[1]}/{cpParts[2]}/{cpParts[1]}-{cpParts[2]}.jar";
                    await DownloadFileAsync(CreateHttpClient(), downloadUrl, cpJarPath);
                }
                cps += $"{cpJarPath}{separator}";
            }
            cps = cps.TrimEnd(';', ':');
        }

        string args = string.Empty;
        var argsArr = processor["args"] as JsonArray;
        if (argsArr != null)
        {
            foreach (var arg in argsArr)
                args += $"{arg} ";
            args = args.TrimEnd(' ');
            args = ReplaceArguments(ipObj, args);
        }

        var mainClass = GetJarMainClass(jarPath);
        if (string.IsNullOrEmpty(mainClass))
            throw new Exception($"无法获取Jar主类: {jarPath}");

        string command = $"-cp \"{cps}{separator}{jarPath}\" {mainClass} {args}";
        var exitCode = RunInstallProcess(command, javaPath);
        //string command = $"\"{javaPath}\" -cp \"{cps}{separator}{jarPath}\" {mainClass} {args}";
        //var exitCode = RunInstallProcess(command, null);
        if (exitCode != 0)
            throw new Exception($"Processor执行失败，命令: {javaPath} {command}\nExit code:{exitCode}");

        outputPaths = ReplaceOutputs(ipObj, processor, gameDir);
        foreach (var output in outputPaths)
        {
            string filePath = ResolveProcessorOutputPath(output.Key);
            string fileSha1 = output.Value.Trim('\'');
            if (!File.Exists(filePath))
                throw new Exception($"Processor执行失败: 输出文件不存在 - {filePath}");
            if (!VerifyFileSha1(filePath, fileSha1))
                throw new Exception($"输出文件SHA1不匹配: {filePath}");
        }
    }

    internal static bool VerifyFileSha1(string filePath, string expectedHash)
    {
        if (!File.Exists(filePath)) return false;
        using var stream = File.OpenRead(filePath);
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        byte[] hashBytes = sha1.ComputeHash(stream);
        string actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        return actualHash.Trim().Equals(expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal string ResolveLibraryPath(string gameDir, string mavenCoordinate)
    {
        var relativePath = MavenToPath(mavenCoordinate);
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new Exception($"无效的Maven坐标: {mavenCoordinate}");
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(gameDir, "libraries", normalizedRelativePath);
    }

    internal string ResolveProcessorOutputPath(string outputKey)
    {
        if (string.IsNullOrWhiteSpace(outputKey)) return string.Empty;
        if (Path.IsPathRooted(outputKey))
        {
            if (!outputKey.StartsWith(this.gameDir, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Forge处理器输出路径越界: {outputKey}");
            return outputKey;
        }
        var rawKey = outputKey.TrimEnd(']').TrimStart('[');
        var libMavenPath = MavenToPath(rawKey);
        if (string.IsNullOrWhiteSpace(libMavenPath)) return outputKey;
        return Path.Combine(this.gameDir, "libraries", libMavenPath.Replace('/', Path.DirectorySeparatorChar));
    }

    internal Dictionary<string, string> ReplaceOutputs(JsonObject ipObj, JsonObject processor, string gameDir)
    {
        var outputs = new Dictionary<string, string>();
        if (processor["outputs"] == null) return outputs;
        foreach (var output in processor["outputs"]!.AsObject())
        {
            string key = output.Key;
            string value = output.Value!.ToString();
            string replacedStr = ReplaceArguments(ipObj, $"{key}={value}");
            var splitArr = replacedStr.Split('=');
            if (splitArr.Length != 2) continue;
            outputs[splitArr[0]] = splitArr[1];
        }
        return outputs;
    }

    internal string ReplaceArguments(JsonObject ipObj, string args)
    {
        if (ipObj["data"] != null)
        {
            var dataObj = ipObj["data"]!.AsObject();
            foreach (var prop in dataObj)
            {
                var name = prop.Key;
                var value = prop.Value?["client"]?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    string placeholder = $"{{{name}}}";
                    if (args.Contains(placeholder))
                        args = args.Replace(placeholder, NormalizeProcessorValue(value));
                }
            }
        }

        var replacements = new Dictionary<string, string>
        {
            { "{MINECRAFT_VERSION}", this.gameVersion },
            { "{MINECRAFT_JAR}", Path.IsPathRooted(_mainJarPath) ? _mainJarPath : Path.Combine(this.gameDir, _mainJarPath) },
            { "{ROOT}", this.gameDir },
            { "{LIBRARY_DIR}", $"{this.gameDir}/libraries" },
            { "{INSTALLER}", _installerPath },
            { "{SIDE}", "client" }
        };

        foreach (var kvp in replacements)
        {
            if (args.Contains(kvp.Key))
                args = args.Replace(kvp.Key, kvp.Value);
        }

        args = ReplaceInlineMavenCoordinates(args);
        return args;
    }

    internal string NormalizeProcessorValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            var mavenCoordinate = value[1..^1];
            return ResolveLibraryPath(this.gameDir, mavenCoordinate);
        }
        return value.Trim('\'');
    }

    internal string ReplaceInlineMavenCoordinates(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var result = value;
        var matches = Regex.Matches(value, "\\[(.+?)\\]");
        foreach (Match match in matches)
        {
            if (!match.Success) continue;
            var replacement = ResolveLibraryPath(this.gameDir, match.Groups[1].Value);
            result = result.Replace(match.Value, replacement, StringComparison.Ordinal);
        }
        return result;
    }

    internal List<string> ExtractMavenCoordinatesFromProcessors(JsonObject installProfileJson)
    {
        var coordinates = new List<string>();
        var processors = installProfileJson["processors"] as JsonArray;
        if (processors == null) return coordinates;
        foreach (var processor in processors!.OfType<JsonObject>())
        {
            var args = processor["args"] as JsonArray;
            if (args == null) continue;
            foreach (var arg in args)
            {
                var text = arg?.ToString();
                if (string.IsNullOrWhiteSpace(text)) continue;
                var matches = Regex.Matches(text, "\\[(.+?)\\]");
                foreach (Match match in matches)
                {
                    if (match.Success)
                        coordinates.Add(match.Groups[1].Value);
                }
            }
        }
        return coordinates;
    }

    internal bool ShouldRunProcessor(JsonObject processor, string side)
    {
        var sides = processor["sides"] as JsonArray;
        if (sides == null || sides.Count == 0) return true;
        return sides.Select(t => t?.ToString()).Any(v => string.Equals(v, side, StringComparison.OrdinalIgnoreCase));
    }
}
