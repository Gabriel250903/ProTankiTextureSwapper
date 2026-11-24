using System.Globalization;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Helpers.Utilities
{
    public static class ShotLightingColorHelper
    {
        public static bool TryMapTurretToHtml5Kind(string? turret, out string html5Kind)
        {
            if (string.IsNullOrWhiteSpace(turret))
            {
                html5Kind = string.Empty;
                return false;
            }

            switch (turret.Trim().ToLowerInvariant())
            {
                case "railgun":
                    html5Kind = "railgun";
                    return true;
                case "twins":
                    html5Kind = "twins";
                    return true;
                case "ricochet":
                    html5Kind = "ricochet";
                    return true;
                case "isida":
                    html5Kind = "isida";
                    return true;
                case "freeze":
                    html5Kind = "freeze";
                    return true;
                case "firebird":
                    html5Kind = "flamethrower";
                    return true;
                case "thunder":
                    html5Kind = "thunder";
                    return true;
                case "smoky":
                    html5Kind = "smoky";
                    return true;
                case "shaft":
                    html5Kind = "shaft";
                    return true;
                case "hammer":
                    html5Kind = "shotgun";
                    return true;
                case "vulcan":
                    html5Kind = "machinegun";
                    return true;
                default:
                    html5Kind = string.Empty;
                    return false;
            }
        }

        public static (string ColorHex, bool IsDark) ComputeFromOptions(IShotColorOptions options)
        {
            double normalizedHue = ((options.Hue % 360.0) + 360.0) % 360.0 / 360.0;
            double saturation = Math.Clamp(options.Saturation / 100.0, 0.0, 1.0);
            double baseLum = options.Invert ? 0.30 : 0.50;
            double brightnessOffset = options.Brightness / 100.0;
            double lum = Math.Clamp(
                ((baseLum + brightnessOffset - 0.5) * options.Contrast) + 0.5,
                0.0,
                1.0
            );

            if (lum <= 0.16)
            {
                return ("#000000", true);
            }

            ColorHelper.HslToRgb(
                normalizedHue,
                saturation,
                lum,
                out byte r,
                out byte g,
                out byte b
            );
            if (r == 0 && g == 0 && b == 0)
            {
                return ("#000000", true);
            }

            return ($"#{r:X2}{g:X2}{b:X2}", false);
        }

        public static void ApplyOptionsToModel(ShotEffectModel model, IShotColorOptions options)
        {
            (string colorHex, bool isDark) = ComputeFromOptions(options);
            model.DynamicLightColorHex = colorHex;
            model.IsDarkDynamicLight = isDark;
        }

        public static void ApplyIsidaOptionsToModel(
            ShotEffectModel model,
            IShotColorOptions damageOptions,
            IShotColorOptions healOptions
        )
        {
            (string primaryHex, bool isPrimaryDark) = ComputeFromOptions(damageOptions);
            (string secondaryHex, bool isSecondaryDark) = ComputeFromOptions(healOptions);

            model.DynamicLightColorHex = primaryHex;
            model.IsDarkDynamicLight = isPrimaryDark;
            model.SecondaryDynamicLightColorHex = secondaryHex;
            model.IsSecondaryDarkDynamicLight = isSecondaryDark;
        }

        public static Html5TurretLightOverride? ResolveOverride(ShotEffectModel effect)
        {
            if (
                effect.IsDarkDynamicLight || !string.IsNullOrWhiteSpace(effect.DynamicLightColorHex)
            )
            {
                return new Html5TurretLightOverride
                {
                    ColorHex = string.IsNullOrWhiteSpace(effect.DynamicLightColorHex)
                        ? "#FFFFFF"
                        : effect.DynamicLightColorHex,
                    IsDark = effect.IsDarkDynamicLight,
                    SecondaryColorHex = effect.SecondaryDynamicLightColorHex,
                    IsSecondaryDark = effect.IsSecondaryDarkDynamicLight,
                };
            }

            string turret = effect.Turret.Trim();
            string name = effect.Name.Trim();

            if (turret.Equals("Railgun", StringComparison.OrdinalIgnoreCase))
            {
                return name.ToLowerInvariant() switch
                {
                    "darkness" or "black" => CreateOverride("#000000", isDark: true),
                    "blue" => CreateOverride("#1E90FF"),
                    "green" => CreateOverride("#00FF44"),
                    "purple" => CreateOverride("#AA33FF"),
                    "red" => CreateOverride("#FF2222"),
                    "snow" => CreateOverride("#D8F2FF"),
                    "classic 2010" => CreateOverride("#44BBFF"),
                    _ => ResolvePresetNameFallback(effect),
                };
            }

            if (turret.Equals("Isida", StringComparison.OrdinalIgnoreCase))
            {
                return name.Equals("Classic 2010", StringComparison.OrdinalIgnoreCase)
                    ? new Html5TurretLightOverride
                    {
                        ColorHex = "#FF2233",
                        IsDark = false,
                        SecondaryColorHex = "#00FF66",
                        IsSecondaryDark = false,
                    }
                    : ResolvePresetNameFallback(effect);
            }

            if (
                turret.Equals("Twins", StringComparison.OrdinalIgnoreCase)
                && name.Equals("Classic 2010", StringComparison.OrdinalIgnoreCase)
            )
            {
                return CreateOverride("#00FF44");
            }

            return turret.Equals("Firebird", StringComparison.OrdinalIgnoreCase)
                && name.Equals("Classic 2010", StringComparison.OrdinalIgnoreCase)
                    ? CreateOverride("#FF6600")
                : turret.Equals("Freeze", StringComparison.OrdinalIgnoreCase)
                && name.Equals("Classic 2010", StringComparison.OrdinalIgnoreCase)
                    ? CreateOverride("#33DDFF")
                : ResolvePresetNameFallback(effect);
        }

        public static bool TryParseColorInt(string? hex, bool isDark, out int colorInt)
        {
            if (isDark)
            {
                colorInt = -1;
                return true;
            }

            if (string.IsNullOrWhiteSpace(hex))
            {
                colorInt = 0;
                return false;
            }

            string cleaned = hex.Trim();
            if (cleaned.StartsWith('#'))
            {
                cleaned = cleaned[1..];
            }
            else if (cleaned.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned[2..];
            }

            if (
                int.TryParse(
                    cleaned,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out int parsed
                )
                && parsed >= 0
                && parsed <= 0xFFFFFF
            )
            {
                colorInt = parsed;
                return true;
            }

            colorInt = 0;
            return false;
        }

        private static Html5TurretLightOverride CreateOverride(string hex, bool isDark = false)
        {
            return new Html5TurretLightOverride { ColorHex = hex, IsDark = isDark };
        }

        private static Html5TurretLightOverride? ResolvePresetNameFallback(ShotEffectModel effect)
        {
            string combined = $"{effect.Name} {effect.SourceFolder}".ToLowerInvariant();
            if (
                combined.Contains("dark", StringComparison.Ordinal)
                || combined.Contains("black", StringComparison.Ordinal)
            )
            {
                return CreateOverride("#000000", isDark: true);
            }
            if (combined.Contains("red", StringComparison.Ordinal))
            {
                return CreateOverride("#FF2222");
            }
            if (combined.Contains("blue", StringComparison.Ordinal))
            {
                return CreateOverride("#1E90FF");
            }
            if (combined.Contains("green", StringComparison.Ordinal))
            {
                return CreateOverride("#00FF44");
            }
            if (
                combined.Contains("purple", StringComparison.Ordinal)
                || combined.Contains("violet", StringComparison.Ordinal)
            )
            {
                return CreateOverride("#AA33FF");
            }
            if (
                combined.Contains("yellow", StringComparison.Ordinal)
                || combined.Contains("gold", StringComparison.Ordinal)
            )
            {
                return CreateOverride("#FFD700");
            }
            if (combined.Contains("orange", StringComparison.Ordinal))
            {
                return CreateOverride("#FF6600");
            }
            if (
                combined.Contains("cyan", StringComparison.Ordinal)
                || combined.Contains("ice", StringComparison.Ordinal)
                || combined.Contains("snow", StringComparison.Ordinal)
            )
            {
                return CreateOverride("#44DDFF");
            }
            return combined.Contains("pink", StringComparison.Ordinal)
                || combined.Contains("magenta", StringComparison.Ordinal)
                    ? CreateOverride("#FF44CC")
                : combined.Contains("white", StringComparison.Ordinal) ? CreateOverride("#FFFFFF")
                : CreateOverride("#FFFFFF");
        }
    }
}
