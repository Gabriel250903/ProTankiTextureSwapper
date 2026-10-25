using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class HammerShotComposer : BaseShotEffectComposer, IHammerShotComposer
    {
        private static CachedTextureData? _acrossGrayTemplate;
        private static CachedTextureData? _acrossAlphaTemplate;
        private static CachedTextureData? _alongGrayTemplate;
        private static CachedTextureData? _alongAlphaTemplate;
        private static CachedTextureData? _smokeGrayTemplate;
        private static CachedTextureData? _smokeAlphaTemplate;
        private static CachedTextureData? _pelletGrayTemplate;
        private static CachedTextureData? _sparkleGrayTemplate;

        public BitmapSource ComposePreview(HammerShotRenderOptions options)
        {
            EnsureTemplatesLoaded();

            return _acrossGrayTemplate == null || _acrossAlphaTemplate == null
                ? CreateDefaultEmptyBitmap(256, 128)
                : ColorizePreview(_acrossGrayTemplate, _acrossAlphaTemplate, options);
        }

        public void SaveShotTara(
            HammerShotRenderOptions options,
            string outputPath,
            HammerShotComponent component
        )
        {
            EnsureTemplatesLoaded();

            (CachedTextureData? grayData, string alphaFileName) = component switch
            {
                HammerShotComponent.ShotAcross => (
                    _acrossGrayTemplate,
                    "hammer_shot_across_alpha.jpg"
                ),
                HammerShotComponent.ShotAlong => (
                    _alongGrayTemplate,
                    "hammer_shot_along_alpha.jpg"
                ),
                HammerShotComponent.Smoke => (_smokeGrayTemplate, "hammer_smoke_alpha.jpg"),
                _ => (_acrossGrayTemplate, "hammer_shot_across_alpha.jpg"),
            };

            if (grayData == null)
            {
                throw new InvalidOperationException(
                    $"Template data for {component} is not loaded."
                );
            }

            string alphaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Hammer",
                alphaFileName
            );

            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Alpha mask not found for {component}: {alphaPath}"
                );
            }

            BitmapSource colorBmp = ColorizeRgb(grayData, options);
            byte[] colorJpegBytes = EncodeToJpeg(colorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        public void SaveShotImage(
            HammerShotRenderOptions options,
            string outputPath,
            HammerShotComponent component
        )
        {
            EnsureTemplatesLoaded();

            CachedTextureData? grayData =
                component switch
                {
                    HammerShotComponent.PelletTrail => _pelletGrayTemplate,
                    HammerShotComponent.Sparkle => _sparkleGrayTemplate,
                    _ => null,
                }
                ?? throw new InvalidOperationException(
                    $"Template data for {component} is not loaded."
                );

            BitmapSource colorBmp = ColorizeRgb(grayData, options);
            SaveImageFile(colorBmp, outputPath);
        }

        private static void EnsureTemplatesLoaded()
        {
            if (
                _acrossGrayTemplate != null
                && _alongGrayTemplate != null
                && _smokeGrayTemplate != null
            )
            {
                return;
            }

            string baseDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Hammer"
            );

            _acrossGrayTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_shot_across_gray.png")
            );
            _acrossAlphaTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_shot_across_alpha.jpg")
            );

            _alongGrayTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_shot_along_gray.png")
            );
            _alongAlphaTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_shot_along_alpha.jpg")
            );

            _smokeGrayTemplate ??= LoadTextureData(Path.Combine(baseDir, "hammer_smoke_gray.png"));
            _smokeAlphaTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_smoke_alpha.jpg")
            );

            _pelletGrayTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_pellet_trail_gray.png")
            );
            _sparkleGrayTemplate ??= LoadTextureData(
                Path.Combine(baseDir, "hammer_sparkle_gray.png")
            );
        }
    }
}
