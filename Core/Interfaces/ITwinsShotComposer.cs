using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ITwinsShotComposer
    {
        BitmapSource ComposeBallPreview(TwinsShotRenderOptions options, bool isMain);
        void SaveBallTara(TwinsShotRenderOptions options, string outputPath, bool isMain);
    }
}
