using System.Security.Cryptography;
using System.Text;

namespace DedLauncher.Helpers;

/// <summary>Шифрование токенов аккаунта через DPAPI (Windows ProtectedData, область CurrentUser).</summary>
public static class TokenProtection
{
    private const string Prefix = "dpapi:";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DEDLauncher.Token.V1");

    public static string Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain) || IsProtected(plain)) return plain ?? "";
        try
        {
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(enc);
        }
        catch { return plain; }
    }

    public static string Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value) || !IsProtected(value)) return value ?? "";
        try
        {
            var enc = Convert.FromBase64String(value.Substring(Prefix.Length));
            var bytes = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return value; }
    }

    private static bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);
}
