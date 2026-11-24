using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services.Composers
{
    public abstract class BaseShotEffectComposer
    {
        private static readonly HttpClient _templateHttpClient = new(
            new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(10),
            }
        )
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        static BaseShotEffectComposer()
        {
            _templateHttpClient.DefaultRequestHeaders.Add("User-Agent", "TextureSwapper/2.0");
        }

        public class CachedTextureData
        {
            public int Width { get; init; }
            public int Height { get; init; }
            public byte[] Pixels { get; init; } = [];
        }

        private static bool TryDownloadTemplate(string fullPath)
        {
            try
            {
                string normalized = fullPath.Replace('\\', '/');
                int texIndex = normalized.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase);
                string relativePath;
                if (texIndex >= 0)
                {
                    relativePath = normalized[(texIndex + 1)..];
                }
                else if (normalized.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = normalized;
                }
                else
                {
                    return false;
                }

                string url = $"{Constants.GitHubRawUrl}/{relativePath}";

                Log.Information(
                    "Attempting to auto-download missing shot effect template: {Url}",
                    url
                );

                using HttpResponseMessage response = _templateHttpClient
                    .GetAsync(url)
                    .GetAwaiter()
                    .GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning(
                        "Failed to download template ({StatusCode}): {Url}",
                        response.StatusCode,
                        url
                    );
                    return false;
                }

                byte[] data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (data.Length == 0)
                {
                    return false;
                }

                string? dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    _ = Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(fullPath, data);
                Log.Information("Successfully downloaded template: {Path}", fullPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to auto-download template for {Path}", fullPath);
                return false;
            }
        }

        private static readonly ConcurrentDictionary<string, CachedTextureData?> _textureCache =
            new();

        protected static CachedTextureData? LoadTextureData(string path)
        {
            try
            {
                string fullPath = Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

                if (_textureCache.TryGetValue(fullPath, out CachedTextureData? cached))
                {
                    return cached;
                }

                if (!File.Exists(fullPath))
                {
                    if (!TryDownloadTemplate(fullPath))
                    {
                        Log.Debug("Template file not found: {Path}", fullPath);
                        _textureCache[fullPath] = null;
                        return null;
                    }
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

                CachedTextureData result = new()
                {
                    Width = width,
                    Height = height,
                    Pixels = pixels,
                };
                _textureCache[fullPath] = result;
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load texture data from {Path}", path);
                return null;
            }
        }

        private static void ComputeRecoloredRgb(
            byte gray,
            double pureR,
            double pureG,
            double pureB,
            double brightnessOffset,
            double contrast,
            bool invert,
            out byte r,
            out byte g,
            out byte b
        )
        {
            double luminance = gray / 255.0;
            if (invert)
            {
                luminance = 1.0 - luminance;
            }

            double lum = luminance + brightnessOffset;
            lum = ((lum - 0.5) * contrast) + 0.5;
            lum = Math.Clamp(lum, 0.0, 1.0);

            double outR = pureR * lum;
            double outG = pureG * lum;
            double outB = pureB * lum;

            if (lum > 0.85)
            {
                double core = (lum - 0.85) / 0.15;
                core = core * core * 0.5;
                outR += (1.0 - outR) * core;
                outG += (1.0 - outG) * core;
                outB += (1.0 - outB) * core;
            }

            r = (byte)Math.Clamp(outR * 255.0, 0, 255);
            g = (byte)Math.Clamp(outG * 255.0, 0, 255);
            b = (byte)Math.Clamp(outB * 255.0, 0, 255);
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

            double brightnessOffset = options.Brightness / 100.0;
            double contrast = options.Contrast;

            ColorHelper.HslToRgb(
                options.Hue / 360.0,
                options.Saturation / 100.0,
                0.5,
                out byte baseRed,
                out byte baseGreen,
                out byte baseBlue
            );
            double pureR = baseRed / 255.0;
            double pureG = baseGreen / 255.0;
            double pureB = baseBlue / 255.0;

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

                        ComputeRecoloredRgb(
                            gray,
                            pureR,
                            pureG,
                            pureB,
                            brightnessOffset,
                            contrast,
                            options.Invert,
                            out byte r,
                            out byte g,
                            out byte b
                        );

                        previewPixels[sheetOffset] = b;
                        previewPixels[sheetOffset + 1] = g;
                        previewPixels[sheetOffset + 2] = r;
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

            double brightnessOffset = options.Brightness / 100.0;
            double contrast = options.Contrast;
            int totalPixels = width * height;

            ColorHelper.HslToRgb(
                options.Hue / 360.0,
                options.Saturation / 100.0,
                0.5,
                out byte baseRed,
                out byte baseGreen,
                out byte baseBlue
            );
            double pureR = baseRed / 255.0;
            double pureG = baseGreen / 255.0;
            double pureB = baseBlue / 255.0;

            try
            {
                for (int i = 0; i < totalPixels; i++)
                {
                    int pixelOffset = i * 4;
                    byte gray = templatePixels[pixelOffset + 2];

                    if (gray == 0)
                    {
                        outputPixels[pixelOffset] = 0;
                        outputPixels[pixelOffset + 1] = 0;
                        outputPixels[pixelOffset + 2] = 0;
                        outputPixels[pixelOffset + 3] = 255;
                        continue;
                    }

                    ComputeRecoloredRgb(
                        gray,
                        pureR,
                        pureG,
                        pureB,
                        brightnessOffset,
                        contrast,
                        options.Invert,
                        out byte r,
                        out byte g,
                        out byte b
                    );

                    outputPixels[pixelOffset] = b;
                    outputPixels[pixelOffset + 1] = g;
                    outputPixels[pixelOffset + 2] = r;
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
