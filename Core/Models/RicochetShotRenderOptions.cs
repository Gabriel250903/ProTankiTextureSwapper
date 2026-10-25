namespace TextureSwapper.Core.Models
{
    public class RicochetShotRenderOptions : IShotColorOptions
    {
        public double Hue { get; set; }
        public double Saturation { get; set; } = 100.0;
        public double Brightness { get; set; } = 0.0;
        public double Contrast { get; set; } = 1.0;
        public bool Invert { get; set; }
    }
}
