using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services.Composers
{
    public class RailgunShotComposer : BaseShotEffectComposer, IRailgunShotComposer
    {
        private static CachedTextureData? _charge1Template;
        private static CachedTextureData? _charge2Template;
        private static CachedTextureData? _chargeSphereTemplate;
        private static CachedTextureData? _beamCoreTemplate;
        private static CachedTextureData? _beamSpiralTemplate;
        private static readonly object _lock = new();

        public BitmapSource ComposePreview(RailgunShotRenderOptions options)
        {
            EnsureTemplatesLoaded();

            const int previewWidth = 600;
            const int previewHeight = 280;
            int stride = previewWidth * 4;
            byte[] previewPixels = ArrayPool<byte>.Shared.Rent(previewHeight * stride);

            try
            {
                for (int i = 0; i < previewWidth * previewHeight; i++)
                {
                    int offset = i * 4;
                    previewPixels[offset] = 26;
                    previewPixels[offset + 1] = 21;
                    previewPixels[offset + 2] = 19;
                    previewPixels[offset + 3] = 255;
                }

                ColorHelper.HslToRgb(
                    options.Hue / 360.0,
                    options.Saturation / 100.0,
                    0.5,
                    out byte pureR,
                    out byte pureG,
                    out byte pureB
                );

                int centerY = previewHeight / 2;
                int startX = 65;
                int endX = previewWidth - 25;
                int beamLength = endX - startX;

                int auraRadius = 42;
                for (int x = startX; x <= endX; x++)
                {
                    double u = (double)(x - startX) / beamLength;
                    double fade = 1.0;
                    if (u > 0.85)
                    {
                        fade = (1.0 - u) / 0.15;
                    }

                    if (u < 0.05)
                    {
                        fade = u / 0.05;
                    }

                    for (int dy = -auraRadius; dy <= auraRadius; dy++)
                    {
                        int y = centerY + dy;
                        if (y is < 0 or >= previewHeight)
                        {
                            continue;
                        }

                        double d = Math.Abs(dy) / (double)auraRadius;
                        double intensity = Math.Pow(1.0 - d, 2.2) * 0.40 * fade;

                        int dstOffset = ((y * previewWidth) + x) * 4;
                        previewPixels[dstOffset] = (byte)
                            Math.Min(255, previewPixels[dstOffset] + (pureB * intensity));
                        previewPixels[dstOffset + 1] = (byte)
                            Math.Min(255, previewPixels[dstOffset + 1] + (pureG * intensity));
                        previewPixels[dstOffset + 2] = (byte)
                            Math.Min(255, previewPixels[dstOffset + 2] + (pureR * intensity));
                    }
                }

                if (_beamSpiralTemplate != null)
                {
                    byte[] recoloredSpiral = RecolorPixels(_beamSpiralTemplate, options);
                    try
                    {
                        int spiralW = _beamSpiralTemplate.Width;
                        int spiralH = _beamSpiralTemplate.Height;
                        int spiralRadius = 26;

                        for (int x = startX; x <= endX; x++)
                        {
                            double u = (double)(x - startX) / beamLength;
                            double fade = 1.0;
                            if (u > 0.85)
                            {
                                fade = (1.0 - u) / 0.15;
                            }

                            int texY = (int)(u * 2.5 * spiralH) % spiralH;

                            for (int dy = -spiralRadius; dy <= spiralRadius; dy++)
                            {
                                int y = centerY + dy;
                                if (y is < 0 or >= previewHeight)
                                {
                                    continue;
                                }

                                double v = (dy + spiralRadius) / (double)(spiralRadius * 2);
                                int texX = Math.Clamp((int)(v * spiralW), 0, spiralW - 1);

                                int srcOffset = ((texY * spiralW) + texX) * 4;
                                byte spB = recoloredSpiral[srcOffset];
                                byte spG = recoloredSpiral[srcOffset + 1];
                                byte spR = recoloredSpiral[srcOffset + 2];

                                double lum =
                                    ((0.299 * spR) + (0.587 * spG) + (0.114 * spB)) / 255.0;
                                if (lum > 0.03)
                                {
                                    double vFalloff = Math.Sin(v * Math.PI);
                                    double factor = lum * 1.1 * fade * vFalloff;

                                    int dstOffset = ((y * previewWidth) + x) * 4;
                                    previewPixels[dstOffset] = (byte)
                                        Math.Min(255, previewPixels[dstOffset] + (pureB * factor));
                                    previewPixels[dstOffset + 1] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset + 1] + (pureG * factor)
                                        );
                                    previewPixels[dstOffset + 2] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset + 2] + (pureR * factor)
                                        );
                                }
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(recoloredSpiral);
                    }
                }

                if (_beamCoreTemplate != null)
                {
                    byte[] recoloredCore = RecolorPixels(_beamCoreTemplate, options);
                    try
                    {
                        int coreW = _beamCoreTemplate.Width;
                        int coreH = _beamCoreTemplate.Height;
                        int coreRadius = 8;

                        for (int x = startX; x <= endX; x++)
                        {
                            double u = (double)(x - startX) / beamLength;
                            double fade = 1.0;
                            if (u > 0.90)
                            {
                                fade = (1.0 - u) / 0.10;
                            }

                            int texY = (int)(u * coreH) % coreH;

                            for (int dy = -coreRadius; dy <= coreRadius; dy++)
                            {
                                int y = centerY + dy;
                                if (y is < 0 or >= previewHeight)
                                {
                                    continue;
                                }

                                double v = (dy + coreRadius) / (double)(coreRadius * 2);
                                int texX = Math.Clamp((int)(v * coreW), 0, coreW - 1);

                                int srcOffset = ((texY * coreW) + texX) * 4;
                                byte cB = recoloredCore[srcOffset];
                                byte cG = recoloredCore[srcOffset + 1];
                                byte cR = recoloredCore[srcOffset + 2];

                                double lum = ((0.299 * cR) + (0.587 * cG) + (0.114 * cB)) / 255.0;
                                if (lum > 0.02)
                                {
                                    double factor = lum * fade;
                                    double coreWhite =
                                        Math.Pow(
                                            Math.Max(
                                                0.0,
                                                1.0 - (Math.Abs(dy) / (double)coreRadius)
                                            ),
                                            2.0
                                        )
                                        * lum
                                        * fade
                                        * 0.90;

                                    int dstOffset = ((y * previewWidth) + x) * 4;
                                    previewPixels[dstOffset] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset]
                                                + (pureB * factor)
                                                + (255 * coreWhite)
                                        );
                                    previewPixels[dstOffset + 1] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset + 1]
                                                + (pureG * factor)
                                                + (255 * coreWhite)
                                        );
                                    previewPixels[dstOffset + 2] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset + 2]
                                                + (pureR * factor)
                                                + (255 * coreWhite)
                                        );
                                }
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(recoloredCore);
                    }
                }

                CachedTextureData? flareTemplate = _charge1Template ?? _chargeSphereTemplate;
                if (flareTemplate != null)
                {
                    byte[] recoloredFlare = RecolorPixels(flareTemplate, options);
                    try
                    {
                        int flareW = flareTemplate.Width;
                        int flareH = flareTemplate.Height;
                        int flareRadius = 45;

                        for (int dy = -flareRadius; dy <= flareRadius; dy++)
                        {
                            int y = centerY + dy;
                            if (y is < 0 or >= previewHeight)
                            {
                                continue;
                            }

                            for (int dx = -flareRadius; dx <= flareRadius; dx++)
                            {
                                int x = startX + dx;
                                if (x is < 0 or >= previewWidth)
                                {
                                    continue;
                                }

                                double u = (dx + flareRadius) / (double)(flareRadius * 2);
                                double v = (dy + flareRadius) / (double)(flareRadius * 2);
                                int texX = Math.Clamp((int)(u * flareW), 0, flareW - 1);
                                int texY = Math.Clamp((int)(v * flareH), 0, flareH - 1);

                                int srcOffset = ((texY * flareW) + texX) * 4;
                                byte flB = recoloredFlare[srcOffset];
                                byte flG = recoloredFlare[srcOffset + 1];
                                byte flR = recoloredFlare[srcOffset + 2];

                                double lum =
                                    ((0.299 * flR) + (0.587 * flG) + (0.114 * flB)) / 255.0;
                                if (lum > 0.03)
                                {
                                    double coreWhite = Math.Pow(lum, 3.0) * 0.75;

                                    int dstOffset = ((y * previewWidth) + x) * 4;
                                    previewPixels[dstOffset] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset] + flB + (255 * coreWhite)
                                        );
                                    previewPixels[dstOffset + 1] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset + 1] + flG + (255 * coreWhite)
                                        );
                                    previewPixels[dstOffset + 2] = (byte)
                                        Math.Min(
                                            255,
                                            previewPixels[dstOffset + 2] + flR + (255 * coreWhite)
                                        );
                                }
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(recoloredFlare);
                    }
                }

                BitmapSource preview = BitmapSource.Create(
                    previewWidth,
                    previewHeight,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    previewPixels,
                    stride
                );
                preview.Freeze();
                return preview;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(previewPixels);
            }
        }

        public void SaveRailgunTextures(RailgunShotRenderOptions options, string outputDirectory)
        {
            EnsureTemplatesLoaded();

            if (!Directory.Exists(outputDirectory))
            {
                _ = Directory.CreateDirectory(outputDirectory);
            }

            (
                CachedTextureData? template,
                string primaryTarget,
                string legacyTarget
            )[] texturesToRecolor =
            [
                (
                    _charge1Template,
                    Constants.RailgunCharge1Target,
                    Constants.RailgunLegacyCharge1Target
                ),
                (
                    _charge2Template,
                    Constants.RailgunCharge2Target,
                    Constants.RailgunLegacyCharge2Target
                ),
                (
                    _chargeSphereTemplate,
                    Constants.RailgunChargeSphereTarget,
                    Constants.RailgunLegacyChargeSphereTarget
                ),
                (
                    _beamCoreTemplate,
                    Constants.RailgunBeamCoreTarget,
                    Constants.RailgunLegacyBeamCoreTarget
                ),
                (
                    _beamSpiralTemplate,
                    Constants.RailgunBeamSpiralTarget,
                    Constants.RailgunLegacyBeamSpiralTarget
                ),
            ];

            RailgunShotRenderOptions gameOptions = new()
            {
                Hue = options.Hue,
                Saturation = options.Saturation,
                Brightness = options.Brightness,
                Contrast = options.Contrast,
                Invert = options.Invert,
            };

            int savedCount = 0;
            foreach (
                (
                    CachedTextureData? template,
                    string primaryTarget,
                    string legacyTarget
                ) in texturesToRecolor
            )
            {
                if (template == null)
                {
                    continue;
                }

                byte[] recoloredPixels = RecolorPixels(
                    template,
                    gameOptions,
                    swapChannelsForGame: true
                );
                try
                {
                    BitmapSource bmp = BitmapSource.Create(
                        template.Width,
                        template.Height,
                        96,
                        96,
                        PixelFormats.Bgr32,
                        null,
                        recoloredPixels,
                        template.Width * 4
                    );

                    byte[] jpegBytes = EncodeToJpeg(bmp, 95);

                    File.WriteAllBytes(Path.Combine(outputDirectory, primaryTarget), jpegBytes);
                    File.WriteAllBytes(Path.Combine(outputDirectory, legacyTarget), jpegBytes);
                    savedCount++;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(recoloredPixels);
                }
            }

            if (savedCount == 0)
            {
                Log.Error("Failed to save custom Railgun textures: no templates could be loaded.");
                throw new InvalidOperationException("Failed to generate custom Railgun textures: shot effect templates could not be loaded.");
            }

            Log.Information($"Successfully saved custom Railgun textures ({savedCount} files) to {outputDirectory}");
        }

        private static byte[] RecolorPixels(
            CachedTextureData template,
            RailgunShotRenderOptions options,
            bool swapChannelsForGame = false
        )
        {
            int totalPixels = template.Width * template.Height;
            int stride = template.Width * 4;
            byte[] output = ArrayPool<byte>.Shared.Rent(template.Height * stride);
            byte[] input = template.Pixels;

            double hue = options.Hue;
            double saturation = options.Saturation;
            double brightnessOffset = options.Brightness / 100.0;
            double contrast = options.Contrast;
            bool invert = options.Invert;

            ColorHelper.HslToRgb(
                hue / 360.0,
                saturation / 100.0,
                0.5,
                out byte pureR,
                out byte pureG,
                out byte pureB
            );

            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                byte b = input[offset];
                byte g = input[offset + 1];
                byte r = input[offset + 2];

                if (r == 0 && g == 0 && b == 0)
                {
                    output[offset] = 0;
                    output[offset + 1] = 0;
                    output[offset + 2] = 0;
                    output[offset + 3] = 255;
                    continue;
                }

                double luminance = ((0.299 * r) + (0.587 * g) + (0.114 * b)) / 255.0;
                if (luminance < 0.005)
                {
                    output[offset] = 0;
                    output[offset + 1] = 0;
                    output[offset + 2] = 0;
                    output[offset + 3] = 255;
                    continue;
                }

                if (invert)
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

                double finalB = swapChannelsForGame ? outR : outB;
                double finalR = swapChannelsForGame ? outB : outR;

                output[offset] = (byte)Math.Clamp(finalB * 255.0, 0, 255);
                output[offset + 1] = (byte)Math.Clamp(outG * 255.0, 0, 255);
                output[offset + 2] = (byte)Math.Clamp(finalR * 255.0, 0, 255);
                output[offset + 3] = 255;
            }

            return output;
        }

        private static void EnsureTemplatesLoaded()
        {
            if (
                _charge1Template != null
                && _charge2Template != null
                && _chargeSphereTemplate != null
                && _beamCoreTemplate != null
                && _beamSpiralTemplate != null
            )
            {
                return;
            }

            lock (_lock)
            {
                if (
                    _charge1Template != null
                    && _charge2Template != null
                    && _chargeSphereTemplate != null
                    && _beamCoreTemplate != null
                    && _beamSpiralTemplate != null
                )
                {
                    return;
                }

                string baseDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Railgun",
                    "Templates"
                );

                if (!Directory.Exists(baseDir))
                {
                    string currentDirCandidate = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "Textures",
                        "ShotEffects",
                        "Turrets",
                        "Railgun",
                        "Templates"
                    );
                    if (Directory.Exists(currentDirCandidate))
                    {
                        baseDir = currentDirCandidate;
                    }
                }

                _charge1Template = LoadTextureData(
                    Path.Combine(baseDir, Constants.RailgunCharge1Target)
                );
                _charge2Template = LoadTextureData(
                    Path.Combine(baseDir, Constants.RailgunCharge2Target)
                );
                _chargeSphereTemplate = LoadTextureData(
                    Path.Combine(baseDir, Constants.RailgunChargeSphereTarget)
                );
                _beamCoreTemplate = LoadTextureData(
                    Path.Combine(baseDir, Constants.RailgunBeamCoreTarget)
                );
                _beamSpiralTemplate = LoadTextureData(
                    Path.Combine(baseDir, Constants.RailgunBeamSpiralTarget)
                );
            }
        }
    }
}
