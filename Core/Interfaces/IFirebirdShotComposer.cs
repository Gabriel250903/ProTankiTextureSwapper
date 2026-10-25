using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IFirebirdShotComposer
    {
        BitmapSource ComposeBallPreview(FirebirdShotRenderOptions options, bool isMain);
        void SaveBallTara(FirebirdShotRenderOptions options, string outputPath, bool isMain);
        void SaveMuzzleTara(FirebirdShotRenderOptions options, string outputPath);
    }
}
