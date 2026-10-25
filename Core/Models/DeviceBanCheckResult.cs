namespace TextureSwapper.Core.Models
{
    public class DeviceBanCheckResult
    {
        public bool IsBanned { get; set; }
        public bool IsTemporary { get; set; }
        public string ExpiresAt { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool NetworkError { get; set; }
    }
}
