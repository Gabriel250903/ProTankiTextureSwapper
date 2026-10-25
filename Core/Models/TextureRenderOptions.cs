namespace TextureSwapper.Core.Models
{
    public class TextureRenderOptions
    {
        public string? BaseTemplatePath { get; set; }
        public string? SourceImagePath { get; set; }
        public double Scale { get; set; } = 1.0;
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public double RotationAngle { get; set; }
        public bool TileMode { get; set; }
        public double Hue { get; set; }
        public double Saturation { get; set; } = 100.0;
        public double Brightness { get; set; }
        public double Contrast { get; set; } = 1.0;
        public bool Invert { get; set; }
        public string BlendMode { get; set; } = "Multiply";
        public double OverlayOpacity { get; set; } = 100.0;
        public double RedScale { get; set; } = 100.0;
        public double GreenScale { get; set; } = 100.0;
        public double BlueScale { get; set; } = 100.0;
        public double PixelSize { get; set; } = 1.0;
        public double Noise { get; set; }
        public double BlurRadius { get; set; }
        public double TextureScaleX { get; set; } = 1.0;
        public double TextureScaleY { get; set; } = 1.0;
        public double TextureOffsetX { get; set; }
        public double TextureOffsetY { get; set; }
        public double WaveAmplitude { get; set; }
        public double WaveFrequency { get; set; } = 1.0;
        public double BeamThickness { get; set; } = 1.0;
        public double CoreSharpness { get; set; } = 1.0;
        public double InnerCoreBoost { get; set; }
    }
}
