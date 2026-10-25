using System.IO;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class DustEffectComposer : BaseShotEffectComposer, IDustEffectComposer
    {
        private static CachedTextureData? _moundGrayTemplate;
        private static CachedTextureData? _moundAlphaTemplate;

        private static CachedTextureData? _smokeGrayTemplate;
        private static CachedTextureData? _smokeAlphaTemplate;

        private static CachedTextureData? _tracerGrayTemplate;
        private static CachedTextureData? _tracerAlphaTemplate;

        private static CachedTextureData? _trailGrayTemplate;
        private static CachedTextureData? _trailAlphaTemplate;

        public BitmapSource ComposeColorBitmap(
            DustEffectRenderOptions options,
            DustEffectComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            return grayTemplate == null
                ? CreateDefaultEmptyBitmap(256, 256)
                : ColorizeRgb(grayTemplate, options);
        }

        public BitmapSource ComposePreview(
            DustEffectRenderOptions options,
            DustEffectComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, CachedTextureData? alphaTemplate, _) = GetTemplates(
                component
            );
            return grayTemplate == null
                ? CreateDefaultEmptyBitmap(256, 256)
                : ColorizePreview(grayTemplate, alphaTemplate, options);
        }

        public void SaveTara(
            DustEffectRenderOptions options,
            string outputPath,
            DustEffectComponent component
        )
        {
            EnsureTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);
            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Dust effect {component} alpha mask not found.",
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
        ) GetTemplates(DustEffectComponent component)
        {
            return component switch
            {
                DustEffectComponent.Mound => (
                    _moundGrayTemplate,
                    _moundAlphaTemplate,
                    "dust_mound_alpha.jpg"
                ),
                DustEffectComponent.Smoke => (
                    _smokeGrayTemplate,
                    _smokeAlphaTemplate,
                    "dust_smoke_alpha.jpg"
                ),
                DustEffectComponent.Tracer => (
                    _tracerGrayTemplate,
                    _tracerAlphaTemplate,
                    "dust_tracer_alpha.jpg"
                ),
                DustEffectComponent.Trail => (
                    _trailGrayTemplate,
                    _trailAlphaTemplate,
                    "dust_trail_alpha.jpg"
                ),
                _ => (_moundGrayTemplate, _moundAlphaTemplate, "dust_mound_alpha.jpg"),
            };
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_moundGrayTemplate != null)
            {
                return;
            }

            string baseDir = GetTexturesDirectory();

            _moundGrayTemplate = LoadTextureData(Path.Combine(baseDir, "dust_mound_gray.png"));
            _moundAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "dust_mound_alpha.jpg"));

            _smokeGrayTemplate = LoadTextureData(Path.Combine(baseDir, "dust_smoke_gray.png"));
            _smokeAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "dust_smoke_alpha.jpg"));

            _tracerGrayTemplate = LoadTextureData(Path.Combine(baseDir, "dust_tracer_gray.png"));
            _tracerAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "dust_tracer_alpha.jpg"));

            _trailGrayTemplate = LoadTextureData(Path.Combine(baseDir, "dust_trail_gray.png"));
            _trailAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "dust_trail_alpha.jpg"));
        }

        private static string GetTexturesDirectory()
        {
            string baseDir = Path.Combine(
                AppContext.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Dust"
            );

            return Directory.Exists(baseDir)
                ? baseDir
                : Path.Combine(Directory.GetCurrentDirectory(), "Textures", "ShotEffects", "Dust");
        }
    }
}
