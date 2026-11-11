using Wpf.Ui.Appearance;

namespace TextureSwapper.Core.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = string.Empty;
        public ApplicationTheme Theme { get; set; } = ApplicationTheme.Dark;
        public int MaxBackupRetentionDays { get; set; } = 30;
        public string LastCheckedTime { get; set; } = "Never";
        public string LastUpdateStatus { get; set; } = "Idle";
        public string? CustomCachePath { get; set; }
        public string? LastSelectedCategory { get; set; }
        public string? LastSelectedItemName { get; set; }
        public double UIScale { get; set; } = 1.0;
        public bool IsPremiumUnlocked { get; set; }
        public string PremiumLicenseKey { get; set; } = string.Empty;
        public string PremiumActivationSignature { get; set; } = string.Empty;
        public string UserNickname { get; set; } = string.Empty;
        public string PremiumExpiresAtUtc { get; set; } = string.Empty;
        public string LastVerifiedUtc { get; set; } = string.Empty;
        public string SavedAdminSecretEncrypted { get; set; } = string.Empty;
        public bool RememberAdminPassword { get; set; } = true;
        public bool SkipRemoteTargetSync { get; set; } = true;
        public bool IsTrialLicense { get; set; } = false;
        public bool HasClaimedTrial { get; set; } = false;
        public bool ForceSeasonalPreview { get; set; } = false;
        public bool EnableSeasonalSound { get; set; } = true;
        public bool IsSeasonalThemeSelected { get; set; } = false;
        public bool EnableSeasonalRandomAudio { get; set; } = true;
        public bool EnableSeasonalAtmosphereAnimation { get; set; } = true;
        public bool EnableSeasonalCustomFont { get; set; } = true;
        public bool EnableSeasonalAppLogo { get; set; } = true;

        public bool ForceHalloweenPreview
        {
            get => ForceSeasonalPreview;
            set => ForceSeasonalPreview = value;
        }
        public bool EnableHalloweenSound
        {
            get => EnableSeasonalSound;
            set => EnableSeasonalSound = value;
        }
        public bool IsHalloweenThemeSelected
        {
            get => IsSeasonalThemeSelected;
            set => IsSeasonalThemeSelected = value;
        }
        public bool EnableHalloweenRandomScares
        {
            get => EnableSeasonalRandomAudio;
            set => EnableSeasonalRandomAudio = value;
        }
        public bool EnableHalloweenAtmosphereAnimation
        {
            get => EnableSeasonalAtmosphereAnimation;
            set => EnableSeasonalAtmosphereAnimation = value;
        }
        public bool EnableHalloweenSpookyFont
        {
            get => EnableSeasonalCustomFont;
            set => EnableSeasonalCustomFont = value;
        }
        public bool EnableHalloweenAppLogo
        {
            get => EnableSeasonalAppLogo;
            set => EnableSeasonalAppLogo = value;
        }
        public string LastSeenVersion { get; set; } = string.Empty;
        public bool HasSeenV2UpdateNotice { get; set; } = false;
        public bool HasSeenDiscordInvite { get; set; } = false;
        public bool EnableCenteredHud { get; set; } = false;
        public bool DisableThematicGold { get; set; } = false;
    }
}
