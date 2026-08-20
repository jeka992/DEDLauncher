using System.Security.Cryptography;

namespace DedLauncher.Helpers;

public static class UpdateSigning
{
    public static byte[] UpdateSignPubKey => Convert.FromBase64String(
        "MHYwEAYHKoZIzj0CAQYFK4EEACIDYgAE/sYjnZgJJwdQgxq0JUCSzYCTKDWkqFpdHE3a1wjoxQ2ozeplRhbh1nYuyMKnPfa1Wp9Rxqf4B73K83s8QOiJlFjZwZdkwgp3BmdGIwG9R5Og1wQqT1wT+RgGI79J1Y07");

    /// <summary>Проверяет подпись ECDSA P-384 данных.</summary>
    public static bool Verify(byte[] data, string signatureB64)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(UpdateSignPubKey, out _);
            return key.VerifyData(data, Convert.FromBase64String(signatureB64), HashAlgorithmName.SHA384);
        }
        catch { return false; }
    }
}