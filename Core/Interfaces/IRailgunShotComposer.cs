using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IRailgunShotComposer
    {
        BitmapSource ComposePreview(RailgunShotRenderOptions options);
        void SaveRailgunTextures(RailgunShotRenderOptions options, string outputDirectory);
    }
}
