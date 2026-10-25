using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IShaftShotComposer
    {
        BitmapSource ComposePreview(ShaftShotRenderOptions options, ShaftShotComponent component);

        BitmapSource ComposeColorBitmap(
            ShaftShotRenderOptions options,
            ShaftShotComponent component
        );

        void SaveTara(
            ShaftShotRenderOptions options,
            string outputPath,
            ShaftShotComponent component
        );

        void SaveImage(
            ShaftShotRenderOptions options,
            string outputPath,
            ShaftShotComponent component
        );
    }
}
