namespace TextureSwapper.Core.Models
{
    public class ParachuteRenderOptions
    {
        public double Scale { get; set; } = 1.0;
        public double OffsetX { get; set; } = 0.0;
        public double OffsetY { get; set; } = 0.0;
        public double RotationAngle { get; set; } = 0.0;
        public bool TileMode { get; set; } = false;
        public double ShadingOpacity { get; set; } = 0.5;
        public string? SourceImagePath { get; set; }
    }
}
