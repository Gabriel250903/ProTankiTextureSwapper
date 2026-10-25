using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class ShaftShotComposer : BaseShotEffectComposer, IShaftShotComposer
    {
        private static CachedTextureData? _trailGrayTemplate;
        private static CachedTextureData? _trailAlphaTemplate;

        private static CachedTextureData? _laserDotGrayTemplate;
        private static CachedTextureData? _laserDotAlphaTemplate;

        private static CachedTextureData? _muzzleSmokeGrayTemplate;
        private static CachedTextureData? _muzzleSmokeAlphaTemplate;

        private static CachedTextureData? _muzzleFlashGrayTemplate;
        private static CachedTextureData? _muzzleFlashAlphaTemplate;

        private static CachedTextureData? _burstGrayTemplate;
        private static CachedTextureData? _burstAlphaTemplate;

        private static CachedTextureData? _impactGrayTemplate;
        private static CachedTextureData? _impactAlphaTemplate;

        public BitmapSource ComposeColorBitmap(
            ShaftShotRenderOptions options,
            ShaftShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            return grayTemplate == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizeRgb(grayTemplate, options);
        }

        public BitmapSource ComposePreview(
            ShaftShotRenderOptions options,
            ShaftShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, CachedTextureData? alphaTemplate, _) = GetTemplates(
                component
            );
            return grayTemplate == null || alphaTemplate == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizePreview(grayTemplate, alphaTemplate, options);
        }

        public void SaveTara(
            ShaftShotRenderOptions options,
            string outputPath,
            ShaftShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);
            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Shaft {component} alpha mask not found.",
                    alphaPath
                );
            }

            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            byte[] colorJpegBytes = EncodeToJpeg(colorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        public void SaveImage(
            ShaftShotRenderOptions options,
            string outputPath,
            ShaftShotComponent component
        )
        {
            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            SaveImageFile(colorBmp, outputPath);
        }

        private static (
            CachedTextureData? gray,
            CachedTextureData? alpha,
            string alphaFileName
        ) GetTemplates(ShaftShotComponent component)
        {
            return component switch
            {
                ShaftShotComponent.Trail => (
                    _trailGrayTemplate,
                    _trailAlphaTemplate,
                    "shaft_trail_alpha.jpg"
                ),
                ShaftShotComponent.LaserDot => (
                    _laserDotGrayTemplate,
                    _laserDotAlphaTemplate,
                    "shaft_laser_alpha.jpg"
                ),
                ShaftShotComponent.MuzzleSmoke => (
                    _muzzleSmokeGrayTemplate,
                    _muzzleSmokeAlphaTemplate,
                    "shaft_muzzle_smoke_alpha.jpg"
                ),
                ShaftShotComponent.MuzzleFlash => (
                    _muzzleFlashGrayTemplate,
                    _muzzleFlashAlphaTemplate,
                    "shaft_muzzle_flash_alpha.jpg"
                ),
                ShaftShotComponent.Burst => (
                    _burstGrayTemplate,
                    _burstAlphaTemplate,
                    "shaft_burst_alpha.jpg"
                ),
                ShaftShotComponent.Impact => (
                    _impactGrayTemplate,
                    _impactAlphaTemplate,
                    "shaft_impact_alpha.jpg"
                ),
                _ => (_trailGrayTemplate, _trailAlphaTemplate, "shaft_trail_alpha.jpg"),
            };
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_trailGrayTemplate != null)
            {
                return;
            }

            string baseDir = GetTexturesDirectory();

            _trailGrayTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_trail_gray.png"));
            _trailAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_trail_alpha.jpg"));

            _laserDotGrayTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_laser_gray.png"));
            _laserDotAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "shaft_laser_alpha.jpg")
            );

            _muzzleSmokeGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "shaft_muzzle_smoke_gray.png")
            );
            _muzzleSmokeAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "shaft_muzzle_smoke_alpha.jpg")
            );

            _muzzleFlashGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "shaft_muzzle_flash_gray.png")
            );
            _muzzleFlashAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "shaft_muzzle_flash_alpha.jpg")
            );

            _burstGrayTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_burst_gray.png"));
            _burstAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_burst_alpha.jpg"));

            _impactGrayTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_impact_gray.png"));
            _impactAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "shaft_impact_alpha.jpg"));
        }

        private static string GetTexturesDirectory()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Shaft"
            );
            if (!Directory.Exists(path))
            {
                string alt = Path.Combine(
                    AppContext.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Shaft"
                );
                if (Directory.Exists(alt))
                {
                    return alt;
                }

                string asmDir =
                    Path.GetDirectoryName(typeof(ShaftShotComposer).Assembly.Location) ?? "";
                string asmPath = Path.Combine(
                    asmDir,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Shaft"
                );
                if (Directory.Exists(asmPath))
                {
                    return asmPath;
                }

                string cwdPath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Shaft"
                );
                if (Directory.Exists(cwdPath))
                {
                    return cwdPath;
                }
            }

            return path;
        }
    }
}
