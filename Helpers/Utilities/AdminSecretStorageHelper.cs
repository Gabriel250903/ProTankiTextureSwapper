using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace TextureSwapper.Helpers.Utilities
{
    public static class AdminSecretStorageHelper
    {
        private static readonly byte[] Entropy = "TS_Admin_DPAPI_Entropy_2026_v2"u8.ToArray();
        private static readonly byte[] LegacyEntropy = "TS_Admin_DPAPI_Entropy_2026"u8.ToArray();

        public static string EncryptSecret(string plainSecret)
        {
            if (string.IsNullOrEmpty(plainSecret))
            {
                return string.Empty;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainSecret);
                byte[] cipherBytes = ProtectedData.Protect(
                    plainBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser
                );
                return Convert.ToBase64String(cipherBytes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to encrypt admin secret.");
                return string.Empty;
            }
        }

        public static string? DecryptSecret(string encryptedBase64, out bool migrated)
        {
            migrated = false;
            if (string.IsNullOrWhiteSpace(encryptedBase64))
            {
                return null;
            }

            string? secret = TryUnprotect(encryptedBase64, Entropy);
            if (secret != null)
            {
                return secret;
            }

            secret = TryUnprotect(encryptedBase64, LegacyEntropy);
            if (secret != null)
            {
                migrated = true;
                Log.Information(
                    "Admin secret decrypted with legacy entropy; migration recommended."
                );
                return secret;
            }

            Log.Error("Could not decrypt saved admin secret with either entropy.");
            return null;
        }

        public static string? DecryptSecret(string encryptedBase64)
        {
            return DecryptSecret(encryptedBase64, out _);
        }

        private static string? TryUnprotect(string encryptedBase64, byte[] entropy)
        {
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    entropy,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null;
            }
        }
    }
}
