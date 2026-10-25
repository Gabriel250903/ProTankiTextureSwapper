using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ITextureComposer
    {
        BitmapSource Compose(TextureRenderOptions options, IMaskStrategy maskStrategy);
        void SaveImage(
            TextureRenderOptions options,
            IMaskStrategy maskStrategy,
            string outputPath,
            bool isJpeg = true
        );
        void ClearCache();
    }
}
