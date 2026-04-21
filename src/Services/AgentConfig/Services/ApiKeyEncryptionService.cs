using System.Security.Cryptography;
using System.Text;

namespace AxonVoiceAI.AgentConfig.Services;

public sealed class ApiKeyEncryptionService
{
    private readonly byte[] _masterKey;

    public ApiKeyEncryptionService(string base64MasterKey)
    {
        var keyBytes = Convert.FromBase64String(base64MasterKey);
        if (keyBytes.Length != 32)
            throw new ArgumentException("PLATFORM_MASTER_KEY must be a 32-byte (256-bit) key encoded as Base64.");
        _masterKey = keyBytes;
    }

    /// <summary>
    /// Encrypts a plaintext Gemini API key using AES-256-GCM.
    /// Returns a Base64-encoded ciphertext string containing the nonce and tag for storage.
    /// </summary>
    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(_masterKey, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Layout: [nonce (12)] + [tag (16)] + [ciphertext (variable)]
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, nonce.Length + tag.Length, ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts a stored AES-256-GCM ciphertext back to the plaintext Gemini API key.
    /// The result must not be logged, stored, or passed as a parameter — use in-memory only.
    /// </summary>
    public string Decrypt(string encryptedBase64)
    {
        var data = Convert.FromBase64String(encryptedBase64);
        const int nonceSize = 12;
        const int tagSize = 16;

        if (data.Length < nonceSize + tagSize)
            throw new CryptographicException("Encrypted data is too short to be valid.");

        var nonce = data[..nonceSize];
        var tag = data[nonceSize..(nonceSize + tagSize)];
        var ciphertext = data[(nonceSize + tagSize)..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_masterKey, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    /// <summary>Returns the last 4 characters of the key for safe display in the dashboard.</summary>
    public static string ExtractHint(string plaintext) =>
        plaintext.Length >= 4 ? plaintext[^4..] : string.Empty;
}
