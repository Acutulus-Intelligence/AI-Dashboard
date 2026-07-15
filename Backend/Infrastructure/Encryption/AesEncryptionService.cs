using System.Security.Cryptography;
using System.Text;
using Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Infrastructure.Encryption;

public class AesEncryptionService : IEncryptionService
{
    private const string GcmPrefix = "v2:";
    private readonly byte[] _key;

    public AesEncryptionService(IOptions<EncryptionSettings> settings)
    {
        var keyString = settings.Value.Key
            ?? throw new InvalidOperationException("Encryption key is not configured.");

        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
    }

    public string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aesGcm = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var payload = new byte[nonce.Length + cipherBytes.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, nonce.Length, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length + cipherBytes.Length, tag.Length);

        return GcmPrefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string cipherText)
    {
        if (cipherText.StartsWith(GcmPrefix, StringComparison.Ordinal))
            return DecryptGcm(cipherText[GcmPrefix.Length..]);

        return DecryptLegacyCbc(cipherText);
    }

    private string DecryptGcm(string encodedPayload)
    {
        var payload = Convert.FromBase64String(encodedPayload);
        var nonceSize = AesGcm.NonceByteSizes.MaxSize;
        var tagSize = AesGcm.TagByteSizes.MaxSize;

        if (payload.Length <= nonceSize + tagSize)
            throw new CryptographicException("Invalid encrypted payload.");

        var nonce = new byte[nonceSize];
        var tag = new byte[tagSize];
        var cipherBytes = new byte[payload.Length - nonceSize - tagSize];

        Buffer.BlockCopy(payload, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(payload, nonceSize, cipherBytes, 0, cipherBytes.Length);
        Buffer.BlockCopy(payload, nonceSize + cipherBytes.Length, tag, 0, tagSize);

        var plainBytes = new byte[cipherBytes.Length];
        using var aesGcm = new AesGcm(_key, tagSize);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private string DecryptLegacyCbc(string cipherText)
    {
        var fullCipher = Convert.FromBase64String(cipherText);

        using var aes = Aes.Create();
        aes.Key = _key;

        var iv = new byte[aes.IV.Length];
        var cipherBytes = new byte[fullCipher.Length - iv.Length];
        Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
        Buffer.BlockCopy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
