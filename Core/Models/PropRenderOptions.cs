using System.Windows.Media;

namespace TextureSwapper.Core.Models
{
    public class PropRenderOptions
    {
        public double Hue { get; set; }
        public double Saturation { get; set; } = 100.0;
        public double Brightness { get; set; }
        public double Contrast { get; set; } = 1.0;

        public Color TintColor { get; set; } = Colors.Transparent;
        public double TintStrength { get; set; }

        public string? PresetName { get; set; }

        public bool ProtectEnabled { get; set; }
        public Color ProtectColor { get; set; } = Colors.Yellow;
        public double ProtectTolerance { get; set; } = 30.0;

        public bool FlipHorizontal { get; set; }
        public bool FlipVertical { get; set; }

        public List<DecalOverlay> Decals { get; set; } = [];
    }
}
