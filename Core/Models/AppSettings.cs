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
        public bool ForceHalloweenPreview { get; set; } = false;
        public bool EnableHalloweenSound { get; set; } = true;
        public bool IsHalloweenThemeSelected { get; set; } = true;
        public bool EnableHalloweenRandomScares { get; set; } = true;
        public bool EnableHalloweenAtmosphereAnimation { get; set; } = true;
        public bool EnableHalloweenSpookyFont { get; set; } = true;
        public bool EnableHalloweenAppLogo { get; set; } = true;
        public string LastSeenVersion { get; set; } = string.Empty;
        public bool HasSeenV2UpdateNotice { get; set; } = false;
        public bool HasSeenDiscordInvite { get; set; } = false;
    }
}
