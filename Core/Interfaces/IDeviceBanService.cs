using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IDeviceBanService
    {
        event EventHandler<DeviceBanCheckResult>? DeviceBanned;
        event EventHandler? DeviceUnbanned;
        void StartPeriodicVerification(TimeSpan interval);
        void StopPeriodicVerification();
        Task<DeviceBanCheckResult> VerifyDeviceStatusAsync(bool forceRefresh = false);
        string GetDeviceHardwareId();
        Task<List<AdminDeviceItem>> GetAllDevicesAsync(bool forceRefresh = false);
        Task<bool> BanDeviceAsync(string hwid, string reason, int durationHours = 0);
        Task<bool> UnbanDeviceAsync(string hwid);
        Task<(bool Success, string Message)> AuthenticateAdminAsync(string password);
    }
}
