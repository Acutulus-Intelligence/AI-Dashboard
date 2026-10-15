namespace Application.Interfaces;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);

    /// <summary>
    /// Returns <c>true</c> when the value was produced by <see cref="Encrypt"/>.
    /// Used to distinguish encrypted values from legacy plaintext stored at rest.
    /// </summary>
    bool IsEncrypted(string value);
}
