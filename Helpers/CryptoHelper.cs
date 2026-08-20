using System.Security.Cryptography;
using System.Text;

namespace DedLauncher.Helpers;

/// <summary>
/// E2E-криптография для чата друзей: подпись ECDSA P-256, обмен ключами ECDH P-256,
/// шифрование AES-256-GCM. Без внешних библиотек — только BCL .NET 8.
/// </summary>
public static class CryptoHelper
{
    public static ECDsa NewSignKey()
        => ECDsa.Create(ECCurve.CreateFromFriendlyName("nistP256"));

    public static ECDiffieHellman NewAgreeKey()
        => ECDiffieHellman.Create(ECCurve.CreateFromFriendlyName("nistP256"));

    public static string ExportSignPub(ECDsa key)
        => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    public static string ExportAgreePub(ECDiffieHellman key)
        => Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

    /// <summary>Общий секрет 32 байта (SHA-256 поверх ECDH) для пары ключей.</summary>
    public static byte[] Derive(ECDiffieHellman own, string peerAgreePubB64)
    {
        using var tmp = ECDiffieHellman.Create();
        tmp.ImportSubjectPublicKeyInfo(Convert.FromBase64String(peerAgreePubB64), out _);
        return own.DeriveKeyFromHash(tmp.PublicKey, HashAlgorithmName.SHA256);
    }

    public static string Sign(ECDsa key, string data)
        => Convert.ToBase64String(key.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA256));

    public static bool Verify(string signPubB64, string data, string sigB64)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(signPubB64), out _);
            return key.VerifyData(Encoding.UTF8.GetBytes(data), Convert.FromBase64String(sigB64), HashAlgorithmName.SHA256);
        }
        catch { return false; }
    }

    public static byte[] RandomNonce()
    {
        var nonce = new byte[12];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }

    /// <summary>Шифрует AES-256-GCM. Возвращает nonce и ciphertext+tag одним куском.</summary>
    public static (byte[] nonce, byte[] cipher) Encrypt(byte[] key, byte[] plain)
    {
        var nonce = RandomNonce();
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var gcm = new AesGcm(key, 16);
        gcm.Encrypt(nonce, plain, cipher, tag);
        var result = new byte[cipher.Length + tag.Length];
        Buffer.BlockCopy(cipher, 0, result, 0, cipher.Length);
        Buffer.BlockCopy(tag, 0, result, cipher.Length, tag.Length);
        return (nonce, result);
    }

    public static byte[] Decrypt(byte[] key, byte[] nonce, byte[] data)
    {
        const int tagLen = 16;
        var cipher = new byte[data.Length - tagLen];
        var tag = new byte[tagLen];
        Buffer.BlockCopy(data, 0, cipher, 0, cipher.Length);
        Buffer.BlockCopy(data, cipher.Length, tag, 0, tagLen);
        var plain = new byte[cipher.Length];
        using var gcm = new AesGcm(key, 16);
        gcm.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    /// <summary>Случайный 256-битный ключ для группового чата.</summary>
    public static byte[] NewGroupKey()
        => RandomNumberGenerator.GetBytes(32);

    /// <summary>Ключ группового чата выводится из кода группы (членство = знание кода). Устарел, используйте NewGroupKey + ECDH-передачу.</summary>
    public static byte[] GroupKey(string code)
        => SHA256.HashData(Encoding.UTF8.GetBytes("ded-grp:" + code.ToUpper()));
}
