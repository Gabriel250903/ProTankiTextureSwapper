namespace TextureSwapper.Core.Models
{
    public class PremiumActivationResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public string ExpiresAt { get; set; } = string.Empty;
    }
}
