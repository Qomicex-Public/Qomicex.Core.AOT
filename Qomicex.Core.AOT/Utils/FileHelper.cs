using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Qomicex.Core.AOT.Utils
{
    public sealed class FileHelper
    {
        public static bool ValidateFileHash(string filePath, string expectedHash)
        {
            if (!File.Exists(filePath) || string.IsNullOrEmpty(expectedHash))
                return false;

            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha1.ComputeHash(stream);
            var actualHash = Convert.ToHexString(hash).ToLower();
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatDirPath(string path)
        {
            if (path.Contains(' '))
                return $"\"{path}\"";
            else
                return path;
        }

        public static bool Unzip(string zipFilePath, string targetDir)
        {
            if (!File.Exists(zipFilePath))
                return false; // 压缩包不存在

            // 确保目标目录存在
            Directory.CreateDirectory(targetDir);

            try
            {
                // 解压所有文件
                ZipFile.ExtractToDirectory(zipFilePath, targetDir, overwriteFiles: true);
                return true;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"解压失败：{ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 删除指定目录中除特定后缀外的所有文件（注意：操作有风险，需确保路径正确）
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="keepSuffix"></param>
        /// <returns></returns>
        public static bool DeleteExcept(string folderPath, string keepSuffix)
        {
            if (!Directory.Exists(folderPath))
                return false;

            // 遍历目录中的所有文件和子目录
            foreach (string itemPath in Directory.GetFileSystemEntries(folderPath))
            {
                if (Directory.Exists(itemPath))
                {
                    // 递归清理子目录
                    DeleteExcept(itemPath, keepSuffix);
                    // 若子目录为空则删除
                    if (Directory.GetFileSystemEntries(itemPath).Length == 0)
                        Directory.Delete(itemPath);
                }
                else
                {
                    // 若文件后缀不是需要保留的，则删除
                    if (!Path.GetExtension(itemPath).Equals(keepSuffix, StringComparison.OrdinalIgnoreCase))
                        File.Delete(itemPath);
                }
            }
            return true;
        }
    }
}
