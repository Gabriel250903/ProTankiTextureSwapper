using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IPremiumService
    {
        event EventHandler<bool>? PremiumStatusChanged;
        Task<bool> VerifyPremiumStatusAsync(AppSettings settings);
        Task<PremiumActivationResult> ActivatePremiumKeyAsync(string licenseKey, string nickname);
        Task<bool> RevokeLicenseKeyAsync(string licenseKey);
        Task<bool> UnbindHwidAsync(string licenseKey);
        Task<bool> CreateLicenseKeyAsync(string licenseKey);
        Task<bool> DeleteLicenseKeyAsync(string licenseKey);
        Task<PremiumActivationResult> AssignKeyToDeviceAsync(
            string licenseKey,
            string targetHwid,
            string nickname
        );
        Task<(bool Success, string Message)> UpdateNicknameAsync(
            string newNickname,
            string? licenseKey = null,
            string? hwid = null
        );
        Task<List<ServerLicenseKeyItem>> FetchAllLicenseKeysAsync(bool forceRefresh = false);
        Task<bool> CheckAndSyncAssignedLicenseAsync(
            AppSettings? settings = null,
            bool forceSync = false
        );
        bool IsPremiumUnlocked { get; }
        bool IsTrialActive { get; }
        TimeSpan? TrialTimeRemaining { get; }
        void ClearPremiumState(AppSettings? settings = null);
        Task<PremiumActivationResult> StartFreeTrialAsync(string nickname);
        Task<(bool Success, string Message)> ResetDeviceTrialAsync(string hwid);
        Task<(bool Success, string Message)> RevokeDeviceTrialAsync(string hwid);
        Task<(bool Success, string Message)> ExtendDeviceTrialAsync(
            string hwid,
            int days = 3,
            string nickname = ""
        );
        Task<SeasonalEventResult> ProcessSeasonalEventAsync(string nickname);
        Task<byte[]?> GetAssetDecryptionKeyAsync(bool forceRefresh = false);
    }
}
