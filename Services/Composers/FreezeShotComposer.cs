using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class FreezeShotComposer : BaseShotEffectComposer, IFreezeShotComposer
    {
        private static CachedTextureData? _mainGrayTemplateData;
        private static CachedTextureData? _mainAlphaTemplateData;
        private static CachedTextureData? _endGrayTemplateData;
        private static CachedTextureData? _endAlphaTemplateData;

        public BitmapSource ComposeBallPreview(FreezeShotRenderOptions options, bool isMain)
        {
            EnsureTemplatesLoaded();

            CachedTextureData? grayData = isMain ? _mainGrayTemplateData : _endGrayTemplateData;
            CachedTextureData? alphaData = isMain ? _mainAlphaTemplateData : _endAlphaTemplateData;

            return grayData == null || alphaData == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizePreview(grayData, alphaData, options);
        }

        private BitmapSource ComposeBall(FreezeShotRenderOptions options, bool isMain)
        {
            EnsureTemplatesLoaded();

            CachedTextureData? grayData = isMain ? _mainGrayTemplateData : _endGrayTemplateData;

            return grayData == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizeRgb(grayData, options);
        }

        public void SaveBallTara(FreezeShotRenderOptions options, string outputPath, bool isMain)
        {
            EnsureTemplatesLoaded();
            string alphaName = isMain ? "freeze_main_alpha.jpg" : "freeze_end_alpha.jpg";
            string alphaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Freeze",
                alphaName
            );

            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException("Freeze ball alpha mask not found.", alphaPath);
            }

            BitmapSource ballColorBmp = ComposeBall(options, isMain);
            byte[] colorJpegBytes = EncodeToJpeg(ballColorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        private static void EnsureTemplatesLoaded()
        {
            if (
                _mainGrayTemplateData != null
                && _mainAlphaTemplateData != null
                && _endGrayTemplateData != null
                && _endAlphaTemplateData != null
            )
            {
                return;
            }

            string dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Freeze"
            );

            string mainGray = Path.Combine(dir, "freeze_main_gray.png");
            string mainAlpha = Path.Combine(dir, "freeze_main_alpha.jpg");
            string endGray = Path.Combine(dir, "freeze_end_gray.png");
            string endAlpha = Path.Combine(dir, "freeze_end_alpha.jpg");

            if (File.Exists(mainGray) && _mainGrayTemplateData == null)
            {
                _mainGrayTemplateData = LoadTextureData(mainGray);
            }

            if (File.Exists(mainAlpha) && _mainAlphaTemplateData == null)
            {
                _mainAlphaTemplateData = LoadTextureData(mainAlpha);
            }

            if (File.Exists(endGray) && _endGrayTemplateData == null)
            {
                _endGrayTemplateData = LoadTextureData(endGray);
            }

            if (File.Exists(endAlpha) && _endAlphaTemplateData == null)
            {
                _endAlphaTemplateData = LoadTextureData(endAlpha);
            }
        }
    }
}
