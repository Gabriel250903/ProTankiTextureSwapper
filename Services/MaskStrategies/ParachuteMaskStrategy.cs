using System.Windows;
using System.Windows.Media;
using TextureSwapper.Core.Interfaces;

namespace TextureSwapper.Services.MaskStrategies
{
    public class ParachuteMaskStrategy : IMaskStrategy
    {
        private static readonly PathGeometry PolygonGeometry;

        static ParachuteMaskStrategy()
        {
            double cx = 219.0;
            double cy = 255.0;
            double radius = 219.0;

            Point[] vertices = new Point[12];
            for (int i = 0; i < 12; i++)
            {
                double angle = (i * Math.PI / 6.0) - (Math.PI / 2.0);
                double x = cx + (radius * Math.Cos(angle));
                double y = cy + (radius * Math.Sin(angle));
                vertices[i] = new Point(x, y);
            }

            PathFigure figure = new()
            {
                StartPoint = vertices[0],
                IsClosed = true,
                IsFilled = true,
            };

            for (int i = 1; i < 12; i++)
            {
                figure.Segments.Add(new LineSegment(vertices[i], true));
            }

            PolygonGeometry = new PathGeometry([figure]);
            PolygonGeometry.Freeze();
        }

        public Geometry? GetClipGeometry(double canvasWidth, double canvasHeight)
        {
            return PolygonGeometry;
        }

        public double GetPixelMaskWeight(int x, int y, double rawLuminance, double rawAlpha)
        {
            return 1.0;
        }
    }
}
