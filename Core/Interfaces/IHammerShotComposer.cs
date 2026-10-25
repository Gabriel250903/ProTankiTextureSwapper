using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IHammerShotComposer
    {
        BitmapSource ComposePreview(HammerShotRenderOptions options);
        void SaveShotTara(
            HammerShotRenderOptions options,
            string outputPath,
            HammerShotComponent component
        );
        void SaveShotImage(
            HammerShotRenderOptions options,
            string outputPath,
            HammerShotComponent component
        );
    }

    public enum HammerShotComponent
    {
        ShotAcross,
        ShotAlong,
        Smoke,
        PelletTrail,
        Sparkle,
    }
}
