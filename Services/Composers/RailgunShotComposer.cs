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
                        int flareRadius = (int)
                            Math.Round(45 * Math.Clamp(options.ChargeSize, 0.5, 2.5));

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
                string alphaTarget,
                string legacyTarget,
                bool isCharge
            )[] texturesToRecolor =
            [
                (
                    _charge1Template,
                    Constants.RailgunCharge1Target,
                    Constants.RailgunCharge1AlphaTarget,
                    Constants.RailgunLegacyCharge1Target,
                    true
                ),
                (
                    _charge2Template,
                    Constants.RailgunCharge2Target,
                    Constants.RailgunCharge2AlphaTarget,
                    Constants.RailgunLegacyCharge2Target,
                    true
                ),
                (
                    _chargeSphereTemplate,
                    Constants.RailgunChargeSphereTarget,
                    Constants.RailgunChargeSphereAlphaTarget,
                    Constants.RailgunLegacyChargeSphereTarget,
                    true
                ),
                (
                    _beamCoreTemplate,
                    Constants.RailgunBeamCoreTarget,
                    Constants.RailgunBeamCoreAlphaTarget,
                    Constants.RailgunLegacyBeamCoreTarget,
                    false
                ),
                (
                    _beamSpiralTemplate,
                    Constants.RailgunBeamSpiralTarget,
                    Constants.RailgunBeamSpiralAlphaTarget,
                    Constants.RailgunLegacyBeamSpiralTarget,
                    false
                ),
            ];

            int savedCount = 0;
            foreach (
                (
                    CachedTextureData? template,
                    string primaryTarget,
                    string alphaTarget,
                    string legacyTarget,
                    bool isCharge
                ) in texturesToRecolor
            )
            {
                if (template == null)
                {
                    continue;
                }

                CachedTextureData sourceTemplate =
                    isCharge && Math.Abs(options.ChargeSize - 1.0) > 0.001
                        ? ScaleCentered(template, options.ChargeSize)
                        : template;

                byte[] recoloredPixels = RecolorPixels(sourceTemplate, options, isGameTexture: true);
                byte[] alphaPixels = BuildAlphaPixels(recoloredPixels, sourceTemplate.Width * sourceTemplate.Height);
                try
                {
                    BitmapSource bmp = BitmapSource.Create(
                        sourceTemplate.Width,
                        sourceTemplate.Height,
                        96,
                        96,
                        PixelFormats.Bgr32,
                        null,
                        recoloredPixels,
                        sourceTemplate.Width * 4
                    );

                    byte[] jpegBytes = EncodeToJpeg(bmp, 95);

                    BitmapSource alphaBmp = BitmapSource.Create(
                        sourceTemplate.Width,
                        sourceTemplate.Height,
                        96,
                        96,
                        PixelFormats.Bgr32,
                        null,
                        alphaPixels,
                        sourceTemplate.Width * 4
                    );
                    byte[] alphaJpegBytes = EncodeToJpeg(alphaBmp, 95);

                    File.WriteAllBytes(Path.Combine(outputDirectory, primaryTarget), jpegBytes);
                    File.WriteAllBytes(Path.Combine(outputDirectory, alphaTarget), alphaJpegBytes);
                    File.WriteAllBytes(Path.Combine(outputDirectory, legacyTarget), jpegBytes);
                    savedCount++;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(recoloredPixels);
                    ArrayPool<byte>.Shared.Return(alphaPixels);
                }
            }

            if (savedCount == 0)
            {
                Log.Error("Failed to save custom Railgun textures: no templates could be loaded.");
                throw new InvalidOperationException(
                    "Failed to generate custom Railgun textures: shot effect templates could not be loaded."
                );
            }

            Log.Information(
                $"Successfully saved custom Railgun textures ({savedCount} files) to {outputDirectory}"
            );
        }

        private static byte[] RecolorPixels(
            CachedTextureData template,
            RailgunShotRenderOptions options,
            bool isGameTexture = false
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

                double luminance = Math.Max(r, Math.Max(g, b)) / 255.0;
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

                output[offset] = (byte)Math.Clamp(outB * 255.0, 0, 255);
                output[offset + 1] = (byte)Math.Clamp(outG * 255.0, 0, 255);
                output[offset + 2] = (byte)Math.Clamp(outR * 255.0, 0, 255);
                output[offset + 3] = 255;
            }

            return output;
        }

        private static byte[] BuildAlphaPixels(byte[] recoloredBgr32, int totalPixels)
        {
            byte[] alpha = ArrayPool<byte>.Shared.Rent(totalPixels * 4);
            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                byte peak = Math.Max(
                    recoloredBgr32[offset],
                    Math.Max(recoloredBgr32[offset + 1], recoloredBgr32[offset + 2])
                );
                byte a = peak < 4 ? (byte)0 : peak;
                alpha[offset] = a;
                alpha[offset + 1] = a;
                alpha[offset + 2] = a;
                alpha[offset + 3] = 255;
            }
            return alpha;
        }

        private static CachedTextureData ScaleCentered(CachedTextureData template, double scale)
        {
            double clampedScale = Math.Clamp(scale, 0.5, 3.0);
            int width = template.Width;
            int height = template.Height;
            byte[] src = template.Pixels;
            byte[] dst = new byte[width * height * 4];

            double cx = (width - 1) * 0.5;
            double cy = (height - 1) * 0.5;
            double invScale = 1.0 / clampedScale;

            for (int y = 0; y < height; y++)
            {
                double sy = ((y - cy) * invScale) + cy;
                if (sy < 0 || sy > height - 1)
                {
                    for (int x = 0; x < width; x++)
                    {
                        dst[((y * width) + x) * 4 + 3] = 255;
                    }
                    continue;
                }

                int y0 = (int)Math.Floor(sy);
                int y1 = Math.Min(y0 + 1, height - 1);
                double fy = sy - y0;

                for (int x = 0; x < width; x++)
                {
                    int dstOffset = ((y * width) + x) * 4;
                    double sx = ((x - cx) * invScale) + cx;
                    if (sx < 0 || sx > width - 1)
                    {
                        dst[dstOffset + 3] = 255;
                        continue;
                    }

                    int x0 = (int)Math.Floor(sx);
                    int x1 = Math.Min(x0 + 1, width - 1);
                    double fx = sx - x0;

                    int i00 = ((y0 * width) + x0) * 4;
                    int i10 = ((y0 * width) + x1) * 4;
                    int i01 = ((y1 * width) + x0) * 4;
                    int i11 = ((y1 * width) + x1) * 4;

                    for (int c = 0; c < 3; c++)
                    {
                        double top = src[i00 + c] + ((src[i10 + c] - src[i00 + c]) * fx);
                        double bot = src[i01 + c] + ((src[i11 + c] - src[i01 + c]) * fx);
                        double val = top + ((bot - top) * fy);
                        double dx = (x - cx) / cx;
                        double dy = (y - cy) / cy;
                        double r = Math.Sqrt((dx * dx) + (dy * dy));
                        if (r > 0.72)
                        {
                            double fade = Math.Clamp((0.98 - r) / (0.98 - 0.72), 0.0, 1.0);
                            val *= fade * fade;
                        }
                        dst[dstOffset + c] = (byte)Math.Clamp(val, 0.0, 255.0);
                    }
                    dst[dstOffset + 3] = 255;
                }
            }

            return new CachedTextureData
            {
                Width = width,
                Height = height,
                Pixels = dst,
            };
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
