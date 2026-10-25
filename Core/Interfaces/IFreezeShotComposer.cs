using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IFreezeShotComposer
    {
        BitmapSource ComposeBallPreview(FreezeShotRenderOptions options, bool isMain);
        void SaveBallTara(FreezeShotRenderOptions options, string outputPath, bool isMain);
    }
}
