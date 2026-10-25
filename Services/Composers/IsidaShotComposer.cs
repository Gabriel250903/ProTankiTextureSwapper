using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services.Composers
{
    public class IsidaShotComposer : BaseShotEffectComposer, IIsidaShotComposer
    {
        private static CachedTextureData? _beamGrayTemplate;
        private static CachedTextureData? _damageBeamAlphaTemplate;
        private static CachedTextureData? _healBeamAlphaTemplate;

        private static CachedTextureData? _ballGrayTemplate;
        private static CachedTextureData? _ballAlphaTemplate;

        private static CachedTextureData? _impactGrayTemplate;
        private static CachedTextureData? _impactAlphaTemplate;

        private static CachedTextureData? _flareGrayTemplate;
        private static CachedTextureData? _flareAlphaTemplate;

        private static CachedTextureData? _healShaftGrayTemplate;
        private static CachedTextureData? _healShaftAlphaTemplate;

        public BitmapSource ComposeColorBitmap(
            IsidaShotRenderOptions options,
            IsidaShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            if (grayTemplate == null)
            {
                return CreateDefaultEmptyBitmap(256, 128);
            }

            int width = grayTemplate.Width;
            int height = grayTemplate.Height;
            int stride = width * 4;
            byte[] outputPixels = ArrayPool<byte>.Shared.Rent(height * stride);
            byte[] templatePixels = grayTemplate.Pixels;

            double hue = options.Hue;
            double saturation = options.Saturation;
            double brightness = options.Brightness;
            double contrast = options.Contrast;
            double brightnessOffset = brightness / 100.0;
            int totalPixels = width * height;

            ColorHelper.HslToRgb(
                hue / 360.0,
                saturation / 100.0,
                0.5,
                out byte pureR,
                out byte pureG,
                out byte pureB
            );

            try
            {
                for (int i = 0; i < totalPixels; i++)
                {
                    int pixelOffset = i * 4;
                    byte gray = templatePixels[pixelOffset + 2];

                    double luminance = gray / 255.0;
                    if (options.Invert)
                    {
                        luminance = 1.0 - luminance;
                    }

                    double lum = luminance + brightnessOffset;
                    lum = ((lum - 0.5) * contrast) + 0.5;
                    lum = Math.Clamp(lum, 0.0, 1.0);

                    double outR = pureR / 255.0 * lum;
                    double outG = pureG / 255.0 * lum;
                    double outB = pureB / 255.0 * lum;

                    if (lum > 0.88)
                    {
                        double core = (lum - 0.88) / 0.12;
                        core = core * core * 0.5;
                        outR += (1.0 - outR) * core;
                        outG += (1.0 - outG) * core;
                        outB += (1.0 - outB) * core;
                    }

                    outputPixels[pixelOffset] = (byte)Math.Clamp(outB * 255.0, 0, 255);
                    outputPixels[pixelOffset + 1] = (byte)Math.Clamp(outG * 255.0, 0, 255);
                    outputPixels[pixelOffset + 2] = (byte)Math.Clamp(outR * 255.0, 0, 255);
                    outputPixels[pixelOffset + 3] = 255;
                }

                BitmapSource source = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgr32,
                    null,
                    outputPixels,
                    stride
                );
                source.Freeze();
                return source;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(outputPixels);
            }
        }

        public BitmapSource ComposePreview(
            IsidaShotRenderOptions options,
            IsidaShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, CachedTextureData? alphaTemplate, _) = GetTemplates(
                component
            );
            if (grayTemplate == null || alphaTemplate == null)
            {
                return CreateDefaultEmptyBitmap(256, 128);
            }

            int sheetWidth = grayTemplate.Width;
            int sheetHeight = grayTemplate.Height;
            int previewStride = sheetWidth * 4;
            byte[] previewPixels = ArrayPool<byte>.Shared.Rent(sheetHeight * previewStride);

            double hue = options.Hue;
            double saturation = options.Saturation;
            double brightness = options.Brightness;
            double contrast = options.Contrast;
            double brightnessOffset = brightness / 100.0;

            byte[] grayPixels = grayTemplate.Pixels;
            byte[] alphaPixels = alphaTemplate.Pixels;

            ColorHelper.HslToRgb(
                hue / 360.0,
                saturation / 100.0,
                0.5,
                out byte pureR,
                out byte pureG,
                out byte pureB
            );

            try
            {
                for (int y = 0; y < sheetHeight; y++)
                {
                    for (int x = 0; x < sheetWidth; x++)
                    {
                        int sheetOffset = ((y * sheetWidth) + x) * 4;

                        byte gray = grayPixels[sheetOffset + 2];
                        byte alpha = alphaPixels[sheetOffset + 2];

                        if (alpha == 0)
                        {
                            previewPixels[sheetOffset] = 0;
                            previewPixels[sheetOffset + 1] = 0;
                            previewPixels[sheetOffset + 2] = 0;
                            previewPixels[sheetOffset + 3] = 0;
                            continue;
                        }

                        double luminance = gray / 255.0;
                        if (options.Invert)
                        {
                            luminance = 1.0 - luminance;
                        }

                        double lum = luminance + brightnessOffset;
                        lum = ((lum - 0.5) * contrast) + 0.5;
                        lum = Math.Clamp(lum, 0.0, 1.0);

                        double outR = pureR / 255.0 * lum;
                        double outG = pureG / 255.0 * lum;
                        double outB = pureB / 255.0 * lum;

                        if (lum > 0.88)
                        {
                            double core = (lum - 0.88) / 0.12;
                            core = core * core * 0.5;
                            outR += (1.0 - outR) * core;
                            outG += (1.0 - outG) * core;
                            outB += (1.0 - outB) * core;
                        }

                        previewPixels[sheetOffset] = (byte)Math.Clamp(outB * 255.0, 0, 255);
                        previewPixels[sheetOffset + 1] = (byte)Math.Clamp(outG * 255.0, 0, 255);
                        previewPixels[sheetOffset + 2] = (byte)Math.Clamp(outR * 255.0, 0, 255);
                        previewPixels[sheetOffset + 3] = alpha;
                    }
                }

                BitmapSource source = BitmapSource.Create(
                    sheetWidth,
                    sheetHeight,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    previewPixels,
                    previewStride
                );
                source.Freeze();
                return source;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(previewPixels);
            }
        }

        public void SaveTara(
            IsidaShotRenderOptions options,
            string outputPath,
            IsidaShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);
            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Isida {component} alpha mask not found.",
                    alphaPath
                );
            }

            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            byte[] colorJpegBytes = EncodeToJpeg(colorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        public void SaveImage(
            IsidaShotRenderOptions options,
            string outputPath,
            IsidaShotComponent component
        )
        {
            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            SaveImageFile(colorBmp, outputPath);
        }

        private static (
            CachedTextureData? gray,
            CachedTextureData? alpha,
            string alphaFileName
        ) GetTemplates(IsidaShotComponent component)
        {
            return component switch
            {
                IsidaShotComponent.DamageBeam => (
                    _beamGrayTemplate,
                    _damageBeamAlphaTemplate,
                    "isida_damage_beam_alpha.jpg"
                ),
                IsidaShotComponent.DamageBall => (
                    _ballGrayTemplate,
                    _ballAlphaTemplate,
                    "isida_ball_alpha.jpg"
                ),
                IsidaShotComponent.DamageImpact => (
                    _impactGrayTemplate,
                    _impactAlphaTemplate,
                    "isida_impact_alpha.jpg"
                ),
                IsidaShotComponent.DamageFlare => (
                    _flareGrayTemplate,
                    _flareAlphaTemplate,
                    "isida_flare_alpha.jpg"
                ),
                IsidaShotComponent.HealBeam => (
                    _beamGrayTemplate,
                    _healBeamAlphaTemplate,
                    "isida_heal_beam_alpha.jpg"
                ),
                IsidaShotComponent.HealBall => (
                    _ballGrayTemplate,
                    _ballAlphaTemplate,
                    "isida_ball_alpha.jpg"
                ),
                IsidaShotComponent.HealShaft => (
                    _healShaftGrayTemplate,
                    _healShaftAlphaTemplate,
                    "isida_heal_shaft_alpha.jpg"
                ),
                _ => (_beamGrayTemplate, _damageBeamAlphaTemplate, "isida_damage_beam_alpha.jpg"),
            };
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_beamGrayTemplate != null)
            {
                return;
            }

            string baseDir = GetTexturesDirectory();

            _beamGrayTemplate = LoadTextureData(Path.Combine(baseDir, "isida_gray.png"));
            _damageBeamAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "isida_damage_beam_alpha.jpg")
            );
            _healBeamAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "isida_heal_beam_alpha.jpg")
            );

            _ballGrayTemplate = LoadTextureData(Path.Combine(baseDir, "isida_ball_gray.png"));
            _ballAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "isida_ball_alpha.jpg"));

            _impactGrayTemplate = LoadTextureData(Path.Combine(baseDir, "isida_impact_gray.png"));
            _impactAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "isida_impact_alpha.jpg"));

            _flareGrayTemplate = LoadTextureData(Path.Combine(baseDir, "isida_flare_gray.png"));
            _flareAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "isida_flare_alpha.jpg"));

            _healShaftGrayTemplate = LoadTextureData(
                Path.Combine(baseDir, "isida_heal_shaft_gray.png")
            );
            _healShaftAlphaTemplate = LoadTextureData(
                Path.Combine(baseDir, "isida_heal_shaft_alpha.jpg")
            );
        }

        private static string GetTexturesDirectory()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Isida"
            );
            if (!Directory.Exists(path))
            {
                string alt = Path.Combine(
                    AppContext.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Isida"
                );
                if (Directory.Exists(alt))
                {
                    return alt;
                }

                string asmDir =
                    Path.GetDirectoryName(typeof(IsidaShotComposer).Assembly.Location) ?? "";
                string asmPath = Path.Combine(
                    asmDir,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Isida"
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
                    "Isida"
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
