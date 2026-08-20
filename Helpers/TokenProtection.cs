using System.Security.Cryptography;
using System.Text;

namespace DedLauncher.Helpers;

/// <summary>Шифрование токенов через DPAPI (Windows ProtectedData, область CurrentUser).</summary>
public static class TokenProtection
{
    private const string Prefix = "dpapi:";

    // Новая entropy: привязана к MachineName
    private static byte[] EntropyV2 { get; } = SHA256.HashData(
        Encoding.UTF8.GetBytes("DEDLauncher.Token.V2:" + Environment.MachineName));

    // Старая entropy (V1) — для миграции старых файлов
    private static readonly byte[] EntropyV1 = Encoding.UTF8.GetBytes("DEDLauncher.Token.V1");

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain) || IsProtected(plain)) return plain ?? "";
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), EntropyV2, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(enc);
        }
        catch { return plain; }
    }

    public static string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value)) return value ?? "";
        try
        {
            var raw = value.Substring(Prefix.Length);
            var enc = Convert.FromBase64String(raw);
            try
            {
                var bytes = ProtectedData.Unprotect(enc, EntropyV2, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Fallback: пробуем старую V1 entropy (для миграции старых файлов)
                var bytes = ProtectedData.Unprotect(enc, EntropyV1, DataProtectionScope.CurrentUser);
                var plain = Encoding.UTF8.GetString(bytes);
                // Пересохраняем с новой entropy
                return plain;
            }
        }
        catch { return value; }
    }

    private static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
}