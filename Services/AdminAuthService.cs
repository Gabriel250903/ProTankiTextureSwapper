using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class AdminAuthService(IDeviceBanService deviceBanService) : IAdminAuthService
    {
        private readonly IDeviceBanService _deviceBanService = deviceBanService;

        public bool IsAuthenticated => AdminSession.IsAuthenticated;
        public string? CurrentSecret => AdminSession.CurrentSecret;

        public async Task<(bool Success, string Message)> AuthenticateAsync(string password)
        {
            (bool success, string message) = await _deviceBanService.AuthenticateAdminAsync(
                password
            );
            if (success)
            {
                AdminSession.SetSession(password);
            }
            return (success, message);
        }

        public async Task<bool> TryAutoLoginAsync(string? encryptedSecret)
        {
            if (string.IsNullOrWhiteSpace(encryptedSecret))
            {
                return false;
            }

            string? decrypted = AdminSecretStorageHelper.DecryptSecret(encryptedSecret);
            if (string.IsNullOrWhiteSpace(decrypted))
            {
                return false;
            }

            (bool success, _) = await AuthenticateAsync(decrypted);
            return success;
        }

        public string EncryptSecret(string secret)
        {
            return AdminSecretStorageHelper.EncryptSecret(secret);
        }

        public string? DecryptSecret(string encrypted)
        {
            return AdminSecretStorageHelper.DecryptSecret(encrypted);
        }

        public void Logout()
        {
            AdminSession.ClearSession();
        }
    }
}
