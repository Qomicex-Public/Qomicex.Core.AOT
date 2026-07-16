using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Qomicex.Core.AOT.Services.Installers;

public abstract class InstallerBase
{
    internal static string? DefaultUserAgent { get; set; }

    internal static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        if (!string.IsNullOrEmpty(DefaultUserAgent))
            client.DefaultRequestHeaders.UserAgent.ParseAdd(DefaultUserAgent);
        return client;
    }

    public enum InstallType
    {
        Forge,
        Fabric,
        NeoForge,
        Quilt,
        LiteLoader,
        OptiFine,
    }

    internal static string MergeJson(string mainVersionJson, string mergedVersionJson)
    {
        try
        {
            var mainJson = JsonNode.Parse(mainVersionJson)!.AsObject();
            var mergedJson = JsonNode.Parse(mergedVersionJson)!.AsObject();
            Merge(mainJson, mergedJson);
            return mainJson.ToJsonString();
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static void MergeDirectories(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(sourceDir)) return;
        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            MergeDirectories(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }
    }

    internal static string MergeVersionJson(string mainVersionJson, string mergedVersionJson, string? defaultVersionId)
    {
        var jsonData = MergeJson(mainVersionJson, mergedVersionJson);
        var json = JsonNode.Parse(jsonData)!.AsObject();
        json.Remove("inheritsFrom");
        if (!string.IsNullOrEmpty(defaultVersionId))
            json["id"] = defaultVersionId;
        return json.ToJsonString();
    }

    internal static bool MergeVersion(string mainVersion, string mergedVersion, string gameDir)
    {
        try
        {
            var mainVersionDir = Path.Combine(gameDir, "versions", mainVersion);
            var mergedVersionDir = Path.Combine(gameDir, "versions", mergedVersion);
            var mainVersionJsonPath = Path.Combine(mainVersionDir, $"{mainVersion}.json");
            var mergedVersionJsonPath = Path.Combine(mergedVersionDir, $"{mergedVersion}.json");

            if (!File.Exists(mainVersionJsonPath))
            {
                Trace.WriteLine($"主版本JSON文件不存在：{mainVersionJsonPath}");
                return false;
            }
            if (!File.Exists(mergedVersionJsonPath))
            {
                Trace.WriteLine($"待合并版本JSON文件不存在：{mergedVersionJsonPath}");
                return false;
            }

            var mainJsonContent = File.ReadAllText(mainVersionJsonPath);
            var mergedJsonContent = File.ReadAllText(mergedVersionJsonPath);
            var mergedJsonResult = MergeJson(mainJsonContent, mergedJsonContent);
            var jsonObj = JsonNode.Parse(mergedJsonResult)!.AsObject();
            jsonObj["id"] = mainVersion;
            jsonObj.Remove("inheritsFrom");

            MergeDirectories(mergedVersionDir, mainVersionDir);
            File.WriteAllText(mainVersionJsonPath, jsonObj.ToJsonString());
            return true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"合并版本失败：{ex.Message}");
            return false;
        }
    }

    internal static async Task<bool> DownloadFileAsync(HttpClient client, string url, string destinationPath, int maxRedirects = 5)
    {
        if (maxRedirects <= 0)
            throw new Exception($"超过最大重定向次数（{maxRedirects}次）");

        try
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode is System.Net.HttpStatusCode.MovedPermanently
                or System.Net.HttpStatusCode.Found
                or System.Net.HttpStatusCode.Redirect
                or System.Net.HttpStatusCode.SeeOther
                or System.Net.HttpStatusCode.TemporaryRedirect)
            {
                var redirectUrl = response.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(redirectUrl))
                    throw new Exception($"重定向失败：未返回Location");

                if (!Uri.IsWellFormedUriString(redirectUrl, UriKind.Absolute))
                {
                    if (Uri.TryCreate(new Uri(url), redirectUrl, out var absoluteUri))
                        redirectUrl = absoluteUri.ToString();
                    else
                        throw new Exception($"重定向地址无效");
                }

                return await DownloadFileAsync(client, redirectUrl, destinationPath, maxRedirects - 1);
            }

            response.EnsureSuccessStatusCode();

            var dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
            return true;
        }
        catch (Exception ex)
        {
            throw new Exception($"下载文件失败（{url}）：{ex.Message}");
        }
    }

    internal static string MavenToPath(string maven)
    {
        if (string.IsNullOrWhiteSpace(maven))
        {
            Trace.WriteLine("Maven坐标为空");
            return string.Empty;
        }

        var parts = maven.Split(':');
        if (parts.Length < 3)
        {
            Trace.WriteLine($"无效的Maven坐标格式：{maven}");
            return string.Empty;
        }

        var group = parts[0].Trim();
        var artifact = parts[1].Trim();
        var version = parts[2].Trim();

        var classifier = string.Empty;
        var type = "jar";

        if (version.Contains('@', StringComparison.Ordinal))
        {
            var verParts = version.Split('@', 2);
            version = verParts[0].Trim();
            type = verParts.Length > 1 && !string.IsNullOrWhiteSpace(verParts[1])
                ? verParts[1].Trim() : "jar";
        }

        if (parts.Length >= 4)
        {
            var classifierPart = parts[3].Trim();
            if (classifierPart.Contains('@', StringComparison.Ordinal))
            {
                var cp = classifierPart.Split('@', 2);
                classifier = cp[0].Trim();
                type = cp.Length > 1 && !string.IsNullOrWhiteSpace(cp[1]) ? cp[1].Trim() : "jar";
            }
            else
            {
                classifier = classifierPart;
                if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
                    type = parts[4].Trim();
            }
        }

        if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(artifact) || string.IsNullOrEmpty(version))
        {
            Trace.WriteLine($"Maven坐标包含空值：{maven}");
            return string.Empty;
        }

        var groupPath = group.Replace('.', '/');
        var fileName = $"{artifact}-{version}";
        if (!string.IsNullOrEmpty(classifier))
            fileName += $"-{classifier}";
        fileName += $".{type}";

        return $"{groupPath}/{artifact}/{version}/{fileName}";
    }

    internal static byte[] ReadSpecifyFileFromZip(string path, string fileName)
    {
        using var fileStream = File.OpenRead(path);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            throw new FileNotFoundException($"未找到指定文件 {fileName}", fileName);
        using var entryStream = entry.Open();
        using var memoryStream = new MemoryStream();
        entryStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    internal static string GetJarMainClass(string jarPath)
    {
        var manifestBytes = ReadSpecifyFileFromZip(jarPath, "META-INF/MANIFEST.MF");
        var mf = System.Text.Encoding.UTF8.GetString(manifestBytes);
        if (string.IsNullOrEmpty(mf)) return string.Empty;

        foreach (var line in mf.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("Main-Class: ", StringComparison.OrdinalIgnoreCase))
                return line["Main-Class: ".Length..].Trim();
        }
        return string.Empty;
    }

    internal int RunInstallProcess(string arguments, string program)
    {
        using var process = new Process();

        // 判断平台
        bool isWindows = OperatingSystem.IsWindows();
        bool isLinux = OperatingSystem.IsLinux();
        bool isMacOS = OperatingSystem.IsMacOS();

        if (program == null)
        {
            // 默认执行 shell
            program = isWindows ? "cmd.exe" : (File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh");
        }

        process.StartInfo.FileName = program;

        if (isWindows)
        {
            process.StartInfo.Arguments = program == "cmd.exe" ? $"/c {arguments}" : arguments;
        }
        else
        {
            // Linux/macOS 用 -c
            process.StartInfo.Arguments = program == "/bin/bash" ? $"-c \"{arguments}\"" : arguments;
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        return process.ExitCode;
    }

    private static void Merge(JsonObject target, JsonObject source, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        foreach (var (key, value) in source)
        {
            if (value is JsonObject nestedSource && target.TryGetPropertyValue(key, out var existing) && existing is JsonObject nestedTarget)
                Merge(nestedTarget, nestedSource, comparison);
            else if (value is JsonArray arr)
            {
                if (target.TryGetPropertyValue(key, out var existingArr) && existingArr is JsonArray targetArr)
                {
                    foreach (var item in arr)
                        targetArr.Add(item?.DeepClone());
                }
                else
                    target[key] = arr.DeepClone();
            }
            else
                target[key] = value?.DeepClone();
        }
    }
}
