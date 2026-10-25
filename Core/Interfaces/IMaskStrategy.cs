using System.Windows.Media;

namespace TextureSwapper.Core.Interfaces
{
    public interface IMaskStrategy
    {
        Geometry? GetClipGeometry(double canvasWidth, double canvasHeight);
        double GetPixelMaskWeight(int x, int y, double rawLuminance, double rawAlpha);
    }
}
