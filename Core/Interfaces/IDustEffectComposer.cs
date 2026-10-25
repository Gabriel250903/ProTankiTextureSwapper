using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IDustEffectComposer
    {
        BitmapSource ComposePreview(DustEffectRenderOptions options, DustEffectComponent component);

        BitmapSource ComposeColorBitmap(
            DustEffectRenderOptions options,
            DustEffectComponent component
        );

        void SaveTara(
            DustEffectRenderOptions options,
            string outputPath,
            DustEffectComponent component
        );
    }
}
