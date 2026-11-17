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
        public ClientType ActiveClient { get; set; } = ClientType.Flash;
        public string? CustomCachePath { get; set; }
        public string? CustomHtml5CachePath { get; set; }
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

        public string PreferredTextureMirror { get; set; } = "Cloudflare";
        public bool EnableProTankiPack { get; set; } = false;
        private string _proTankiPackHotkey = "F8";
        public string ProTankiPackHotkey
        {
            get => _proTankiPackHotkey;
            set => _proTankiPackHotkey = Constants.NormalizeShaderHotkey(value);
        }
        public bool EnableSkinAnimations { get; set; } = false;
        private string _skinAnimMenuHotkey = "F9";
        public string SkinAnimMenuHotkey
        {
            get => _skinAnimMenuHotkey;
            set => _skinAnimMenuHotkey = Constants.NormalizeShaderHotkey(value);
        }
        public bool DisableThematicGold { get; set; } = false;
        public bool EnablePackLogListener { get; set; } = false;
        public string PackIpcToken { get; set; } = string.Empty;
        public bool HasSelectedClient { get; set; } = false;
    }
}
