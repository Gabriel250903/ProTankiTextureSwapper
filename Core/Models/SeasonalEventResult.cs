namespace TextureSwapper.Core.Models
{
    public class SeasonalEventResult
    {
        public bool Success { get; set; }
        public string? LicenseKey { get; set; }
        public string? ExpiresAt { get; set; }
        public string? Signature { get; set; }
        public string? DialogTitle { get; set; }
        public string? DialogBody { get; set; }
        public string? CloseButtonText { get; set; }
        public string? NotificationTitle { get; set; }
        public string? NotificationMessage { get; set; }
        public string? Appearance { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
