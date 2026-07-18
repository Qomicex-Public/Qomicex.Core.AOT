using Qomicex.Core.AOT.Builder;
using Qomicex.Core.AOT.Public.Models;
using Qomicex.Core.AOT.Public.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Qomicex.Core.AOT.Services
{
    internal class InstallerProvider : IInstallerProvider
    {
        private readonly HttpClient _http;
        private readonly DownloadMirror _mirror;
        private readonly DownloadSource _source;

        private class DownloadSource
        {
            public string librariesSource = "https://libraries.minecraft.net/";
            public string mainJarSource = string.Empty;
            public string assetsIndexSource = string.Empty;
            public string assetsSource = "https://resources.download.minecraft.net/";
        }

        private static readonly string ForgeVersionCacheDir = Path.Combine(Path.GetTempPath(), "ForgeVersionCache");

        private DownloadSource SetDownloadSource(DownloadMirror source)
        {
            if (source == DownloadMirror.BMCLAPI)
            {
                return new DownloadSource
                {
                    librariesSource = "https://bmclapi2.bangbang93.com/maven/",
                    mainJarSource = "https://bmclapi2.bangbang93.com/",
                    assetsIndexSource = "https://bmclapi2.bangbang93.com/"
                };
            }
            else
            {
                return new DownloadSource
                {
                    librariesSource = "https://libraries.minecraft.net/",
                    mainJarSource = "https://launcher.mojang.com/",
                    assetsIndexSource = "https://launchermeta.mojang.com/"
                };
            }
        }

        public InstallerProvider(HttpClient http, DownloadMirror mirror)
        {
            _http = http;
            _mirror = mirror;
            _source = SetDownloadSource(mirror);
        }

        public async Task<List<ModLoaderResult>> GetAvailableModLoaders(string gameVersion, ModLoaderType type = ModLoaderType.All)
        {
            if (type == ModLoaderType.All)
            {
                var allLoaders = new List<ModLoaderResult>();
                var forgeTask = WithTimeout(GetForgeVersions(gameVersion));
                var fabricTask = WithTimeout(GetFabricVersions(gameVersion, GetFabricBaseUrl()));
                var neoForgeTask = WithTimeout(
                    _mirror == DownloadMirror.Official
                        ? GetNeoForgeFromOfficialApi(gameVersion)
                        : GetNeoForgeFromBmclApi(gameVersion));
                var quiltTask = WithTimeout(GetQuiltVersions(gameVersion));
                var optifineTask = WithTimeout(GetOptifineVersions(gameVersion));
                var liteloaderTask = WithTimeout(GetLiteloaderVersions(gameVersion));

                await Task.WhenAll(forgeTask, fabricTask, neoForgeTask, optifineTask, liteloaderTask, quiltTask);
                allLoaders.AddRange(await forgeTask ?? new List<ModLoaderResult>());
                allLoaders.AddRange(await fabricTask ?? new List<ModLoaderResult>());
                allLoaders.AddRange(await neoForgeTask ?? new List<ModLoaderResult>());
                allLoaders.AddRange(await optifineTask ?? new List<ModLoaderResult>());
                allLoaders.AddRange(await liteloaderTask ?? new List<ModLoaderResult>());
                allLoaders.AddRange(await quiltTask ?? new List<ModLoaderResult>());
                return allLoaders.OrderByDescending(l => l.Version, new VersionComparer()).ToList();
            }

            return type switch
            {
                ModLoaderType.Forge => (await GetForgeVersions(gameVersion)).OrderByDescending(l => l.Version, new VersionComparer()).ToList(),
                ModLoaderType.Fabric => (await GetFabricVersions(gameVersion, GetFabricBaseUrl())).OrderByDescending(l => l.Version, new VersionComparer()).ToList(),
                ModLoaderType.Quilt => (await GetQuiltVersions(gameVersion)).OrderByDescending(l => l.Version, new VersionComparer()).ToList(),
                ModLoaderType.LiteLoader => (await GetLiteloaderVersions(gameVersion)).OrderByDescending(l => l.Version, new VersionComparer()).ToList(),
                ModLoaderType.NeoForge => _mirror == DownloadMirror.Official
                    ? (await GetNeoForgeFromOfficialApi(gameVersion)).OrderByDescending(l => l.Version, new VersionComparer()).ToList()
                    : (await GetNeoForgeFromBmclApi(gameVersion)).OrderByDescending(l => l.Version, new VersionComparer()).ToList(),
                ModLoaderType.OptiFine => (await GetOptifineVersions(gameVersion)).OrderByDescending(l => l.Version, new VersionComparer()).ToList(),
                _ => throw new ArgumentException($"不支持的ModLoader类型: {type}")
            };
        }

        private string GetFabricBaseUrl()
        {
            return _mirror == DownloadMirror.BMCLAPI
                ? "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions"
                : "https://meta.fabricmc.net/v2/versions";
        }

        // ==================== 基础工具方法 ====================

        private static async Task<List<ModLoaderResult>> WithTimeout(Task<List<ModLoaderResult>> task, int timeoutMs = 10000)
        {
            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(task, timeoutTask);
            if (completedTask == timeoutTask)
                return new List<ModLoaderResult>();
            return await task;
        }

        private static List<ModLoaderResult> SortAndDeduplicate(List<ModLoaderResult> versions)
        {
            return versions
                .GroupBy(v => v.Version)
                .Select(g => g.First())
                .OrderByDescending(v => v.Version, new VersionComparer())
                .ToList();
        }

        // ==================== 版本排序 ====================

        private class VersionComparer : IComparer<string>
        {
            public int Compare(string? x, string? y)
            {
                return VersionSortInteger(x!, y!);
            }
        }

        private static int VersionSortInteger(string left, string right)
        {
            if (left == "未知版本" || right == "未知版本")
            {
                if (left == "未知版本" && right != "未知版本") return 1;
                if (left != "未知版本" && right == "未知版本") return -1;
                return 0;
            }

            left = left.ToLowerInvariant().Replace("快照", "snapshot").Replace("预览版", "pre");
            right = right.ToLowerInvariant().Replace("快照", "snapshot").Replace("预览版", "pre");

            var leftParts = Regex.Matches(left, "[a-z]+|[0-9]+").Select(m => m.Value).ToList();
            var rightParts = Regex.Matches(right, "[a-z]+|[0-9]+").Select(m => m.Value).ToList();

            for (int i = 0; ; i++)
            {
                if (i >= leftParts.Count && i >= rightParts.Count)
                    return string.Compare(left, right, StringComparison.Ordinal);

                string lVal = i < leftParts.Count ? leftParts[i] : "-1";
                string rVal = i < rightParts.Count ? rightParts[i] : "-1";
                if (lVal == rVal) continue;

                lVal = ConvertSpecialLabel(lVal);
                rVal = ConvertSpecialLabel(rVal);

                if (!int.TryParse(lVal, out int lNum) || !int.TryParse(rVal, out int rNum))
                    return string.Compare(lVal, rVal, StringComparison.Ordinal);

                if (lNum > rNum) return 1;
                if (lNum < rNum) return -1;
            }
        }

        private static string ConvertSpecialLabel(string label)
        {
            return label switch
            {
                "pre" or "snapshot" => "-3",
                "rc" => "-2",
                "experimental" => "-4",
                _ => label
            };
        }

        // ==================== Minecraft 版本工具方法 ====================

        private static async Task<HashSet<string>> GetSupportedGameVersions(HttpClient client, string gameVersionsUrl)
        {
            var response = await client.GetAsync(gameVersionsUrl);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            return JsonNode.Parse(json)!.AsArray()
                .OfType<JsonObject>()
                .Select(j => j["version"]?.ToString())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToHashSet()!;
        }

        private static string NormalizeMinecraftVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return string.Empty;

            version = version.Trim();
            if (version.StartsWith("1.", StringComparison.Ordinal))
                return version;

            var dashIndex = version.IndexOf('-');
            var baseVersion = dashIndex >= 0 ? version[..dashIndex] : version;
            var parts = baseVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return version;

            return int.TryParse(parts[0], out var major) && major >= 22
                ? $"1.{baseVersion}"
                : version;
        }

        private static IEnumerable<string> GetMinecraftVersionAliases(string version)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(version))
                return aliases;

            aliases.Add(version);
            var normalized = NormalizeMinecraftVersion(version);
            aliases.Add(normalized);

            if (normalized.StartsWith("1.", StringComparison.Ordinal))
                aliases.Add(normalized[2..]);
            else
                aliases.Add($"1.{normalized}");

            return aliases;
        }

        private static bool MatchesMinecraftVersion(string candidateVersion, string requestedVersion)
        {
            if (string.IsNullOrWhiteSpace(candidateVersion) || string.IsNullOrWhiteSpace(requestedVersion))
                return false;

            var candidateAliases = GetMinecraftVersionAliases(candidateVersion);
            var requestedAliases = GetMinecraftVersionAliases(requestedVersion);
            return candidateAliases.Intersect(requestedAliases, StringComparer.OrdinalIgnoreCase).Any();
        }

        private static bool SupportsMinecraftVersion(IEnumerable<string> supportedVersions, string requestedVersion)
        {
            var normalizedSupportedVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var supportedVersion in supportedVersions)
            {
                foreach (var alias in GetMinecraftVersionAliases(supportedVersion))
                    normalizedSupportedVersions.Add(alias);
            }
            return GetMinecraftVersionAliases(requestedVersion).Any(normalizedSupportedVersions.Contains);
        }

        // ==================== Fabric ====================

        private async Task<List<ModLoaderResult>> GetFabricVersions(string minecraftVersion, string baseUrl)
        {
            var versions = new List<ModLoaderResult>();
            try
            {
                var gameVersions = await GetSupportedGameVersions(_http, $"{baseUrl}/game");
                if (!SupportsMinecraftVersion(gameVersions, minecraftVersion))
                {
                    Trace.WriteLine($"Fabric 不支持 MC 版本 {minecraftVersion}");
                    return versions;
                }

                var encodedMcVersion = Uri.EscapeDataString(minecraftVersion);
                var loaderResponse = await _http.GetAsync($"{baseUrl}/loader/{encodedMcVersion}");
                loaderResponse.EnsureSuccessStatusCode();

                var loaderJson = await loaderResponse.Content.ReadAsStringAsync();
                var loaderArray = JsonNode.Parse(loaderJson)!.AsArray();

                foreach (var item in loaderArray.OfType<JsonObject>())
                {
                    var loaderInfo = item["loader"] as JsonObject;
                    if (loaderInfo == null) continue;

                    var loaderVersion = loaderInfo["version"]?.ToString();
                    var isStable = loaderInfo["stable"]?.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) == true;

                    if (string.IsNullOrEmpty(loaderVersion)) continue;

                    versions.Add(new ModLoaderResult(
                        ModLoaderType.Fabric,
                        loaderVersion,
                        minecraftVersion,
                        "API未提供",
                        string.Empty,
                        isStable,
                        DateTimeOffset.MinValue
                    ));
                }
                Trace.WriteLine($"Fabric：成功解析 {versions.Count} 个版本");
            }
            catch (HttpRequestException ex)
            {
                Trace.WriteLine($"Fabric API 请求失败：{ex.StatusCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Fabric API 处理失败：{ex.Message}");
            }
            return SortAndDeduplicate(versions);
        }

        // ==================== Quilt ====================

        private async Task<List<ModLoaderResult>> GetQuiltVersions(string minecraftVersion)
        {
            var versions = new List<ModLoaderResult>();
            var baseUrl = "https://meta.quiltmc.org/v3/versions";
            try
            {
                var gameVersions = await GetSupportedGameVersions(_http, $"{baseUrl}/game");
                if (!SupportsMinecraftVersion(gameVersions, minecraftVersion))
                {
                    Trace.WriteLine($"Quilt 不支持 MC 版本 {minecraftVersion}");
                    return versions;
                }

                JsonArray loaderArray = new JsonArray();
                foreach (var alias in GetMinecraftVersionAliases(minecraftVersion))
                {
                    var encodedMcVersion = Uri.EscapeDataString(alias);
                    var loaderResponse = await _http.GetAsync($"{baseUrl}/loader/{encodedMcVersion}");
                    if (!loaderResponse.IsSuccessStatusCode) continue;

                    var loaderJson = await loaderResponse.Content.ReadAsStringAsync();
                    loaderArray = JsonNode.Parse(loaderJson)!.AsArray();
                    if (loaderArray.Count > 0) break;
                }

                if (loaderArray.Count == 0)
                {
                    var globalLoaderResponse = await _http.GetAsync($"{baseUrl}/loader");
                    globalLoaderResponse.EnsureSuccessStatusCode();
                    var globalLoaderJson = await globalLoaderResponse.Content.ReadAsStringAsync();
                    var globalLoaderItems = JsonNode.Parse(globalLoaderJson)!.AsArray().OfType<JsonObject>();
                    loaderArray = new JsonArray(globalLoaderItems.Where(item =>
                    {
                        var hashedVersion = item["hashed"]?["version"]?.ToString();
                        var intermediaryVersion = item["intermediary"]?["version"]?.ToString();
                        return MatchesMinecraftVersion(hashedVersion ?? string.Empty, minecraftVersion)
                            || MatchesMinecraftVersion(intermediaryVersion ?? string.Empty, minecraftVersion);
                    }).ToArray());
                }

                foreach (var item in loaderArray.OfType<JsonObject>())
                {
                    var loaderInfo = item["loader"] as JsonObject;
                    if (loaderInfo == null) continue;

                    var loaderVersion = loaderInfo["version"]?.ToString();
                    var isStable = loaderInfo["stable"]?.ToString().Equals("true", StringComparison.OrdinalIgnoreCase) == true;

                    if (string.IsNullOrEmpty(loaderVersion)) continue;

                    versions.Add(new ModLoaderResult(
                        ModLoaderType.Quilt,
                        loaderVersion,
                        minecraftVersion,
                        "API未提供",
                        string.Empty,
                        isStable,
                        DateTimeOffset.MinValue
                    ));
                }
                Trace.WriteLine($"Quilt：成功解析 {versions.Count} 个版本");
            }
            catch (HttpRequestException ex)
            {
                Trace.WriteLine($"Quilt API 请求失败：{ex.StatusCode} - {ex.Message}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Quilt API 处理失败：{ex.Message}");
            }
            return SortAndDeduplicate(versions);
        }

        // ==================== Optifine ====================

        private async Task<List<ModLoaderResult>> GetOptifineVersions(string minecraftVersion)
        {
            var result = new List<ModLoaderResult>();
            try
            {
                List<JsonObject> optifineList = new List<JsonObject>();
                foreach (var alias in GetMinecraftVersionAliases(minecraftVersion))
                {
                    try
                    {
                        string url = $"https://bmclapi2.bangbang93.com/optifine/{Uri.EscapeDataString(alias)}";
                        var response = await _http.GetAsync(url);
                        response.EnsureSuccessStatusCode();
                        string json = await response.Content.ReadAsStringAsync();
                        optifineList = JsonNode.Parse(json)!.AsArray().OfType<JsonObject>().ToList();
                        if (optifineList.Count > 0) break;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"Optifine 获取列表失败 (MC版本: {alias}): {ex.Message}");
                    }
                }

                foreach (var info in optifineList)
                {
                    var mcVer = info["mcversion"]?.ToString() ?? string.Empty;
                    var patch = info["patch"]?.ToString() ?? string.Empty;
                    var type = info["type"]?.ToString() ?? string.Empty;
                    var forge = info["forge"]?.ToString() ?? string.Empty;

                    if (string.IsNullOrEmpty(mcVer) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(patch))
                        continue;

                    string downloadUrl = $"https://bmclapi2.bangbang93.com/optifine/{Uri.EscapeDataString(mcVer)}/{type}/{patch}";
                    result.Add(new ModLoaderResult(
                        ModLoaderType.OptiFine,
                        $"{type}-{patch}",
                        mcVer,
                        downloadUrl,
                        string.Empty,
                        forge.Contains("Forge N/A"),
                        DateTimeOffset.MinValue
                    ));
                }
                return SortAndDeduplicate(result);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"OptiFine 版本获取失败: {ex.Message}");
            }
            return result;
        }

        // ==================== LiteLoader ====================

        private async Task<List<ModLoaderResult>> GetLiteloaderVersions(string minecraftVersion)
        {
            var result = new List<ModLoaderResult>();
            try
            {
                if (string.IsNullOrEmpty(minecraftVersion))
                    return result;

                string url = $"https://bmclapi2.bangbang93.com/liteloader/list/?mcversion={Uri.EscapeDataString(minecraftVersion)}";
                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();

                List<JsonObject> liteloaderList = new List<JsonObject>();
                try
                {
                    var array = JsonNode.Parse(json)!.AsArray();
                    liteloaderList = array.OfType<JsonObject>().ToList();
                }
                catch
                {
                    var single = JsonNode.Parse(json) as JsonObject;
                    if (single != null)
                        liteloaderList.Add(single);
                }

                foreach (var info in liteloaderList)
                {
                    var mcVer = info["mcversion"]?.ToString() ?? string.Empty;
                    var version = info["version"]?.ToString() ?? string.Empty;
                    var hash = info["hash"]?.ToString() ?? string.Empty;

                    if (string.IsNullOrEmpty(version)) continue;

                    string downloadUrl = $"https://bmclapi2.bangbang93.com/liteloader/download/?version={Uri.EscapeDataString(version)}";
                    result.Add(new ModLoaderResult(
                        ModLoaderType.LiteLoader,
                        version,
                        mcVer,
                        downloadUrl,
                        hash,
                        true,
                        DateTimeOffset.MinValue
                    ));
                }
                return SortAndDeduplicate(result);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"LiteLoader 版本获取失败: {ex.Message}");
            }
            return result;
        }

        // ==================== NeoForge ====================

        private async Task<List<ModLoaderResult>> GetNeoForgeFromOfficialApi(string minecraftVersion)
        {
            const string OLD_URL = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge";
            const string META_URL = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";
            try
            {
                var oldTask = _http.GetAsync(OLD_URL);
                var metaTask = _http.GetAsync(META_URL);
                await Task.WhenAll(oldTask, metaTask);

                var oldResponse = await oldTask;
                var metaResponse = await metaTask;
                oldResponse.EnsureSuccessStatusCode();
                metaResponse.EnsureSuccessStatusCode();

                var oldJson = await oldResponse.Content.ReadAsStringAsync();
                var metaJson = await metaResponse.Content.ReadAsStringAsync();
                var oldObj = JsonNode.Parse(oldJson)!.AsObject();
                var metaObj = JsonNode.Parse(metaJson)!.AsObject();

                var versions = new List<ModLoaderResult>();

                if (MatchesMinecraftVersion("1.20.1", minecraftVersion))
                {
                    var oldVersions = oldObj["versions"]?.AsArray();
                    if (oldVersions != null)
                    {
                        foreach (var v in oldVersions)
                        {
                            var ver = v?.ToString();
                            if (string.IsNullOrEmpty(ver)) continue;
                            versions.Add(new ModLoaderResult(
                                ModLoaderType.NeoForge,
                                ver,
                                "1.20.1",
                                $"https://maven.neoforged.net/releases/net/neoforged/forge/{ver}/forge-{ver}-installer.jar",
                                string.Empty,
                                !ver.Contains("beta", StringComparison.OrdinalIgnoreCase),
                                DateTimeOffset.MinValue
                            ));
                        }
                    }
                }

                var metaVersions = metaObj["versions"]?.AsArray();
                if (metaVersions != null)
                {
                    foreach (var v in metaVersions)
                    {
                        var ver = v?.ToString();
                        if (string.IsNullOrEmpty(ver)) continue;
                        var mcVersion = ParseNeoForgeMinecraftVersion(ver);
                        if (string.IsNullOrEmpty(mcVersion)) continue;
                        if (!string.IsNullOrEmpty(minecraftVersion) && !MatchesMinecraftVersion(mcVersion, minecraftVersion))
                            continue;

                        versions.Add(new ModLoaderResult(
                            ModLoaderType.NeoForge,
                            ver,
                            mcVersion,
                            $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{ver}/neoforge-{ver}-installer.jar",
                            string.Empty,
                            !ver.Contains("beta", StringComparison.OrdinalIgnoreCase),
                            DateTimeOffset.MinValue
                        ));
                    }
                }

                return versions
                    .GroupBy(v => v.Version)
                    .Select(g => g.First())
                    .OrderByDescending(v => v.Version, new VersionComparer())
                    .ToList();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"NeoForge 版本获取失败: {ex.Message}");
                return new List<ModLoaderResult>();
            }
        }

        private async Task<List<ModLoaderResult>> GetNeoForgeFromBmclApi(string minecraftVersion)
        {
            var result = new List<ModLoaderResult>();
            try
            {
                if (string.IsNullOrEmpty(minecraftVersion))
                    return result;

                string url = $"https://bmclapi2.bangbang93.com/neoforge/list/{Uri.EscapeDataString(minecraftVersion)}";
                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                var array = JsonNode.Parse(json)!.AsArray();

                foreach (var item in array.OfType<JsonObject>())
                {
                    var version = item["version"]?.ToString();
                    var mcVersion = item["mcversion"]?.ToString();
                    if (string.IsNullOrEmpty(version) || string.IsNullOrEmpty(mcVersion)) continue;

                    string downloadUrl = $"https://bmclapi2.bangbang93.com/neoforge/version/{Uri.EscapeDataString(version)}/download/installer.jar";
                    result.Add(new ModLoaderResult(
                        ModLoaderType.NeoForge,
                        version,
                        mcVersion,
                        downloadUrl,
                        string.Empty,
                        !version.Contains("-beta") && !version.Contains("-alpha"),
                        DateTimeOffset.MinValue
                    ));
                }
                return result
                    .GroupBy(v => v.Version)
                    .Select(g => g.First())
                    .OrderByDescending(v => v.Version, new VersionComparer())
                    .ToList();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"NeoForge BMCLAPI 版本获取失败: {ex.Message}");
            }
            return result;
        }

        private static string ParseNeoForgeMinecraftVersion(string neoForgeVersion)
        {
            try
            {
                var firstDot = neoForgeVersion.IndexOf('.');
                var secondDot = neoForgeVersion.IndexOf('.', firstDot + 1);
                if (firstDot == -1 || secondDot == -1)
                    return string.Empty;

                var majorVersion = int.Parse(neoForgeVersion[..firstDot]);
                if (majorVersion >= 22)
                    return neoForgeVersion[..secondDot];

                if (majorVersion == 0)
                    return neoForgeVersion[(firstDot + 1)..secondDot];

                var minorVersion = int.Parse(neoForgeVersion[(firstDot + 1)..secondDot]);
                return minorVersion == 0
                    ? $"1.{majorVersion}"
                    : $"1.{majorVersion}.{minorVersion}";
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"解析 NeoForge 版本号失败 {neoForgeVersion}: {ex.Message}");
                return string.Empty;
            }
        }

        // ==================== Forge ====================

        private async Task<List<ModLoaderResult>> GetForgeVersions(string minecraftVersion)
        {
            if (_mirror == DownloadMirror.BMCLAPI)
                return await GetForgeVersionsFromBmclApi(minecraftVersion);
            else
                return await GetForgeVersionsFromOfficialHtml(minecraftVersion);
        }

        private async Task<List<ModLoaderResult>> GetForgeVersionsFromBmclApi(string minecraftVersion)
        {
            var forgeLoaders = new List<ModLoaderResult>();
            try
            {
                string url = $"https://bmclapi2.bangbang93.com/forge/minecraft/{Uri.EscapeDataString(minecraftVersion)}";
                var response = await _http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var versionsArray = JsonNode.Parse(json)!.AsArray();

                    foreach (var version in versionsArray.OfType<JsonObject>())
                    {
                        string apiMcVersion = version["mcversion"]?.ToString() ?? string.Empty;
                        if (!apiMcVersion.Equals(minecraftVersion, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var installerFile = version["files"]?.AsArray().OfType<JsonObject>()
                            .FirstOrDefault(f => f["category"]?.ToString().Equals("installer", StringComparison.OrdinalIgnoreCase) == true);
                        if (installerFile == null) continue;

                        var build = version["build"]?.ToString() ?? string.Empty;
                        forgeLoaders.Add(new ModLoaderResult(
                            ModLoaderType.Forge,
                            version["version"]?.ToString() ?? string.Empty,
                            minecraftVersion,
                            GetForgeDownloadUrl(minecraftVersion, build),
                            installerFile["hash"]?.ToString() ?? string.Empty,
                            IsRecommendedVersion(build, forgeLoaders),
                            version["modified"] != null
                                ? DateTimeOffset.TryParse(version["modified"]!.ToString(), out var dt) ? dt : DateTimeOffset.MinValue
                                : DateTimeOffset.MinValue
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"BMCLAPI JSON 获取 Forge 版本失败: {ex.Message}");
            }
            return forgeLoaders;
        }

        private async Task<List<ModLoaderResult>> GetForgeVersionsFromOfficialHtml(string minecraftVersion)
        {
            var forgeLoaders = new List<ModLoaderResult>();
            var cacheFilePath = GetCacheFilePath(minecraftVersion);
            const int cacheExpiryHours = 24;

            if (File.Exists(cacheFilePath) &&
                (DateTime.Now - File.GetLastWriteTime(cacheFilePath)).TotalHours < cacheExpiryHours)
            {
                try
                {
                    var cachedHtml = File.ReadAllText(cacheFilePath);
                    return ParseForgeVersions(minecraftVersion, cachedHtml);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"使用缓存失败: {ex.Message}，将重新获取");
                }
            }

            var sourceUrls = new List<string>
            {
                $"https://files.minecraftforge.net/net/minecraftforge/forge/index_{minecraftVersion}.html"
            };

            foreach (var url in sourceUrls)
            {
                try
                {
                    var response = await _http.GetAsync(url);
                    if (!response.IsSuccessStatusCode)
                    {
                        Trace.WriteLine($"源 {url} 请求失败: {response.StatusCode}");
                        continue;
                    }

                    var htmlBytes = await response.Content.ReadAsByteArrayAsync();
                    var htmlContent = Encoding.UTF8.GetString(htmlBytes);

                    try
                    {
                        var cacheDir = Path.GetDirectoryName(cacheFilePath);
                        if (!Directory.Exists(cacheDir))
                            Directory.CreateDirectory(cacheDir!);
                        File.WriteAllText(cacheFilePath, htmlContent);
                        Trace.WriteLine($"已缓存html到{cacheFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"缓存写入失败: {ex.Message}");
                    }

                    var result = ParseForgeVersions(minecraftVersion, htmlContent);
                    if (result.Any())
                        return result;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"从源 {url} 提取数据失败: {ex.Message}");
                }
            }

            if (File.Exists(cacheFilePath))
            {
                try
                {
                    var cachedHtml = File.ReadAllText(cacheFilePath);
                    Trace.WriteLine($"读取已缓存html到{cacheFilePath}中的数据");
                    return ParseForgeVersions(minecraftVersion, cachedHtml);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"使用过期缓存失败: {ex.Message}");
                }
            }

            return forgeLoaders;
        }

        private static List<ModLoaderResult> ParseForgeVersions(string minecraftVersion, string htmlContent)
        {
            var forgeLoaders = new List<ModLoaderResult>();

            var tableMatch = Regex.Match(
                htmlContent,
                @"<table[^>]+class=""[^""]*download-list[^""]*""[^>]*>.*?</table>",
                RegexOptions.Singleline
            );

            if (!tableMatch.Success)
            {
                Trace.WriteLine("未找到版本表格");
                return forgeLoaders;
            }

            var rowMatches = Regex.Matches(
                tableMatch.Value,
                @"<tr[^>]*>.*?<td[^>]+class=""[^""]*download-version[^""]*""[^>]*>.*?</tr>",
                RegexOptions.Singleline
            );
            Trace.WriteLine($"找到 {rowMatches.Count} 个版本行");

            foreach (Match rowMatch in rowMatches)
            {
                if (!rowMatch.Success) continue;
                var rowHtml = rowMatch.Value;

                var versionMatch = Regex.Match(
                    rowHtml,
                    @"(?<=<td[^>]+class=""[^""]*download-version[^""]*""[^>]*>\s*)[\d.]+(?:-[a-zA-Z0-9_]+)?(?=\s*<)",
                    RegexOptions.IgnoreCase
                );
                if (!versionMatch.Success) continue;
                var forgeVersion = versionMatch.Value;

                var categoryMatch = Regex.Match(rowHtml, @"classifier-(installer|universal|client)", RegexOptions.IgnoreCase);
                var fileCategory = categoryMatch.Success ? categoryMatch.Groups[1].Value : "installer";

                var urlPattern = $@"href=""([^""]*?forge-(?:{Regex.Escape(minecraftVersion)}|.{Regex.Escape(minecraftVersion)})-{Regex.Escape(forgeVersion)}.*?{fileCategory}\.(jar|zip)[^""]*)""";
                var urlMatch = Regex.Match(rowHtml, urlPattern, RegexOptions.IgnoreCase);
                if (!urlMatch.Success)
                {
                    urlMatch = Regex.Match(rowHtml, @"href=""([^""]*?forge-.*?\.jar[^""]*)""", RegexOptions.IgnoreCase);
                }
                if (!urlMatch.Success) continue;

                var rawDownloadUrl = urlMatch.Groups[1].Value;
                var cleanDownloadUrl = CleanDownloadUrl(rawDownloadUrl);

                var sha1Match = Regex.Match(rowHtml, @"(?i)sha1[:=]\s*([a-f0-9]{40})", RegexOptions.IgnoreCase);
                var sha1 = sha1Match.Success ? sha1Match.Groups[1].Value.Trim() : string.Empty;

                var isRecommended = rowHtml.Contains("promo-recommended", StringComparison.OrdinalIgnoreCase)
                    || rowHtml.Contains("promo-latest", StringComparison.OrdinalIgnoreCase);

                forgeLoaders.Add(new ModLoaderResult(
                    ModLoaderType.Forge,
                    forgeVersion,
                    minecraftVersion,
                    cleanDownloadUrl,
                    sha1,
                    isRecommended,
                    DateTimeOffset.MinValue
                ));
            }

            forgeLoaders.Sort((a, b) => VersionSortInteger(b.Version, a.Version));
            Trace.WriteLine($"最终提取到 {forgeLoaders.Count} 个有效版本");
            return forgeLoaders;
        }

        private static string CleanDownloadUrl(string rawUrl)
        {
            if (rawUrl.Contains("adfoc.us"))
            {
                var decodedUrl = WebUtility.UrlDecode(rawUrl);
                var directUrlMatch = Regex.Match(decodedUrl, @"https://maven\.minecraftforge\.net/.*?\.jar");
                if (directUrlMatch.Success)
                    return directUrlMatch.Value;
            }

            if (!rawUrl.StartsWith("http"))
            {
                return "https://files.minecraftforge.net" +
                       (rawUrl.StartsWith("/") ? "" : "/") +
                       rawUrl;
            }

            return rawUrl;
        }

        private string GetForgeDownloadUrl(string mcVersion, string forgeVersion)
        {
            if (string.IsNullOrEmpty(forgeVersion))
                return string.Empty;
            return _mirror == DownloadMirror.BMCLAPI
                ? $"https://bmclapi2.bangbang93.com/forge/download/{forgeVersion}"
                : $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
        }

        private static bool IsRecommendedVersion(string buildNumber, List<ModLoaderResult> existingLoaders)
        {
            if (!int.TryParse(buildNumber, out int currentBuild))
                return false;
            foreach (var loader in existingLoaders)
            {
                if (int.TryParse(loader.Version.Split('.').LastOrDefault(), out int existingBuild))
                {
                    if (currentBuild <= existingBuild)
                        return false;
                }
            }
            return true;
        }

        private static string GetCacheFilePath(string minecraftVersion)
        {
            return Path.Combine(ForgeVersionCacheDir, $"{minecraftVersion}_forge.html");
        }
    }
}
