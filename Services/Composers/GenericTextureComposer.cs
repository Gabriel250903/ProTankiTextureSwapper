using System.Collections.Concurrent;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Services.MaskStrategies;

namespace TextureSwapper.Services.Composers
{
    public class GenericTextureComposer : ITextureComposer
    {
        private static readonly ConcurrentDictionary<string, BitmapImage?> ImageCache = new();
        private static readonly ConcurrentQueue<string> ImageCacheKeys = new();
        private const int MaxCacheSize = 30;

        public BitmapSource Compose(TextureRenderOptions options, IMaskStrategy maskStrategy)
        {
            return maskStrategy is ParachuteMaskStrategy
                ? ComposeVectorCanvas(options, maskStrategy)
                : ComposePixelArray(options);
        }

        private RenderTargetBitmap ComposeVectorCanvas(
            TextureRenderOptions options,
            IMaskStrategy maskStrategy
        )
        {
            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                BitmapImage? templateImage = GetCachedBitmap(options.BaseTemplatePath);

                if (templateImage != null)
                {
                    dc.DrawImage(templateImage, new Rect(0, 0, 512, 512));
                }
                else
                {
                    SolidColorBrush bgBrush = new(Color.FromRgb(38, 38, 38));
                    dc.DrawRectangle(bgBrush, null, new Rect(0, 0, 512, 512));
                }

                Geometry? clipGeo = maskStrategy.GetClipGeometry(512, 512);
                if (clipGeo != null)
                {
                    dc.PushClip(clipGeo);
                }

                if (
                    !string.IsNullOrEmpty(options.SourceImagePath)
                    && File.Exists(options.SourceImagePath)
                )
                {
                    BitmapImage? sourceImage = GetCachedBitmap(options.SourceImagePath);
                    if (sourceImage != null)
                    {
                        double imgW = sourceImage.PixelWidth;
                        double imgH = sourceImage.PixelHeight;

                        TransformGroup transformGroup = new();
                        transformGroup.Children.Add(
                            new ScaleTransform(options.Scale, options.Scale, 219, 255)
                        );
                        transformGroup.Children.Add(
                            new RotateTransform(options.RotationAngle, 219, 255)
                        );
                        transformGroup.Children.Add(
                            new TranslateTransform(options.OffsetX, options.OffsetY)
                        );

                        if (options.TileMode)
                        {
                            ImageBrush tileBrush = new(sourceImage)
                            {
                                TileMode = TileMode.Tile,
                                ViewportUnits = BrushMappingMode.Absolute,
                                Viewport = new Rect(
                                    0,
                                    0,
                                    Math.Max(10, imgW * options.Scale),
                                    Math.Max(10, imgH * options.Scale)
                                ),
                                Transform = transformGroup,
                            };
                            dc.DrawRectangle(tileBrush, null, new Rect(0, 0, 512, 512));
                        }
                        else
                        {
                            dc.PushTransform(transformGroup);
                            double left = 219.0 - (imgW / 2.0);
                            double top = 255.0 - (imgH / 2.0);
                            dc.DrawImage(sourceImage, new Rect(left, top, imgW, imgH));
                            dc.Pop();
                        }
                    }
                }

                if (clipGeo != null)
                {
                    dc.Pop();
                    Pen outlinePen = new(
                        new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                        1.5
                    );
                    dc.DrawGeometry(null, outlinePen, clipGeo);
                }
            }

            RenderTargetBitmap renderBitmap = new(512, 512, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            renderBitmap.Freeze();
            return renderBitmap;
        }

        private static BitmapSource ComposePixelArray(TextureRenderOptions options)
        {
            Log.Warning("GenericTextureComposer: Unsupported or legacy mask strategy requested for composition.");
            RenderTargetBitmap fallback = new(512, 512, 96, 96, PixelFormats.Pbgra32);
            fallback.Freeze();
            return fallback;
        }

        public void SaveImage(
            TextureRenderOptions options,
            IMaskStrategy maskStrategy,
            string outputPath,
            bool isJpeg = true
        )
        {
            BitmapSource bitmap = Compose(options, maskStrategy);
            BitmapEncoder encoder = isJpeg
                ? new JpegBitmapEncoder { QualityLevel = 95 }
                : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
        }

        public void ClearCache()
        {
            ImageCache.Clear();
            while (ImageCacheKeys.TryDequeue(out _)) { }
        }

        private static BitmapImage? GetCachedBitmap(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            if (ImageCache.TryGetValue(filePath, out BitmapImage? cached))
            {
                return cached;
            }

            try
            {
                BitmapImage bitmap = new();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                ImageCache[filePath] = bitmap;
                ImageCacheKeys.Enqueue(filePath);

                while (
                    ImageCacheKeys.Count > MaxCacheSize
                    && ImageCacheKeys.TryDequeue(out string? oldKey)
                )
                {
                    _ = ImageCache.TryRemove(oldKey, out _);
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
