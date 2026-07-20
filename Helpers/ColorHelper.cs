namespace TextureSwapper.Helpers
{
    public static class ColorHelper
    {
        public static void HslToRgb(double h, double s, double l, out byte r, out byte g, out byte b)
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

            return t < 1.0 / 6.0 ? p + ((q - p) * 6.0 * t) : t < 1.0 / 2.0 ? q : t < 2.0 / 3.0 ? p + ((q - p) * ((2.0 / 3.0) - t) * 6.0) : p;
        }
    }
}
