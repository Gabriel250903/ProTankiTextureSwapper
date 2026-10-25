using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ISmokyShotComposer
    {
        BitmapSource ComposePreview(SmokyShotRenderOptions options, SmokyShotComponent component);

        BitmapSource ComposeColorBitmap(
            SmokyShotRenderOptions options,
            SmokyShotComponent component
        );

        void SaveTara(
            SmokyShotRenderOptions options,
            string outputPath,
            SmokyShotComponent component
        );

        void SaveImage(
            SmokyShotRenderOptions options,
            string outputPath,
            SmokyShotComponent component
        );
    }
}
