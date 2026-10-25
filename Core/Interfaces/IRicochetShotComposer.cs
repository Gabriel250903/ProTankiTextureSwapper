using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IRicochetShotComposer
    {
        BitmapSource ComposeBall(
            RicochetShotRenderOptions options,
            RicochetShotComponent component = RicochetShotComponent.Main
        );
        BitmapSource ComposeBallPreview(
            RicochetShotRenderOptions options,
            RicochetShotComponent component = RicochetShotComponent.Main
        );
        void SaveBallTara(
            RicochetShotRenderOptions options,
            string outputPath,
            RicochetShotComponent component = RicochetShotComponent.Main
        );
    }
}
