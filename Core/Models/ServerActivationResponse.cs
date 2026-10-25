namespace TextureSwapper.Core.Models
{
    public class ServerActivationResponse
    {
        public bool Success { get; set; }
        public bool HasLicense { get; set; }
        public string? LicenseKey { get; set; }
        public string? Nickname { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public string? Signature { get; set; }
        public string? Payload { get; set; }
        public string? ExpiresAt { get; set; }
    }
}
