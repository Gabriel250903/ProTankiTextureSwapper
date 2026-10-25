using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IThunderShotComposer
    {
        BitmapSource ComposePreview(
            ThunderShotRenderOptions options,
            ThunderShotComponent component
        );

        BitmapSource ComposeColorBitmap(
            ThunderShotRenderOptions options,
            ThunderShotComponent component
        );

        void SaveTara(
            ThunderShotRenderOptions options,
            string outputPath,
            ThunderShotComponent component
        );

        void SaveImage(
            ThunderShotRenderOptions options,
            string outputPath,
            ThunderShotComponent component
        );
    }
}
