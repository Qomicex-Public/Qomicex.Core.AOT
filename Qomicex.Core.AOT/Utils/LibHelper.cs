using Qomicex.Core.AOT.Models.VersionMetadata;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Runtime;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Qomicex.Core.AOT.Utils
{
    public sealed class LibHelper
    {
        public static bool IsClassPath(Library library)
        {
            if (library.Downloads is not null)
            {
                if (library.Downloads.Artifact is not null)
                    return true;
            }
            else
            {
                if (library.Natives is null)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsNatives(Library library)
        {
            if (library.Natives is not null)
                return true;
            if (library.Downloads is not null && library.Downloads.Classifiers is not null)
                return true;
            if (library.Name.ToLower().Contains("natives"))
                return true;
            return false;
        }

        public static bool IsRuleSuitable(Models.VersionMetadata.Rule? rule)
        {
            if (rule is null)
            {
                return true;
            }

            if (rule.Action == "allow")
            {
                if (rule.Os is not null && rule.Os.Name is not null)
                {
                    if (SystemHelper.IsOsMatch(rule.Os))
                    {
                        return true;
                    }
                }
                else
                {
                    return true;
                }
            }
            else if (rule.Action == "disallow")
            {
                if (rule.Os is not null && rule.Os.Name is not null)
                {
                    if (SystemHelper.IsOsMatch(rule.Os))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
            return false;
        }

        public static List<Library> CheckLibsVer(List<Library> libs)
        {
            var groupedLibs = libs
                .GroupBy(lib => lib.Name)
                .Select(group =>
                {
                    Library newest = group.First();
                    foreach (var lib in group.Skip(1))
                    {
                        //int cmp = CompareVersionsExtended(lib.Version, newest.Version);
                        int cmp = VersionSortInteger(GetLibVersion(lib), GetLibVersion(newest));
                        if (cmp > 0)
                        {
                            newest = lib; // 替换为更新版本
                        }
                    }
                    return newest;
                });

            return groupedLibs.ToList();
        }

        public static string GetLibVersion(Library library)
        {
            string _fullName = library.Name ?? string.Empty;
            if (string.IsNullOrEmpty(_fullName)) return "";

            string[] temp = _fullName.Split(':');
            if (temp.Length >= 3)
            {
                string _version = temp[2];
                return _version;
            }
            return "";
        }

        private static int VersionSortInteger(string left, string right)
        {
            left = left.ToLowerInvariant();
            right = right.ToLowerInvariant();

            var leftParts = Regex.Matches(left, "[a-z]+|[0-9]+").Select(m => m.Value).ToList();
            var rightParts = Regex.Matches(right, "[a-z]+|[0-9]+").Select(m => m.Value).ToList();

            for (int i = 0; ; i++)
            {
                if (i >= leftParts.Count && i >= rightParts.Count)
                {
                    return string.Compare(left, right, StringComparison.Ordinal);
                }

                string lVal = i < leftParts.Count ? leftParts[i] : "-1";
                string rVal = i < rightParts.Count ? rightParts[i] : "-1";

                if (lVal == rVal) continue;

                lVal = ConvertSpecialLabel(lVal);
                rVal = ConvertSpecialLabel(rVal);

                if (!int.TryParse(lVal, out int lNum) || !int.TryParse(rVal, out int rNum))
                {
                    return string.Compare(lVal, rVal, StringComparison.Ordinal);
                }

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

        private static string RemoveOptionalSuffix(string input)
        {
            int atIndex = input.IndexOf('@');
            return atIndex >= 0 ? input.Substring(0, atIndex) : input;
        }

        public static string MavenToPath(string maven)
        {
            // 防御性检查：坐标为空直接返回
            if (string.IsNullOrWhiteSpace(maven))
            {
                Trace.WriteLine("Maven坐标为空，无法转换路径");
                return string.Empty;
            }

            // 分割坐标（支持格式：group:artifact:version[:classifier[:type]]）
            string[] parts = maven.Split(':');

            // 最少需要3个部分（group:artifact:version）
            if (parts.Length < 3)
            {
                Trace.WriteLine($"无效的Maven坐标格式：{maven}，至少需要3个部分（group:artifact:version）");
                return string.Empty;
            }

            // 提取基础部分（确保不越界）
            string group = RemoveOptionalSuffix(parts[0].Trim());
            string artifact = RemoveOptionalSuffix(parts[1].Trim());
            string version = parts[2].Trim();

            // 处理可选的classifier和type，兼容 classifier@type 与 classifier:type 两种格式
            string classifier = string.Empty;
            string type = "jar";

            // 处理版本号尾部的 @扩展名（如 1.16.5-20210115.111550@zip）
            if (version.Contains('@', StringComparison.Ordinal))
            {
                var verParts = version.Split('@', 2);
                version = verParts[0].Trim();
                type = verParts.Length > 1 && !string.IsNullOrWhiteSpace(verParts[1])
                    ? verParts[1].Trim()
                    : "jar";
            }

            if (parts.Length >= 4)
            {
                var classifierPart = parts[3].Trim();
                if (classifierPart.Contains('@', StringComparison.Ordinal))
                {
                    var classifierParts = classifierPart.Split('@', 2);
                    classifier = classifierParts[0].Trim();
                    type = classifierParts.Length > 1 && !string.IsNullOrWhiteSpace(classifierParts[1])
                        ? classifierParts[1].Trim()
                        : "jar";
                }
                else
                {
                    classifier = classifierPart;
                    if (parts.Length >= 5 && !string.IsNullOrWhiteSpace(parts[4]))
                    {
                        type = parts[4].Trim();
                    }
                }
            }

            // 验证基础部分有效性
            if (string.IsNullOrEmpty(group) || string.IsNullOrEmpty(artifact) || string.IsNullOrEmpty(version))
            {
                Trace.WriteLine($"Maven坐标包含空值：{maven}");
                return string.Empty;
            }

            // 转换 group为路径（com.mumfrey → com/mumfrey）
            string groupPath = group.Replace('.', '/');

            // 构建文件名（artifact-version[-classifier].type）
            string fileName = $"{artifact}-{version}";
            if (!string.IsNullOrEmpty(classifier))
                fileName += $"-{classifier}";
            fileName += $".{type}";

            // 组合完整路径
            return $"{groupPath}/{artifact}/{version}/{fileName}";
        }
    }
    
}
