using System.Security.Cryptography;
using System.Text;
using Serilog;

namespace TextureSwapper.Helpers.Utilities
{
    public static class AdminSecretStorageHelper
    {
        private static readonly byte[] Entropy = "TS_Admin_DPAPI_Entropy_2026"u8.ToArray();

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

        public static string? DecryptSecret(string encryptedBase64)
        {
            if (string.IsNullOrWhiteSpace(encryptedBase64))
            {
                return null;
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(encryptedBase64);
                byte[] plainBytes = ProtectedData.Unprotect(
                    cipherBytes,
                    Entropy,
                    DataProtectionScope.CurrentUser
                );
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch (Exception ex)
            {
                Log.Error("Could not decrypt saved admin secret: {Error}", ex.Message);
                return null;
            }
        }
    }
}
