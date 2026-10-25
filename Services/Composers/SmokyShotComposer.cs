using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services.Composers
{
    public class SmokyShotComposer : BaseShotEffectComposer, ISmokyShotComposer
    {
        private static CachedTextureData? _flashGrayTemplate;
        private static CachedTextureData? _flashAlphaTemplate;

        private static CachedTextureData? _crit1GrayTemplate;
        private static CachedTextureData? _crit1AlphaTemplate;

        private static CachedTextureData? _crit2GrayTemplate;
        private static CachedTextureData? _crit2AlphaTemplate;

        private static CachedTextureData? _crit3GrayTemplate;
        private static CachedTextureData? _crit3AlphaTemplate;

        private static CachedTextureData? _bulletGrayTemplate;
        private static CachedTextureData? _bulletAlphaTemplate;

        private static CachedTextureData? _glowGrayTemplate;
        private static CachedTextureData? _glowAlphaTemplate;

        public BitmapSource ComposeColorBitmap(
            SmokyShotRenderOptions options,
            SmokyShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, _, _) = GetTemplates(component);
            if (grayTemplate == null)
            {
                return CreateDefaultEmptyBitmap(256, 256);
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

                    if (lum > 0.85)
                    {
                        double core = (lum - 0.85) / 0.15;
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
            SmokyShotRenderOptions options,
            SmokyShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (CachedTextureData? grayTemplate, CachedTextureData? alphaTemplate, _) = GetTemplates(
                component
            );
            if (grayTemplate == null || alphaTemplate == null)
            {
                return CreateDefaultEmptyBitmap(256, 256);
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

                        if (lum > 0.85)
                        {
                            double core = (lum - 0.85) / 0.15;
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
            SmokyShotRenderOptions options,
            string outputPath,
            SmokyShotComponent component
        )
        {
            EnsureTemplatesLoaded();
            (_, _, string alphaFileName) = GetTemplates(component);

            string alphaPath = Path.Combine(GetTexturesDirectory(), alphaFileName);
            if (!File.Exists(alphaPath))
            {
                throw new FileNotFoundException(
                    $"Smoky {component} alpha mask not found.",
                    alphaPath
                );
            }

            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            byte[] colorJpegBytes = EncodeToJpeg(colorBmp);
            byte[] alphaJpegBytes = File.ReadAllBytes(alphaPath);

            SaveTara(outputPath, colorJpegBytes, alphaJpegBytes);
        }

        public void SaveImage(
            SmokyShotRenderOptions options,
            string outputPath,
            SmokyShotComponent component
        )
        {
            BitmapSource colorBmp = ComposeColorBitmap(options, component);
            SaveImageFile(colorBmp, outputPath);
        }

        private static (
            CachedTextureData? gray,
            CachedTextureData? alpha,
            string alphaFileName
        ) GetTemplates(SmokyShotComponent component)
        {
            return component switch
            {
                SmokyShotComponent.MuzzleFlash => (
                    _flashGrayTemplate,
                    _flashAlphaTemplate,
                    "smoky_flash_alpha.jpg"
                ),
                SmokyShotComponent.CriticalHit1 => (
                    _crit1GrayTemplate,
                    _crit1AlphaTemplate,
                    "smoky_crit1_alpha.jpg"
                ),
                SmokyShotComponent.CriticalHit2 => (
                    _crit2GrayTemplate,
                    _crit2AlphaTemplate,
                    "smoky_crit2_alpha.jpg"
                ),
                SmokyShotComponent.CriticalHit3 => (
                    _crit3GrayTemplate,
                    _crit3AlphaTemplate,
                    "smoky_crit3_alpha.jpg"
                ),
                SmokyShotComponent.Bullet => (
                    _bulletGrayTemplate,
                    _bulletAlphaTemplate,
                    "smoky_bullet_alpha.jpg"
                ),
                SmokyShotComponent.Glow => (
                    _glowGrayTemplate,
                    _glowAlphaTemplate,
                    "smoky_glow_alpha.jpg"
                ),
                _ => (_flashGrayTemplate, _flashAlphaTemplate, "smoky_flash_alpha.jpg"),
            };
        }

        private static void EnsureTemplatesLoaded()
        {
            if (_flashGrayTemplate != null)
            {
                return;
            }

            string baseDir = GetTexturesDirectory();

            _flashGrayTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_flash_gray.png"));
            _flashAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_flash_alpha.jpg"));

            _crit1GrayTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_crit1_gray.png"));
            _crit1AlphaTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_crit1_alpha.jpg"));

            _crit2GrayTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_crit2_gray.png"));
            _crit2AlphaTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_crit2_alpha.jpg"));

            _crit3GrayTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_crit3_gray.png"));
            _crit3AlphaTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_crit3_alpha.jpg"));

            _bulletGrayTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_bullet_gray.png"));
            _bulletAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_bullet_alpha.jpg"));

            _glowGrayTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_glow_gray.png"));
            _glowAlphaTemplate = LoadTextureData(Path.Combine(baseDir, "smoky_glow_alpha.jpg"));
        }

        private static string GetTexturesDirectory()
        {
            string path = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Textures",
                "ShotEffects",
                "Turrets",
                "Smoky"
            );
            if (!Directory.Exists(path))
            {
                string alt = Path.Combine(
                    AppContext.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Smoky"
                );
                if (Directory.Exists(alt))
                {
                    return alt;
                }

                string asmDir =
                    Path.GetDirectoryName(typeof(SmokyShotComposer).Assembly.Location) ?? "";
                string asmPath = Path.Combine(
                    asmDir,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Smoky"
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
                    "Smoky"
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
