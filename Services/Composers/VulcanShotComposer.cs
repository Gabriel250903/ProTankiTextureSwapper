using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class VulcanShotComposer : BaseShotEffectComposer, IVulcanShotComposer
    {
        private static CachedTextureData? _tracersGrayTemplate;
        private static CachedTextureData? _tracersAlphaTemplate;

        private static CachedTextureData? _muzzleBurstGrayTemplate;
        private static CachedTextureData? _muzzleBurstAlphaTemplate;

        private static CachedTextureData? _muzzleFlameGrayTemplate;
        private static CachedTextureData? _muzzleFlameAlphaTemplate;

        private static CachedTextureData? _smokePuffGrayTemplate;
        private static CachedTextureData? _smokePuffAlphaTemplate;

        private static CachedTextureData? _smokeCloudGrayTemplate;
        private static CachedTextureData? _smokeCloudAlphaTemplate;

        private static CachedTextureData? _overheatGrayTemplate;
        private static CachedTextureData? _overheatAlphaTemplate;

        public BitmapSource ComposeColorBitmap(
            VulcanShotRenderOptions options,
            VulcanShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            return grayTemplate == null
                ? CreateDefaultEmptyBitmap(256, 256)
                : ColorizeRgb(grayTemplate, options);
        }

        public BitmapSource ComposePreview(
            VulcanShotRenderOptions options,
            VulcanShotComponent component
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
            VulcanShotRenderOptions options,
            string outputPath,
            VulcanShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);
            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Vulcan {component} alpha mask not found.",
                    alphaPath
                );
            }

            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            byte[] colorJpegBytes = EncodeToJpeg(colorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        private static (
            CachedTextureData? gray,
            CachedTextureData? alpha,
            string alphaFileName
        ) GetTemplates(VulcanShotComponent component)
        {
            return component switch
            {
                VulcanShotComponent.Tracers => (
                    _tracersGrayTemplate,
                    _tracersAlphaTemplate,
                    "vulcan_tracers_alpha.jpg"
                ),
                VulcanShotComponent.MuzzleBurst => (
                    _muzzleBurstGrayTemplate,
                    _muzzleBurstAlphaTemplate,
                    "vulcan_muzzle_burst_alpha.jpg"
                ),
                VulcanShotComponent.MuzzleFlame => (
                    _muzzleFlameGrayTemplate,
                    _muzzleFlameAlphaTemplate,
                    "vulcan_muzzle_flame_alpha.jpg"
                ),
                VulcanShotComponent.SmokePuff => (
                    _smokePuffGrayTemplate,
                    _smokePuffAlphaTemplate,
                    "vulcan_smoke_puff_alpha.jpg"
                ),
                VulcanShotComponent.SmokeCloud => (
                    _smokeCloudGrayTemplate,
                    _smokeCloudAlphaTemplate,
                    "vulcan_smoke_cloud_alpha.jpg"
                ),
                VulcanShotComponent.Overheat => (
                    _overheatGrayTemplate,
                    _overheatAlphaTemplate,
                    "vulcan_overheat_alpha.jpg"
                ),
                _ => (_tracersGrayTemplate, _tracersAlphaTemplate, "vulcan_tracers_alpha.jpg"),
            };
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_tracersGrayTemplate != null)
            {
                return;
            }

            string baseDir = GetTexturesDirectory();

            _tracersGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_tracers_gray.png")
            );
            _tracersAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_tracers_alpha.jpg")
            );

            _muzzleBurstGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_muzzle_burst_gray.png")
            );
            _muzzleBurstAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_muzzle_burst_alpha.jpg")
            );

            _muzzleFlameGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_muzzle_flame_gray.png")
            );
            _muzzleFlameAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_muzzle_flame_alpha.jpg")
            );

            _smokePuffGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_smoke_puff_gray.png")
            );
            _smokePuffAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_smoke_puff_alpha.jpg")
            );

            _smokeCloudGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_smoke_cloud_gray.png")
            );
            _smokeCloudAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_smoke_cloud_alpha.jpg")
            );

            _overheatGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_overheat_gray.png")
            );
            _overheatAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "vulcan_overheat_alpha.jpg")
            );
        }

        private static string GetTexturesDirectory()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Vulcan"
            );
            if (!Directory.Exists(path))
            {
                string alt = Path.Combine(
                    AppContext.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Vulcan"
                );
                if (Directory.Exists(alt))
                {
                    return alt;
                }

                string asmDir =
                    Path.GetDirectoryName(typeof(VulcanShotComposer).Assembly.Location) ?? "";
                string asmPath = Path.Combine(
                    asmDir,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Vulcan"
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
                    "Vulcan"
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
