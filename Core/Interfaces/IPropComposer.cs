using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IPropComposer
    {
        BitmapSource ComposePreview(byte[] originalTextureData, PropRenderOptions options);
        byte[] ComposeTexture(
            byte[] originalTextureData,
            PropRenderOptions options,
            int jpegQuality = 90,
            string? targetFilename = null
        );
    }
}
