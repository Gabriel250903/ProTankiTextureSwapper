using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class ThunderShotComposer : BaseShotEffectComposer, IThunderShotComposer
    {
        private static CachedTextureData? _explosionMainGrayTemplate;
        private static CachedTextureData? _explosionMainAlphaTemplate;

        private static CachedTextureData? _explosionSmokeGrayTemplate;
        private static CachedTextureData? _explosionSmokeAlphaTemplate;

        private static CachedTextureData? _explosionRingGrayTemplate;
        private static CachedTextureData? _explosionRingAlphaTemplate;

        private static CachedTextureData? _flashGrayTemplate;
        private static CachedTextureData? _flashAlphaTemplate;

        private static CachedTextureData? _projectileShaftGrayTemplate;
        private static CachedTextureData? _projectileShaftAlphaTemplate;

        private static CachedTextureData? _projectileHeadGrayTemplate;
        private static CachedTextureData? _projectileHeadAlphaTemplate;

        private static CachedTextureData? _shockwaveGrayTemplate;
        private static CachedTextureData? _shockwaveAlphaTemplate;

        private static CachedTextureData? _explosionVar1GrayTemplate;
        private static CachedTextureData? _explosionVar1AlphaTemplate;

        private static CachedTextureData? _explosionVar2GrayTemplate;
        private static CachedTextureData? _explosionVar2AlphaTemplate;

        private static CachedTextureData? _explosionVar3GrayTemplate;
        private static CachedTextureData? _explosionVar3AlphaTemplate;

        private static CachedTextureData? _explosionVar4GrayTemplate;
        private static CachedTextureData? _explosionVar4AlphaTemplate;

        public BitmapSource ComposeColorBitmap(
            ThunderShotRenderOptions options,
            ThunderShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            return grayTemplate == null
                ? CreateDefaultEmptyBitmap(256, 256)
                : ColorizeRgb(grayTemplate, options);
        }

        public BitmapSource ComposePreview(
            ThunderShotRenderOptions options,
            ThunderShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, CachedTextureData? alphaTemplate, _) = GetTemplates(
                component
            );
            return grayTemplate == null || alphaTemplate == null
                ? CreateDefaultEmptyBitmap(256, 256)
                : ColorizePreview(grayTemplate, alphaTemplate, options);
        }

        public void SaveTara(
            ThunderShotRenderOptions options,
            string outputPath,
            ThunderShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);
            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Thunder {component} alpha mask not found.",
                    alphaPath
                );
            }

            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            byte[] colorJpegBytes = EncodeToJpeg(colorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        public void SaveImage(
            ThunderShotRenderOptions options,
            string outputPath,
            ThunderShotComponent component
        )
        {
            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            SaveImageFile(colorBmp, outputPath);
        }

        private static (
            CachedTextureData? gray,
            CachedTextureData? alpha,
            string alphaFileName
        ) GetTemplates(ThunderShotComponent component)
        {
            return component switch
            {
                ThunderShotComponent.Explosion => (
                    _explosionMainGrayTemplate,
                    _explosionMainAlphaTemplate,
                    "thunder_explosion_main_alpha.jpg"
                ),
                ThunderShotComponent.ExplosionSmoke => (
                    _explosionSmokeGrayTemplate,
                    _explosionSmokeAlphaTemplate,
                    "thunder_explosion_smoke_alpha.jpg"
                ),
                ThunderShotComponent.ExplosionRing => (
                    _explosionRingGrayTemplate,
                    _explosionRingAlphaTemplate,
                    "thunder_explosion_ring_alpha.jpg"
                ),
                ThunderShotComponent.MuzzleFlash => (
                    _flashGrayTemplate,
                    _flashAlphaTemplate,
                    "thunder_flash_alpha.jpg"
                ),
                ThunderShotComponent.ProjectileShaft => (
                    _projectileShaftGrayTemplate,
                    _projectileShaftAlphaTemplate,
                    "thunder_projectile_shaft_alpha.jpg"
                ),
                ThunderShotComponent.ProjectileHead => (
                    _projectileHeadGrayTemplate,
                    _projectileHeadAlphaTemplate,
                    "thunder_projectile_head_alpha.jpg"
                ),
                ThunderShotComponent.Shockwave => (
                    _shockwaveGrayTemplate,
                    _shockwaveAlphaTemplate,
                    "thunder_shockwave_alpha.jpg"
                ),
                ThunderShotComponent.ExplosionVar1 => (
                    _explosionVar1GrayTemplate,
                    _explosionVar1AlphaTemplate,
                    "thunder_explosion_var1_alpha.jpg"
                ),
                ThunderShotComponent.ExplosionVar2 => (
                    _explosionVar2GrayTemplate,
                    _explosionVar2AlphaTemplate,
                    "thunder_explosion_var2_alpha.jpg"
                ),
                ThunderShotComponent.ExplosionVar3 => (
                    _explosionVar3GrayTemplate,
                    _explosionVar3AlphaTemplate,
                    "thunder_explosion_var3_alpha.jpg"
                ),
                ThunderShotComponent.ExplosionVar4 => (
                    _explosionVar4GrayTemplate,
                    _explosionVar4AlphaTemplate,
                    "thunder_explosion_var4_alpha.jpg"
                ),
                _ => (
                    _explosionMainGrayTemplate,
                    _explosionMainAlphaTemplate,
                    "thunder_explosion_main_alpha.jpg"
                ),
            };
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_explosionMainGrayTemplate != null)
            {
                return;
            }

            string baseDir = GetTexturesDirectory();

            _explosionMainGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_main_gray.png")
            );
            _explosionMainAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_main_alpha.jpg")
            );

            _explosionSmokeGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_smoke_gray.png")
            );
            _explosionSmokeAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_smoke_alpha.jpg")
            );

            _explosionRingGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_ring_gray.png")
            );
            _explosionRingAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_ring_alpha.jpg")
            );

            _flashGrayTemplate = LoadTextureData(Path.Combine(baseDir, "thunder_flash_gray.png"));
            _flashAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "thunder_flash_alpha.jpg"));

            _projectileShaftGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_projectile_shaft_gray.png")
            );
            _projectileShaftAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_projectile_shaft_alpha.jpg")
            );

            _projectileHeadGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_projectile_head_gray.png")
            );
            _projectileHeadAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_projectile_head_alpha.jpg")
            );

            _shockwaveGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_shockwave_gray.png")
            );
            _shockwaveAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_shockwave_alpha.jpg")
            );

            _explosionVar1GrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var1_gray.png")
            );
            _explosionVar1AlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var1_alpha.jpg")
            );

            _explosionVar2GrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var2_gray.png")
            );
            _explosionVar2AlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var2_alpha.jpg")
            );

            _explosionVar3GrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var3_gray.png")
            );
            _explosionVar3AlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var3_alpha.jpg")
            );

            _explosionVar4GrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var4_gray.png")
            );
            _explosionVar4AlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "thunder_explosion_var4_alpha.jpg")
            );
        }

        private static string GetTexturesDirectory()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Thunder"
            );
            if (!Directory.Exists(path))
            {
                string alt = Path.Combine(
                    AppContext.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Thunder"
                );
                if (Directory.Exists(alt))
                {
                    return alt;
                }

                string asmDir =
                    Path.GetDirectoryName(typeof(ThunderShotComposer).Assembly.Location) ?? "";
                string asmPath = Path.Combine(
                    asmDir,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Thunder"
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
                    "Thunder"
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
