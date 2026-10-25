using System.Windows.Media.Imaging;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IParachuteComposer
    {
        BitmapSource ComposeParachute(ParachuteRenderOptions options);
        void SaveParachuteJpg(ParachuteRenderOptions options, string outputPath);
    }
}
