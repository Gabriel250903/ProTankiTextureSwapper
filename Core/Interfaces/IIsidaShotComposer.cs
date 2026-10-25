using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IIsidaShotComposer
    {
        BitmapSource ComposePreview(IsidaShotRenderOptions options, IsidaShotComponent component);

        BitmapSource ComposeColorBitmap(
            IsidaShotRenderOptions options,
            IsidaShotComponent component
        );

        void SaveTara(
            IsidaShotRenderOptions options,
            string outputPath,
            IsidaShotComponent component
        );

        void SaveImage(
            IsidaShotRenderOptions options,
            string outputPath,
            IsidaShotComponent component
        );
    }
}
