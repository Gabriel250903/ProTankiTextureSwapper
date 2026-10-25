using System.Buffers;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services.Composers
{
    public sealed class PropComposer : IPropComposer
    {
        public BitmapSource ComposePreview(byte[] originalTextureData, PropRenderOptions options)
        {
            try
            {
                BitmapSource source = LoadBitmap(originalTextureData);
                BitmapSource flipped = ApplyFlip(
                    source,
                    options.FlipHorizontal,
                    options.FlipVertical
                );
                BitmapSource filtered = ApplyFiltersAndTint(flipped, options);
                BitmapSource withDecals = ApplyDecals(filtered, options.Decals);
                BitmapSource masked = ApplyBackgroundMask(withDecals, flipped);

                masked.Freeze();
                return masked;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to compose prop preview.");
                return CreateFallbackBitmap(256, 256);
            }
        }

        private static bool IsPngHeader(byte[] data)
        {
            return data.Length >= 8
                && data[0] == 0x89
                && data[1] == 0x50
                && data[2] == 0x4E
                && data[3] == 0x47
                && data[4] == 0x0D
                && data[5] == 0x0A
                && data[6] == 0x1A
                && data[7] == 0x0A;
        }

        public byte[] ComposeTexture(
            byte[] originalTextureData,
            PropRenderOptions options,
            int jpegQuality = 90,
            string? targetFilename = null
        )
        {
            BitmapSource source = LoadBitmap(originalTextureData);
            BitmapSource flipped = ApplyFlip(source, options.FlipHorizontal, options.FlipVertical);
            BitmapSource filtered = ApplyFiltersAndTint(flipped, options);
            BitmapSource withDecals = ApplyDecals(filtered, options.Decals);
            BitmapSource masked = ApplyBackgroundMask(withDecals, flipped);

            return
                (
                    targetFilename != null
                    && targetFilename.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ) || (targetFilename == null && IsPngHeader(originalTextureData))
                ? EncodeToPng(masked)
                : EncodeToJpeg(masked, jpegQuality);
        }

        private static BitmapSource ApplyFlip(BitmapSource source, bool flipX, bool flipY)
        {
            if (!flipX && !flipY)
            {
                return source;
            }

            TransformedBitmap transformed = new();
            transformed.BeginInit();
            transformed.Source = source;
            transformed.Transform = new ScaleTransform(flipX ? -1 : 1, flipY ? -1 : 1);
            transformed.EndInit();
            transformed.Freeze();

            return transformed;
        }

        private static BitmapSource ApplyBackgroundMask(
            BitmapSource composed,
            BitmapSource original
        )
        {
            FormatConvertedBitmap compFmt = new(composed, PixelFormats.Bgra32, null, 0);
            FormatConvertedBitmap origFmt = new(original, PixelFormats.Bgra32, null, 0);

            int width = compFmt.PixelWidth;
            int height = compFmt.PixelHeight;
            int stride = width * 4;
            int bufferSize = height * stride;

            byte[] compPixels = ArrayPool<byte>.Shared.Rent(bufferSize);
            byte[] origPixels = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                compFmt.CopyPixels(compPixels, stride, 0);
                origFmt.CopyPixels(origPixels, stride, 0);

                const byte threshold = 12;

                for (int i = 0; i < width * height; i++)
                {
                    int off = i * 4;
                    byte bB = origPixels[off];
                    byte bG = origPixels[off + 1];
                    byte bR = origPixels[off + 2];
                    byte bA = origPixels[off + 3];

                    compPixels[off + 3] = bA;

                    if (bR <= threshold && bG <= threshold && bB <= threshold)
                    {
                        compPixels[off] = bB;
                        compPixels[off + 1] = bG;
                        compPixels[off + 2] = bR;
                    }
                }

                BitmapSource result = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    compPixels,
                    stride
                );
                result.Freeze();
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(compPixels);
                ArrayPool<byte>.Shared.Return(origPixels);
            }
        }

        private static BitmapSource ApplyFiltersAndTint(
            BitmapSource source,
            PropRenderOptions options
        )
        {
            FormatConvertedBitmap formatted = new(source, PixelFormats.Bgra32, null, 0);
            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            int bufferSize = height * stride;

            byte[] pixels = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                formatted.CopyPixels(pixels, stride, 0);

                double hueShift = options.Hue;
                double satMul = options.Saturation / 100.0;
                double brightnessOffset = options.Brightness / 100.0;
                double contrast = options.Contrast;

                bool hasTint =
                    options.TintStrength > 0.001 && options.TintColor != Colors.Transparent;
                double tintR = options.TintColor.R / 255.0;
                double tintG = options.TintColor.G / 255.0;
                double tintB = options.TintColor.B / 255.0;
                double tintAlpha = options.TintStrength;

                int totalPixels = width * height;
                for (int i = 0; i < totalPixels; i++)
                {
                    int off = i * 4;
                    byte bB = pixels[off];
                    byte bG = pixels[off + 1];
                    byte bR = pixels[off + 2];
                    byte bA = pixels[off + 3];

                    if (bA == 0)
                    {
                        continue;
                    }

                    if (options.ProtectEnabled)
                    {
                        double dist = Math.Sqrt(
                            Math.Pow(bR - options.ProtectColor.R, 2)
                                + Math.Pow(bG - options.ProtectColor.G, 2)
                                + Math.Pow(bB - options.ProtectColor.B, 2)
                        );
                        if (dist <= options.ProtectTolerance)
                        {
                            continue;
                        }
                    }

                    double r = bR / 255.0;
                    double g = bG / 255.0;
                    double b = bB / 255.0;

                    ColorHelper.RgbToHsl(r, g, b, out double h, out double s, out double l);

                    h = (h + (hueShift / 360.0)) % 1.0;
                    if (h < 0)
                    {
                        h += 1.0;
                    }

                    s = Math.Clamp(s * satMul, 0.0, 1.0);

                    ColorHelper.HslToRgb(h, s, l, out r, out g, out b);

                    r += brightnessOffset;
                    g += brightnessOffset;
                    b += brightnessOffset;

                    r = ((r - 0.5) * contrast) + 0.5;
                    g = ((g - 0.5) * contrast) + 0.5;
                    b = ((b - 0.5) * contrast) + 0.5;

                    if (hasTint)
                    {
                        r = (r * (1.0 - tintAlpha)) + (tintR * tintAlpha);
                        g = (g * (1.0 - tintAlpha)) + (tintG * tintAlpha);
                        b = (b * (1.0 - tintAlpha)) + (tintB * tintAlpha);
                    }

                    pixels[off] = (byte)Math.Clamp(b * 255.0, 0, 255);
                    pixels[off + 1] = (byte)Math.Clamp(g * 255.0, 0, 255);
                    pixels[off + 2] = (byte)Math.Clamp(r * 255.0, 0, 255);
                }

                BitmapSource result = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride
                );
                result.Freeze();
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixels);
            }
        }

        private static BitmapSource ApplyDecals(BitmapSource baseImage, List<DecalOverlay> decals)
        {
            if (decals.Count == 0)
            {
                return baseImage;
            }

            int width = baseImage.PixelWidth;
            int height = baseImage.PixelHeight;

            FormatConvertedBitmap formattedBase = new(baseImage, PixelFormats.Bgra32, null, 0);
            int stride = width * 4;
            int bufferSize = height * stride;
            byte[] basePixels = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                formattedBase.CopyPixels(basePixels, stride, 0);

                foreach (DecalOverlay decal in decals)
                {
                    if (string.IsNullOrEmpty(decal.ImagePath) || !File.Exists(decal.ImagePath))
                    {
                        continue;
                    }

                    try
                    {
                        BitmapImage decalBmp = new();
                        decalBmp.BeginInit();
                        decalBmp.CacheOption = BitmapCacheOption.OnLoad;
                        decalBmp.UriSource = new Uri(decal.ImagePath, UriKind.Absolute);
                        decalBmp.EndInit();
                        decalBmp.Freeze();

                        BitmapSource processedDecal = ApplyDecalFilters(
                            decalBmp,
                            decal.Brightness,
                            decal.Contrast
                        );

                        double decalWidth = processedDecal.PixelWidth * decal.Scale;
                        double decalHeight = processedDecal.PixelHeight * decal.Scale;

                        DrawingVisual visual = new();
                        using (DrawingContext dc = visual.RenderOpen())
                        {
                            TransformGroup transforms = new();
                            transforms.Children.Add(
                                new TranslateTransform(-decalWidth / 2.0, -decalHeight / 2.0)
                            );
                            transforms.Children.Add(new RotateTransform(decal.Rotation));
                            transforms.Children.Add(new TranslateTransform(decal.X, decal.Y));

                            dc.PushTransform(transforms);
                            dc.DrawImage(processedDecal, new Rect(0, 0, decalWidth, decalHeight));
                            dc.Pop();
                        }

                        RenderTargetBitmap decalRTB = new(
                            width,
                            height,
                            96,
                            96,
                            PixelFormats.Pbgra32
                        );
                        decalRTB.Render(visual);

                        FormatConvertedBitmap layerFormatted = new(
                            decalRTB,
                            PixelFormats.Bgra32,
                            null,
                            0
                        );
                        byte[] layerPixels = ArrayPool<byte>.Shared.Rent(bufferSize);

                        try
                        {
                            layerFormatted.CopyPixels(layerPixels, stride, 0);
                            CompositeLayer(
                                basePixels,
                                layerPixels,
                                width,
                                height,
                                decal.BlendMode,
                                decal.Opacity
                            );
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(layerPixels);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to apply decal overlay: {Path}", decal.ImagePath);
                    }
                }

                BitmapSource result = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    basePixels,
                    stride
                );
                result.Freeze();
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(basePixels);
            }
        }

        private static void CompositeLayer(
            byte[] basePixels,
            byte[] layerPixels,
            int width,
            int height,
            DecalBlendMode blendMode,
            double decalOpacity
        )
        {
            int total = width * height * 4;
            for (int i = 0; i < total; i += 4)
            {
                byte lA = layerPixels[i + 3];
                if (lA == 0)
                {
                    continue;
                }

                double alpha = lA / 255.0 * decalOpacity;
                if (alpha < 0.001)
                {
                    continue;
                }

                double lB = layerPixels[i] / 255.0;
                double lG = layerPixels[i + 1] / 255.0;
                double lR = layerPixels[i + 2] / 255.0;

                double bB = basePixels[i] / 255.0;
                double bG = basePixels[i + 1] / 255.0;
                double bR = basePixels[i + 2] / 255.0;

                double rR = lR,
                    rG = lG,
                    rB = lB;

                switch (blendMode)
                {
                    case DecalBlendMode.Normal:
                        rR = lR;
                        rG = lG;
                        rB = lB;
                        break;
                    case DecalBlendMode.Multiply:
                        rR = bR * lR;
                        rG = bG * lG;
                        rB = bB * lB;
                        break;
                    case DecalBlendMode.Screen:
                        rR = 1.0 - ((1.0 - bR) * (1.0 - lR));
                        rG = 1.0 - ((1.0 - bG) * (1.0 - lG));
                        rB = 1.0 - ((1.0 - bB) * (1.0 - lB));
                        break;
                    case DecalBlendMode.Overlay:
                        rR = bR < 0.5 ? (2.0 * bR * lR) : (1.0 - (2.0 * (1.0 - bR) * (1.0 - lR)));
                        rG = bG < 0.5 ? (2.0 * bG * lG) : (1.0 - (2.0 * (1.0 - bG) * (1.0 - lG)));
                        rB = bB < 0.5 ? (2.0 * bB * lB) : (1.0 - (2.0 * (1.0 - bB) * (1.0 - lB)));
                        break;
                }

                basePixels[i] = (byte)
                    Math.Clamp(((rB * alpha) + (bB * (1.0 - alpha))) * 255.0, 0, 255);
                basePixels[i + 1] = (byte)
                    Math.Clamp(((rG * alpha) + (bG * (1.0 - alpha))) * 255.0, 0, 255);
                basePixels[i + 2] = (byte)
                    Math.Clamp(((rR * alpha) + (bR * (1.0 - alpha))) * 255.0, 0, 255);
            }
        }

        private static BitmapSource ApplyDecalFilters(
            BitmapSource source,
            double brightnessOffset,
            double contrast
        )
        {
            if (Math.Abs(brightnessOffset) < 0.001 && Math.Abs(contrast - 1.0) < 0.001)
            {
                return source;
            }

            FormatConvertedBitmap formatted = new(source, PixelFormats.Bgra32, null, 0);
            int width = formatted.PixelWidth;
            int height = formatted.PixelHeight;
            int stride = width * 4;
            int bufferSize = height * stride;

            byte[] pixels = ArrayPool<byte>.Shared.Rent(bufferSize);
            try
            {
                formatted.CopyPixels(pixels, stride, 0);

                double bOffset = brightnessOffset / 100.0;

                for (int i = 0; i < width * height; i++)
                {
                    int off = i * 4;
                    byte bA = pixels[off + 3];

                    if (bA == 0)
                    {
                        continue;
                    }

                    double b = pixels[off] / 255.0;
                    double g = pixels[off + 1] / 255.0;
                    double r = pixels[off + 2] / 255.0;

                    r += bOffset;
                    g += bOffset;
                    b += bOffset;

                    r = ((r - 0.5) * contrast) + 0.5;
                    g = ((g - 0.5) * contrast) + 0.5;
                    b = ((b - 0.5) * contrast) + 0.5;

                    pixels[off] = (byte)Math.Clamp(b * 255.0, 0, 255);
                    pixels[off + 1] = (byte)Math.Clamp(g * 255.0, 0, 255);
                    pixels[off + 2] = (byte)Math.Clamp(r * 255.0, 0, 255);
                }

                BitmapSource result = BitmapSource.Create(
                    width,
                    height,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride
                );
                result.Freeze();
                return result;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(pixels);
            }
        }

        private static byte[] EncodeToJpeg(BitmapSource bitmap, int quality)
        {
            FormatConvertedBitmap bgr = new(bitmap, PixelFormats.Bgr24, null, 0);

            JpegBitmapEncoder encoder = new() { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(bgr));

            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static byte[] EncodeToPng(BitmapSource bitmap)
        {
            FormatConvertedBitmap bgra = new(bitmap, PixelFormats.Bgra32, null, 0);

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bgra));

            using MemoryStream ms = new();
            encoder.Save(ms);
            return ms.ToArray();
        }

        private static BitmapImage LoadBitmap(byte[] data)
        {
            using MemoryStream ms = new(data);
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource CreateFallbackBitmap(int width, int height)
        {
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            Array.Fill(pixels, (byte)64);
            BitmapSource bmp = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgr32,
                null,
                pixels,
                stride
            );
            bmp.Freeze();
            return bmp;
        }
    }
}
