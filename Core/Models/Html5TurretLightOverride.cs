namespace TextureSwapper.Core.Models
{
    public class Html5TurretLightOverride
    {
        public string ColorHex { get; set; } = "#FFFFFF";
        public bool IsDark { get; set; }
        public string? SecondaryColorHex { get; set; }
        public bool IsSecondaryDark { get; set; }
        public List<long> ResourceIds { get; set; } = [];
    }
}
