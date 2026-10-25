using System.Buffers;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services.Composers
{
    public abstract class BaseShotEffectComposer
    {
        public class CachedTextureData
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public byte[] Pixels { get; init; } = [];
        }

        protected static CachedTextureData? LoadTextureData(string path)
        {
            try
            {
                string fullPath = Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

                if (!File.Exists(fullPath))
                {
                    Log.Warning("Template file not found: {Path}", fullPath);
                    return null;
                }

                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(fullPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                FormatConvertedBitmap formatted = new(bitmap, PixelFormats.Bgra32, null, 0);
                int width = formatted.PixelWidth;
                int height = formatted.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                formatted.CopyPixels(pixels, stride, 0);

                return new CachedTextureData
                {
                    Width = width,
                    Height = height,
                    Pixels = pixels,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load texture data from {Path}", path);
                return null;
            }
        }

        protected static BitmapSource ColorizePreview(
            CachedTextureData grayData,
            CachedTextureData? alphaData,
            IShotColorOptions options
        )
        {
            int sheetWidth = grayData.Width;
            int sheetHeight = grayData.Height;
            int previewStride = sheetWidth * 4;
            byte[] previewPixels = ArrayPool<byte>.Shared.Rent(sheetHeight * previewStride);

            double hue = options.Hue;
            double saturation = options.Saturation;
            double brightness = options.Brightness;
            double contrast = options.Contrast;
            double brightnessOffset = brightness / 100.0;

            byte[] grayPixels = grayData.Pixels;
            byte[]? alphaPixels = alphaData?.Pixels;

            try
            {
                for (int y = 0; y < sheetHeight; y++)
                {
                    for (int x = 0; x < sheetWidth; x++)
                    {
                        int sheetOffset = ((y * sheetWidth) + x) * 4;

                        byte gray = grayPixels[sheetOffset + 2];
                        byte alpha = alphaPixels != null ? alphaPixels[sheetOffset + 2] : (byte)255;

                        if (gray == 0 || alpha == 0)
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

                        ColorHelper.HslToRgb(
                            hue / 360.0,
                            saturation / 100.0,
                            luminance,
                            out byte rgbRed,
                            out byte rgbGreen,
                            out byte rgbBlue
                        );

                        double r = rgbRed / 255.0;
                        double g = rgbGreen / 255.0;
                        double b = rgbBlue / 255.0;

                        r += brightnessOffset;
                        g += brightnessOffset;
                        b += brightnessOffset;

                        r = ((r - 0.5) * contrast) + 0.5;
                        g = ((g - 0.5) * contrast) + 0.5;
                        b = ((b - 0.5) * contrast) + 0.5;

                        previewPixels[sheetOffset] = (byte)Math.Clamp(b * 255.0, 0, 255);
                        previewPixels[sheetOffset + 1] = (byte)Math.Clamp(g * 255.0, 0, 255);
                        previewPixels[sheetOffset + 2] = (byte)Math.Clamp(r * 255.0, 0, 255);
                        previewPixels[sheetOffset + 3] = alpha;
                    }
                }

                BitmapSource previewBmp = BitmapSource.Create(
                    sheetWidth,
                    sheetHeight,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    previewPixels,
                    previewStride
                );
                previewBmp.Freeze();
                return previewBmp;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(previewPixels);
            }
        }

        protected static BitmapSource ColorizeRgb(
            CachedTextureData grayData,
            IShotColorOptions options
        )
        {
            int width = grayData.Width;
            int height = grayData.Height;
            int stride = width * 4;
            byte[] outputPixels = ArrayPool<byte>.Shared.Rent(height * stride);
            byte[] templatePixels = grayData.Pixels;

            double hue = options.Hue;
            double saturation = options.Saturation;
            double brightness = options.Brightness;
            double contrast = options.Contrast;
            double brightnessOffset = brightness / 100.0;
            int totalPixels = width * height;

            try
            {
                for (int i = 0; i < totalPixels; i++)
                {
                    int pixelOffset = i * 4;
                    byte gray = templatePixels[pixelOffset + 2];

                    if (gray == 0)
                    {
                        outputPixels[pixelOffset] = 255;
                        outputPixels[pixelOffset + 1] = 255;
                        outputPixels[pixelOffset + 2] = 255;
                        outputPixels[pixelOffset + 3] = 255;
                        continue;
                    }

                    double luminance = gray / 255.0;
                    if (options.Invert)
                    {
                        luminance = 1.0 - luminance;
                    }

                    ColorHelper.HslToRgb(
                        hue / 360.0,
                        saturation / 100.0,
                        luminance,
                        out byte rgbRed,
                        out byte rgbGreen,
                        out byte rgbBlue
                    );

                    double r = rgbRed / 255.0;
                    double g = rgbGreen / 255.0;
                    double b = rgbBlue / 255.0;

                    r += brightnessOffset;
                    g += brightnessOffset;
                    b += brightnessOffset;

                    r = ((r - 0.5) * contrast) + 0.5;
                    g = ((g - 0.5) * contrast) + 0.5;
                    b = ((b - 0.5) * contrast) + 0.5;

                    outputPixels[pixelOffset] = (byte)Math.Clamp(b * 255.0, 0, 255);
                    outputPixels[pixelOffset + 1] = (byte)Math.Clamp(g * 255.0, 0, 255);
                    outputPixels[pixelOffset + 2] = (byte)Math.Clamp(r * 255.0, 0, 255);
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

        protected static byte[] EncodeToJpeg(BitmapSource bitmap, int quality = 90)
        {
            using MemoryStream ms = new();
            JpegBitmapEncoder encoder = new() { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return ms.ToArray();
        }

        protected static byte[] EncodeToPng(BitmapSource bitmap)
        {
            using MemoryStream ms = new();
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return ms.ToArray();
        }

        protected static void SaveTara(
            string outputPath,
            byte[] colorJpegBytes,
            byte[] alphaJpegBytes
        )
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write);
            using BinaryWriter bw = new(fs);
            bw.Write([0, 0, 0, 2]);

            bw.Write([0, 1]);
            bw.Write((byte)'i');
            byte[] colorLenBytes = BitConverter.GetBytes((uint)colorJpegBytes.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(colorLenBytes);
            }
            bw.Write(colorLenBytes);

            bw.Write([0, 1]);
            bw.Write((byte)'a');
            byte[] alphaLenBytes = BitConverter.GetBytes((uint)alphaJpegBytes.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(alphaLenBytes);
            }
            bw.Write(alphaLenBytes);

            bw.Write(colorJpegBytes);
            bw.Write(alphaJpegBytes);
        }

        protected static void SaveImageFile(
            BitmapSource bitmap,
            string outputPath,
            bool asPng = false,
            int quality = 90
        )
        {
            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            using FileStream fs = new(outputPath, FileMode.Create, FileAccess.Write);
            BitmapEncoder encoder = asPng
                ? new PngBitmapEncoder()
                : new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(fs);
        }

        protected static BitmapSource CreateDefaultEmptyBitmap(int width = 256, int height = 128)
        {
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            Array.Fill(pixels, (byte)255);
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
