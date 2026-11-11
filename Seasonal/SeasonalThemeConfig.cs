using System.Windows.Media;

namespace TextureSwapper.Seasonal
{
    public sealed class SeasonalThemeDefinition
    {
        public SeasonType Season { get; set; }
        public string ThemeKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AppTitle { get; set; } = "ProTanki Texture Swapper";
        public string AppLogoSource { get; set; } = "pack://application:,,,/Assets/logo.png";
        public Color AccentColor { get; set; } = Color.FromRgb(0x3B, 0x82, 0xF6);
        public string? FontFileName { get; set; }
        public string? FontFamilyName { get; set; }
        public string ThematicGoldBoxName { get; set; } = "Thematic gold";
        public ParticleMode ParticleMode { get; set; } = ParticleMode.None;
        public AudioPreset AudioPreset { get; set; } = AudioPreset.None;
        public string EventId { get; set; } = string.Empty;
        public string SettingsHeader { get; set; } = "Seasonal Customization";
        public string SettingsDescription { get; set; } =
            "Seasonal audio, visual atmosphere, and customization";
    }

    public static class SeasonalThemeConfig
    {
        public static SeasonType ActiveSeason { get; set; } = SeasonType.None;

        private static readonly Dictionary<SeasonType, SeasonalThemeDefinition> Definitions = new()
        {
            [SeasonType.None] = new SeasonalThemeDefinition
            {
                Season = SeasonType.None,
                ThemeKey = "Standard",
                DisplayName = "Standard",
                AppTitle = "ProTanki Texture Swapper",
                AppLogoSource = "pack://application:,,,/Assets/logo.png",
                AccentColor = Color.FromRgb(0x3B, 0x82, 0xF6),
                FontFileName = null,
                FontFamilyName = null,
                ThematicGoldBoxName = "Thematic gold",
                ParticleMode = ParticleMode.None,
                AudioPreset = AudioPreset.None,
                EventId = "",
                SettingsHeader = "Seasonal Customization",
                SettingsDescription = "No seasonal event active.",
            },

            [SeasonType.Christmas] = new SeasonalThemeDefinition
            {
                Season = SeasonType.Christmas,
                ThemeKey = "Christmas",
                DisplayName = "Christmas",
                AppTitle = "🎄 ProTanki Texture Swapper 🎄",
                AppLogoSource = "pack://application:,,,/Assets/logo.png",
                AccentColor = Color.FromRgb(0xD4, 0x24, 0x26),
                FontFileName = null,
                FontFamilyName = null,
                ThematicGoldBoxName = "Christmas Gold Box",
                ParticleMode = ParticleMode.Snowfall,
                AudioPreset = AudioPreset.Christmas,
                EventId = "CHRISTMAS_2026",
                SettingsHeader = "🎄 Christmas Seasonal Customization",
                SettingsDescription =
                    "Festive winter atmosphere, falling snowflakes, and holiday sounds",
            },

            [SeasonType.Halloween] = new SeasonalThemeDefinition
            {
                Season = SeasonType.Halloween,
                ThemeKey = "Halloween",
                DisplayName = "Halloween",
                AppTitle = "🎃 ProTanki Texture Swapper 🎃",
                AppLogoSource = "pack://application:,,,/Assets/halloween_logo.png",
                AccentColor = Color.FromRgb(0xFF, 0x75, 0x18),
                FontFileName = "Creepster-Regular.ttf",
                FontFamilyName = "Creepster",
                ThematicGoldBoxName = "Pumpkin Gold Box",
                ParticleMode = ParticleMode.HalloweenSpooky,
                AudioPreset = AudioPreset.Halloween,
                EventId = "HALLOWEEN_2026",
                SettingsHeader = "🎃 Halloween Seasonal Customization",
                SettingsDescription = "Eerie ambient sounds, floating embers, and spooky effects",
            },
        };

        public static SeasonalThemeDefinition Current =>
            Definitions.TryGetValue(ActiveSeason, out SeasonalThemeDefinition? def)
                ? def
                : Definitions[SeasonType.None];

        public static SeasonalThemeDefinition Get(SeasonType season)
        {
            return Definitions.TryGetValue(season, out SeasonalThemeDefinition? def)
                ? def
                : Definitions[SeasonType.None];
        }
    }
}
