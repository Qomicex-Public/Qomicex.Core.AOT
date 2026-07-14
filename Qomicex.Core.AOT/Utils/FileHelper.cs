using System;
using System.Collections.Generic;
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
    }
}
