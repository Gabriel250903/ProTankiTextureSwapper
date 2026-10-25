using System.IO;
using System.Security.Cryptography;

namespace TextureSwapper.Helpers.Utilities
{
    public static class AssetCryptoHelper
    {
        public const string EncryptedExtension = ".enc";
        public const int NormalSize = 12;
        public const int TagSize = 16;
        public const int HeaderSize = NormalSize + TagSize;

        public static bool IsEncryptedAsset(string filePath)
        {
            return !string.IsNullOrEmpty(filePath)
                && filePath.EndsWith(EncryptedExtension, StringComparison.OrdinalIgnoreCase);
        }

        public static byte[] DecryptAsset(byte[] encryptedPayload, byte[] key)
        {
            if (encryptedPayload == null || encryptedPayload.Length < HeaderSize)
            {
                throw new CryptographicException(
                    $"Invalid encrypted payload: minimum size is {HeaderSize} bytes."
                );
            }

            if (key == null || key.Length != 32)
            {
                throw new ArgumentException(
                    "Decryption key must be 32 bytes (256-bit).",
                    nameof(key)
                );
            }

            byte[] nonce = encryptedPayload[..NormalSize];
            byte[] tag = encryptedPayload[NormalSize..HeaderSize];
            byte[] ciphertext = encryptedPayload[HeaderSize..];
            byte[] plaintext = new byte[ciphertext.Length];

            using AesGcm aesGcm = new(key, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

            return plaintext;
        }

        public static byte[] DecryptFile(string filePath, byte[] key)
        {
            byte[] payload = File.ReadAllBytes(filePath);
            return DecryptAsset(payload, key);
        }
    }
}
