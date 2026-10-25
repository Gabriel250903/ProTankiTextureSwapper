namespace TextureSwapper.Helpers.Utilities
{
    public static class ColorHelper
    {
        public static void HslToRgb(
            double h,
            double s,
            double l,
            out byte r,
            out byte g,
            out byte b
        )
        {
            if (s == 0)
            {
                r = g = b = (byte)Math.Clamp(l * 255.0, 0, 255);
                return;
            }

            double q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
            double p = (2.0 * l) - q;

            double rD = HueToRgb(p, q, h + (1.0 / 3.0));
            double gD = HueToRgb(p, q, h);
            double bD = HueToRgb(p, q, h - (1.0 / 3.0));

            r = (byte)Math.Clamp(rD * 255.0, 0, 255);
            g = (byte)Math.Clamp(gD * 255.0, 0, 255);
            b = (byte)Math.Clamp(bD * 255.0, 0, 255);
        }

        public static void HslToRgb(
            double h,
            double s,
            double l,
            out double r,
            out double g,
            out double b
        )
        {
            if (Math.Abs(s) < 0.0001)
            {
                r = g = b = l;
                return;
            }

            double q = l < 0.5 ? l * (1.0 + s) : l + s - (l * s);
            double p = (2.0 * l) - q;

            r = HueToRgb2(p, q, h + (1.0 / 3.0));
            g = HueToRgb2(p, q, h);
            b = HueToRgb2(p, q, h - (1.0 / 3.0));
        }

        public static double HueToRgb2(double p, double q, double t)
        {
            if (t < 0)
            {
                t += 1.0;
            }

            if (t > 1)
            {
                t -= 1.0;
            }

            return t < 1.0 / 6.0 ? p + ((q - p) * 6.0 * t)
                : t < 1.0 / 2.0 ? q
                : t < 2.0 / 3.0 ? p + ((q - p) * ((2.0 / 3.0) - t) * 6.0)
                : p;
        }

        public static void RgbToHsl(
            double r,
            double g,
            double b,
            out double h,
            out double s,
            out double l
        )
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2.0;

            if (Math.Abs(max - min) < 0.0001)
            {
                h = 0;
                s = 0;
            }
            else
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);

                h =
                    Math.Abs(max - r) < 0.0001 ? ((g - b) / d) + (g < b ? 6.0 : 0.0)
                    : Math.Abs(max - g) < 0.0001 ? ((b - r) / d) + 2.0
                    : ((r - g) / d) + 4.0;

                h /= 6.0;
            }
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0)
            {
                t += 1.0;
            }

            if (t > 1.0)
            {
                t -= 1.0;
            }

            return t < 1.0 / 6.0 ? p + ((q - p) * 6.0 * t)
                : t < 1.0 / 2.0 ? q
                : t < 2.0 / 3.0 ? p + ((q - p) * ((2.0 / 3.0) - t) * 6.0)
                : p;
        }
    }
}
