using System.Security.Cryptography;
using System.Text;

namespace Qomicex.Core.AOT.Utils;

public static class OfflineUuidHelper
{
    public static string GenerateUuid(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{name}"));
        var hex = Convert.ToHexStringLower(hashBytes);

        var bit6 = ((byte)(Convert.ToByte(hex.Substring(12, 2), 16) & 0x0F | 0x30)).ToString("x2");
        var bit8 = ((byte)(Convert.ToByte(hex.Substring(16, 2), 16) & 0x3F | 0x80)).ToString("x2");

        return hex.Substring(0, 12) + bit6 + hex.Substring(14, 2) + bit8 + hex.Substring(18);
    }
}
