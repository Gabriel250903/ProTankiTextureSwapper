using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services.Composers
{
    public class ParachuteComposer : IParachuteComposer
    {
        private static readonly PathGeometry CanopyGeometry;
        private static readonly Point[] CanopyVertices;

        static ParachuteComposer()
        {
            double cx = 219.0;
            double cy = 255.0;
            double radius = 219.0;

            CanopyVertices = new Point[12];
            for (int i = 0; i < 12; i++)
            {
                double angle = (i * Math.PI / 6.0) - (Math.PI / 2.0);
                double x = cx + (radius * Math.Cos(angle));
                double y = cy + (radius * Math.Sin(angle));
                CanopyVertices[i] = new Point(x, y);
            }

            PathFigure figure = new()
            {
                StartPoint = CanopyVertices[0],
                IsClosed = true,
                IsFilled = true,
            };

            for (int i = 1; i < 12; i++)
            {
                figure.Segments.Add(new LineSegment(CanopyVertices[i], true));
            }

            CanopyGeometry = new PathGeometry([figure]);
            CanopyGeometry.Freeze();
        }

        public BitmapSource ComposeParachute(ParachuteRenderOptions options)
        {
            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                SolidColorBrush bgBrush = new(Color.FromRgb(38, 38, 38));

                dc.DrawRectangle(bgBrush, null, new Rect(0, 0, 512, 512));
                dc.PushClip(CanopyGeometry);

                string templatePath = GetTemplatePath();
                if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
                {
                    try
                    {
                        BitmapImage templateImg = LoadBitmapImage(templatePath);
                        dc.DrawImage(templateImg, new Rect(0, 0, 512, 512));
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "Failed to load base parachute template image.");
                    }
                }
                else
                {
                    SolidColorBrush bgFallback = new(Color.FromRgb(50, 50, 50));
                    dc.DrawRectangle(bgFallback, null, new Rect(0, 0, 512, 512));
                }

                if (
                    !string.IsNullOrEmpty(options.SourceImagePath)
                    && File.Exists(options.SourceImagePath)
                )
                {
                    try
                    {
                        BitmapImage sourceImage = LoadBitmapImage(options.SourceImagePath);

                        TransformGroup transformGroup = new();
                        transformGroup.Children.Add(
                            new ScaleTransform(options.Scale, options.Scale, 256, 256)
                        );
                        transformGroup.Children.Add(
                            new RotateTransform(options.RotationAngle, 256, 256)
                        );
                        transformGroup.Children.Add(
                            new TranslateTransform(options.OffsetX, options.OffsetY)
                        );

                        double opacity =
                            options.ShadingOpacity > 0
                                ? Math.Clamp(options.ShadingOpacity, 0.1, 1.0)
                                : 1.0;
                        dc.PushOpacity(opacity);

                        if (options.TileMode)
                        {
                            double tileW = Math.Max(10, 512.0 * options.Scale);
                            double tileH = Math.Max(10, 512.0 * options.Scale);
                            ImageBrush tileBrush = new(sourceImage)
                            {
                                TileMode = TileMode.Tile,
                                ViewportUnits = BrushMappingMode.Absolute,
                                Viewport = new Rect(0, 0, tileW, tileH),
                                Transform = transformGroup,
                            };
                            dc.DrawRectangle(tileBrush, null, new Rect(0, 0, 512, 512));
                        }
                        else
                        {
                            dc.PushTransform(transformGroup);
                            dc.DrawImage(sourceImage, new Rect(0, 0, 512, 512));
                            dc.Pop();
                        }

                        dc.Pop();
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(
                            ex,
                            "Failed to render custom texture in ParachuteComposer."
                        );
                    }
                }

                dc.Pop();
            }

            RenderTargetBitmap renderBitmap = new(512, 512, 96, 96, PixelFormats.Pbgra32);
            renderBitmap.Render(visual);
            renderBitmap.Freeze();

            return renderBitmap;
        }

        public void SaveParachuteJpg(ParachuteRenderOptions options, string outputPath)
        {
            BitmapSource bitmap = ComposeParachute(options);
            string ext = Path.GetExtension(outputPath).ToLowerInvariant();
            BitmapEncoder encoder =
                ext == ".png"
                    ? new PngBitmapEncoder()
                    : new JpegBitmapEncoder { QualityLevel = 95 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));

            string? dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            using FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
        }

        private static string GetTemplatePath()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string rel = Path.Combine(
                baseDir,
                "Textures",
                "Spawners",
                "Parachute",
                "Template",
                "para.jpg"
            );
            if (File.Exists(rel))
            {
                return rel;
            }

            string cwd = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Textures",
                "Spawners",
                "Parachute",
                "Template",
                "para.jpg"
            );
            return File.Exists(cwd) ? cwd : string.Empty;
        }

        private static BitmapImage LoadBitmapImage(string filePath)
        {
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}
