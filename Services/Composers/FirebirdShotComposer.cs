using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class FirebirdShotComposer : BaseShotEffectComposer, IFirebirdShotComposer
    {
        private static CachedTextureData? _mainGrayTemplateData;
        private static CachedTextureData? _mainAlphaTemplateData;
        private static CachedTextureData? _endGrayTemplateData;
        private static CachedTextureData? _endAlphaTemplateData;
        private static CachedTextureData? _muzzleGrayTemplateData;
        private static CachedTextureData? _muzzleAlphaTemplateData;

        public BitmapSource ComposeBallPreview(FirebirdShotRenderOptions options, bool isMain)
        {
            EnsureTemplatesLoaded();

            CachedTextureData? grayData = isMain ? _mainGrayTemplateData : _endGrayTemplateData;
            CachedTextureData? alphaData = isMain ? _mainAlphaTemplateData : _endAlphaTemplateData;

            return grayData == null || alphaData == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizePreview(grayData, alphaData, options);
        }

        private BitmapSource ComposeBall(FirebirdShotRenderOptions options, bool isMain)
        {
            EnsureTemplatesLoaded();

            CachedTextureData? grayData = isMain ? _mainGrayTemplateData : _endGrayTemplateData;

            return grayData == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizeRgb(grayData, options);
        }

        public void SaveBallTara(FirebirdShotRenderOptions options, string outputPath, bool isMain)
        {
            EnsureTemplatesLoaded();
            string alphaName = isMain ? "firebird_main_alpha.jpg" : "firebird_end_alpha.jpg";
            string alphaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Firebird",
                alphaName
            );

            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException("Firebird ball alpha mask not found.", alphaPath);
            }

            BitmapSource ballColorBmp = ComposeBall(options, isMain);
            byte[] colorJpegBytes = EncodeToJpeg(ballColorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        public void SaveMuzzleTara(FirebirdShotRenderOptions options, string outputPath)
        {
            EnsureTemplatesLoaded();
            string alphaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Firebird",
                "firebird_muzzle_alpha.jpg"
            );

            if (!File.Exists(alphaPath) || _muzzleGrayTemplateData == null)
            {
                return;
            }

            BitmapSource muzzleColorBmp = ColorizeRgb(_muzzleGrayTemplateData, options);
            byte[] colorJpegBytes = EncodeToJpeg(muzzleColorBmp);
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
                && _muzzleGrayTemplateData != null
                && _muzzleAlphaTemplateData != null
            )
            {
                return;
            }

            string dir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Firebird"
            );

            string mainGray = Path.Combine(dir, "firebird_main_gray.png");
            string mainAlpha = Path.Combine(dir, "firebird_main_alpha.jpg");
            string endGray = Path.Combine(dir, "firebird_end_gray.png");
            string endAlpha = Path.Combine(dir, "firebird_end_alpha.jpg");
            string muzzleGray = Path.Combine(dir, "firebird_muzzle_gray.png");
            string muzzleAlpha = Path.Combine(dir, "firebird_muzzle_alpha.jpg");

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

            if (File.Exists(muzzleGray) && _muzzleGrayTemplateData == null)
            {
                _muzzleGrayTemplateData = LoadTextureData(muzzleGray);
            }

            if (File.Exists(muzzleAlpha) && _muzzleAlphaTemplateData == null)
            {
                _muzzleAlphaTemplateData = LoadTextureData(muzzleAlpha);
            }
        }
    }
}
