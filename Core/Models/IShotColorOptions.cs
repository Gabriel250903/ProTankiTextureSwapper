namespace TextureSwapper.Core.Models
{
    public interface IShotColorOptions
    {
        double Hue { get; set; }
        double Saturation { get; set; }
        double Brightness { get; set; }
        double Contrast { get; set; }
        bool Invert { get; set; }
    }
}
