using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IVulcanShotComposer
    {
        BitmapSource ComposePreview(VulcanShotRenderOptions options, VulcanShotComponent component);

        BitmapSource ComposeColorBitmap(
            VulcanShotRenderOptions options,
            VulcanShotComponent component
        );

        void SaveTara(
            VulcanShotRenderOptions options,
            string outputPath,
            VulcanShotComponent component
        );
    }
}
