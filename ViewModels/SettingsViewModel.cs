using Serilog;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Seasonal;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels;

public class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly ISettingsService _settingsService;

    private readonly MainViewModel _mainViewModel;

    private readonly ILocalizationService _localizationService;

    private readonly IPremiumService _premiumService;

    private readonly IDeviceBanService _deviceBanService;

    private readonly Services.PackLogListener? _packLogListener;

    private readonly SemaphoreSlim _scriptPatchLock = new(1, 1);

    private string _activationStatusText = string.Empty;

    private bool _isLoading;

    private string _logContent = string.Empty;

    public AppSettings Settings { get; }

    public int BackupRetentionDays
    {
        get => Settings.MaxBackupRetentionDays;
        set
        {
            if (Settings.MaxBackupRetentionDays != value)
            {
                int maxBackupRetentionDays = Settings.MaxBackupRetentionDays;
                Settings.MaxBackupRetentionDays = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(BackupRetentionDays));
                Log.Information(
                    "Settings changed: Max Backup Retention updated from {OldDays} to {NewDays} days.",
                    maxBackupRetentionDays,
                    value
                );
                ExecuteRefreshLogs(null);
            }
        }
    }

    public bool IsPremiumUnlocked => _premiumService.IsPremiumUnlocked;

    public bool IsPremiumLocked => !_premiumService.IsPremiumUnlocked;

    public string UserNickname
    {
        get => Settings.UserNickname;
        set
        {
            if (Settings.UserNickname != value)
            {
                Settings.UserNickname = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(UserNickname));
            }
        }
    }

    public string PremiumLicenseKey
    {
        get => Settings.PremiumLicenseKey;
        set
        {
            if (Settings.PremiumLicenseKey != value)
            {
                Settings.PremiumLicenseKey = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(PremiumLicenseKey));
            }
        }
    }

    public bool IsTrialActive => _premiumService.IsTrialActive;

    public bool CanStartTrial => !Settings.HasClaimedTrial && !IsPremiumUnlocked;

    public bool HasClaimedTrial => Settings.HasClaimedTrial;

    public string TrialRemainingDisplay
    {
        get
        {
            if (!_premiumService.IsTrialActive)
            {
                return string.Empty;
            }
            TimeSpan? trialTimeRemaining = _premiumService.TrialTimeRemaining;
            string result =
                !trialTimeRemaining.HasValue || trialTimeRemaining.Value <= TimeSpan.Zero
                    ? "Expired"
                : (trialTimeRemaining.Value.TotalDays >= 1.0)
                    ? $"{(int)trialTimeRemaining.Value.TotalDays}d {trialTimeRemaining.Value.Hours}h remaining"
                : $"{(int)trialTimeRemaining.Value.TotalHours}h {trialTimeRemaining.Value.Minutes}m remaining";
            return result;
        }
    }

    public string PremiumExpirationDisplay =>
        !IsPremiumUnlocked ? string.Empty
        : IsTrialActive ? "Free Trial (" + TrialRemainingDisplay + ")"
        : string.IsNullOrWhiteSpace(Settings.PremiumExpiresAtUtc)
        || IsLifetimeSeasonalKey(Settings.PremiumLicenseKey)
            ? string.Empty
        : DateTime.TryParse(Settings.PremiumExpiresAtUtc, out DateTime result2)
            ? _localizationService.GetString(
                "Loc_Settings_ExpiresFormat",
                result2.ToLocalTime().ToString("g")
            )
        : _localizationService.GetString(
            "Loc_Settings_ExpiresFormat",
            Settings.PremiumExpiresAtUtc
        );

    private static bool IsLifetimeSeasonalKey(string? key)
    {
        return !string.IsNullOrEmpty(key)
            && (
                key.StartsWith("HALLOWEEN-", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("XMAS-", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("CHRISTMAS-", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("SEASONAL-", StringComparison.OrdinalIgnoreCase)
            );
    }

    public bool HasExpirationDisplay => !string.IsNullOrWhiteSpace(PremiumExpirationDisplay);

    public string ActivationStatusText
    {
        get => _activationStatusText;
        set => SetProperty(ref _activationStatusText, value, nameof(ActivationStatusText));
    }

    public string LicenseTierDisplay =>
        !IsPremiumUnlocked ? "Free Tier"
        : IsTrialActive ? "3-Day Free Trial"
        : IsLifetimeSeasonalKey(Settings.PremiumLicenseKey) ? "Lifetime Supporter"
        : !string.IsNullOrWhiteSpace(Settings.PremiumExpiresAtUtc) ? "Time-Limited Premium"
        : "Lifetime Supporter";

    private bool _isKeyRevealed;
    public bool IsKeyRevealed
    {
        get => _isKeyRevealed;
        set
        {
            if (SetProperty(ref _isKeyRevealed, value, nameof(IsKeyRevealed)))
            {
                OnPropertyChanged(nameof(DisplayLicenseKey));
            }
        }
    }

    public string DisplayLicenseKey
    {
        get
        {
            string key = Settings.PremiumLicenseKey;
            return string.IsNullOrWhiteSpace(key) ? "—"
                : IsKeyRevealed ? key
                : key.Length <= 8 ? "••••••••"
                : key[..4] + "-••••-••••-" + key[^4..];
        }
    }

    public string DeviceHardwareId => _deviceBanService.GetDeviceHardwareId();

    public string ShortDeviceHardwareId
    {
        get
        {
            string hwid = DeviceHardwareId;
            return string.IsNullOrEmpty(hwid) || hwid.Length <= 16
                ? hwid
                : $"{hwid[..8]}...{hwid[^8..]}";
        }
    }

    public string LastVerifiedDisplay =>
        string.IsNullOrWhiteSpace(Settings.LastVerifiedUtc) ? "Not verified online"
        : DateTime.TryParse(Settings.LastVerifiedUtc, out DateTime dt)
            ? dt.ToLocalTime().ToString("g")
        : Settings.LastVerifiedUtc;

    public bool HasCryptographicSignature =>
        !string.IsNullOrEmpty(Settings.PremiumActivationSignature);

    public ICommand BuyPremiumCommand { get; }

    public ICommand UseWindowsUsernameCommand { get; }

    public ICommand ActivatePremiumCommand { get; }

    public ICommand StartFreeTrialCommand { get; }

    public ICommand ToggleKeyVisibilityCommand { get; }

    public ICommand CopyLicenseKeyCommand { get; }

    public ICommand SyncLicenseCommand { get; }

    public IReadOnlyList<LanguageInfo> AvailableLanguages =>
        _localizationService.SupportedLanguages;

    public LanguageInfo? SelectedLanguage
    {
        get =>
            AvailableLanguages.FirstOrDefault(l =>
                l.Code.Equals(Settings.Language, StringComparison.OrdinalIgnoreCase)
            )
            ?? AvailableLanguages.FirstOrDefault(l =>
                l.Code.Equals("en", StringComparison.OrdinalIgnoreCase)
            )
            ?? (AvailableLanguages.Count > 0 ? AvailableLanguages[0] : null);
        set
        {
            if (value != null && Settings.Language != value.Code)
            {
                Settings.Language = value.Code;
                _settingsService.Save(Settings);
                _localizationService.SetLanguage(value.Code);
                OnPropertyChanged(nameof(SelectedLanguage));
                Log.Information("Language changed to {Language}.", value.Code);
            }
        }
    }

    public string SelectedTheme
    {
        get =>
            _mainViewModel.SeasonalThemeService.IsSeasonalActive
                ? SeasonalThemeConfig.Current.DisplayName
            : Settings.Theme == ApplicationTheme.Light ? "Light"
            : "Dark";
        set
        {
            if (
                string.IsNullOrEmpty(value)
                || string.Equals(value, SelectedTheme, StringComparison.OrdinalIgnoreCase)
            )
            {
                return;
            }

            string currentSeasonName = SeasonalThemeConfig.Current.DisplayName;
            if (
                string.Equals(value, currentSeasonName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Seasonal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Halloween", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Christmas", StringComparison.OrdinalIgnoreCase)
            )
            {
                Settings.IsSeasonalThemeSelected = true;
                _settingsService.Save(Settings);
                _mainViewModel.SeasonalThemeService.CheckAndApply(notify: true, force: true);
                Log.Information("Theme changed to {Season}.", currentSeasonName);
                _ = _mainViewModel.NotificationService.ShowAsync(
                    $"{SeasonalThemeConfig.Current.AppTitle} Active",
                    "Live seasonal effects are active. Higher CPU, RAM, and GPU usage is normal for animations.",
                    Wpf.Ui.Controls.ControlAppearance.Info
                );
            }
            else
            {
                Settings.IsSeasonalThemeSelected = false;
                Settings.ForceSeasonalPreview = false;
                ApplicationTheme theme = string.Equals(
                    value,
                    "Light",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? ApplicationTheme.Light
                    : ApplicationTheme.Dark;
                Settings.Theme = theme;
                _settingsService.Save(Settings);
                _mainViewModel.SeasonalThemeService.CheckAndApply(notify: true, force: true);
                Log.Information("Theme changed to {Theme}.", theme);
            }
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(CanConfigureSeasonalSound));
            OnPropertyChanged(nameof(CanConfigureHalloweenSound));
            OnPropertyChanged(nameof(IsSeasonalCustomizationVisible));
            OnPropertyChanged(nameof(IsHalloweenCustomizationVisible));
            ExecuteRefreshLogs(null);
        }
    }

    public IEnumerable<string> AvailableThemes
    {
        get
        {
            List<string> list = ["Dark", "Light"];
            if (_mainViewModel.SeasonalThemeService.IsWithinSeasonalWindow)
            {
                list.Add(SeasonalThemeConfig.Current.DisplayName);
            }
            return list;
        }
    }

    public string SeasonalSettingsHeader => SeasonalThemeConfig.Current.SettingsHeader;
    public string SeasonalSettingsDescription => SeasonalThemeConfig.Current.SettingsDescription;

    public bool IsWithinSeasonalPeriod =>
        _mainViewModel.SeasonalThemeService.IsWithinSeasonalWindow;
    public bool IsWithinHalloweenPeriod => IsWithinSeasonalPeriod;

    public bool IsSeasonalCustomizationVisible =>
        _mainViewModel.SeasonalThemeService.IsSeasonalActive;
    public bool IsHalloweenCustomizationVisible => IsSeasonalCustomizationVisible;

    public bool CanConfigureSeasonalSound => IsSeasonalCustomizationVisible;
    public bool CanConfigureHalloweenSound => CanConfigureSeasonalSound;

    public bool EnableSeasonalAtmosphereAnimation
    {
        get => Settings.EnableSeasonalAtmosphereAnimation;
        set
        {
            if (Settings.EnableSeasonalAtmosphereAnimation != value)
            {
                Settings.EnableSeasonalAtmosphereAnimation = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableSeasonalAtmosphereAnimation));
                OnPropertyChanged(nameof(EnableHalloweenAtmosphereAnimation));
                _mainViewModel.NotifySeasonalVisualsChanged();
            }
        }
    }
    public bool EnableHalloweenAtmosphereAnimation
    {
        get => EnableSeasonalAtmosphereAnimation;
        set => EnableSeasonalAtmosphereAnimation = value;
    }

    public bool EnableSeasonalCustomFont
    {
        get => Settings.EnableSeasonalCustomFont;
        set
        {
            if (Settings.EnableSeasonalCustomFont != value)
            {
                Settings.EnableSeasonalCustomFont = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableSeasonalCustomFont));
                OnPropertyChanged(nameof(EnableHalloweenSpookyFont));
                _mainViewModel.SeasonalThemeService.CheckAndApply(notify: true, force: true);
            }
        }
    }
    public bool EnableHalloweenSpookyFont
    {
        get => EnableSeasonalCustomFont;
        set => EnableSeasonalCustomFont = value;
    }

    public bool EnableSeasonalAppLogo
    {
        get => Settings.EnableSeasonalAppLogo;
        set
        {
            if (Settings.EnableSeasonalAppLogo != value)
            {
                Settings.EnableSeasonalAppLogo = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableSeasonalAppLogo));
                OnPropertyChanged(nameof(EnableHalloweenAppLogo));
                _mainViewModel.NotifySeasonalVisualsChanged();
            }
        }
    }
    public bool EnableHalloweenAppLogo
    {
        get => EnableSeasonalAppLogo;
        set => EnableSeasonalAppLogo = value;
    }

    public bool EnableSeasonalSound
    {
        get => Settings.EnableSeasonalSound;
        set
        {
            if (Settings.EnableSeasonalSound != value)
            {
                Settings.EnableSeasonalSound = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableSeasonalSound));
                OnPropertyChanged(nameof(EnableHalloweenSound));
                if (value && _mainViewModel.IsSeasonalThemeActive)
                {
                    _mainViewModel.SeasonalAudioService.PlayClick();
                    _mainViewModel.SeasonalAudioService.SetAmbientDrone(true);
                }
                else
                {
                    _mainViewModel.SeasonalAudioService.SetAmbientDrone(false);
                }
            }
        }
    }
    public bool EnableHalloweenSound
    {
        get => EnableSeasonalSound;
        set => EnableSeasonalSound = value;
    }

    public bool EnableSeasonalRandomAudio
    {
        get => Settings.EnableSeasonalRandomAudio;
        set
        {
            if (Settings.EnableSeasonalRandomAudio != value)
            {
                Settings.EnableSeasonalRandomAudio = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableSeasonalRandomAudio));
                OnPropertyChanged(nameof(EnableHalloweenRandomScares));
            }
        }
    }
    public bool EnableHalloweenRandomScares
    {
        get => EnableSeasonalRandomAudio;
        set => EnableSeasonalRandomAudio = value;
    }

    public double UIScale
    {
        get => Settings.UIScale;
        set
        {
            if (Settings.UIScale != value)
            {
                _mainViewModel.UIScale = value;
                OnPropertyChanged(nameof(UIScale));
                Log.Information("UI Scale changed to {Scale}.", value);
                ExecuteRefreshLogs(null);
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value, nameof(IsLoading));
    }

    public string AppVersion =>
        $"v{MainViewModel.FormatVersion(Assembly.GetExecutingAssembly().GetName().Version)}";

    private string _latestGitHubVersion = "Checking...";
    public string LatestGitHubVersion
    {
        get => _latestGitHubVersion;
        set => SetProperty(ref _latestGitHubVersion, value, nameof(LatestGitHubVersion));
    }

    private string _latestVersionPill = "Up to date";
    public string LatestVersionPill
    {
        get => _latestVersionPill;
        set => SetProperty(ref _latestVersionPill, value, nameof(LatestVersionPill));
    }

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value, nameof(IsUpdateAvailable));
    }

    private string _engineStateDisplay = "Ready";
    public string EngineStateDisplay
    {
        get => _engineStateDisplay;
        set => SetProperty(ref _engineStateDisplay, value, nameof(EngineStateDisplay));
    }

    public string UpdateStatus
    {
        get
        {
            string lastUpdateStatus = Settings.LastUpdateStatus;
            string result =
                string.IsNullOrEmpty(lastUpdateStatus)
                || lastUpdateStatus.Equals("Idle", StringComparison.OrdinalIgnoreCase)
                    ? _localizationService.GetString("Loc_Msg_Idle")
                : lastUpdateStatus.Equals("You are up to date!", StringComparison.OrdinalIgnoreCase)
                    ? _localizationService.GetString("Loc_Msg_UpToDate")
                : lastUpdateStatus.Equals(
                    "Connecting to GitHub...",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? _localizationService.GetString("Loc_Msg_ConnectingGitHub")
                : lastUpdateStatus.Equals(
                    "Checking for updates...",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? _localizationService.GetString("Loc_Msg_CheckingUpdates")
                : lastUpdateStatus.Equals(
                    "Error: Check connection.",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? _localizationService.GetString("Loc_Msg_UpdateErrorShort")
                : lastUpdateStatus;
            return result;
        }
        set
        {
            Settings.LastUpdateStatus = value;
            OnPropertyChanged(nameof(UpdateStatus));
        }
    }

    public string LastCheckedTime
    {
        get
        {
            string lastCheckedTime = Settings.LastCheckedTime;
            return (
                string.IsNullOrEmpty(lastCheckedTime)
                || lastCheckedTime.Equals("Never", StringComparison.OrdinalIgnoreCase)
            )
                ? _localizationService.GetString("Loc_Msg_Never")
                : lastCheckedTime;
        }
        set
        {
            Settings.LastCheckedTime = value;
            OnPropertyChanged(nameof(LastCheckedTime));
        }
    }

    public IReadOnlyList<string> AvailableDownloadMirrors { get; } =
    [
        "Cloudflare (Fast)",
        "GitHub Releases"
    ];

    public string SelectedDownloadMirror
    {
        get =>
            string.Equals(
                Settings.PreferredTextureMirror,
                "GitHub",
                StringComparison.OrdinalIgnoreCase
            )
                ? "GitHub Releases"
                : "Cloudflare (Fast)";
        set
        {
            string clean = value == "GitHub Releases" ? "GitHub" : "Cloudflare";
            if (
                !string.Equals(
                    Settings.PreferredTextureMirror,
                    clean,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                Settings.PreferredTextureMirror = clean;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(SelectedDownloadMirror));
                Log.Information(
                    "Settings changed: Preferred Texture Download Mirror set to {Mirror}",
                    clean
                );
            }
        }
    }

    public ICommand CheckForUpdatesCommand { get; }

    public ICommand CopyHardwareIdCommand { get; }

    public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value, nameof(LogContent));
    }

    public ICommand RefreshLogsCommand { get; }

    public ICommand OpenLogsFolderCommand { get; }

    public IReadOnlyList<string> AvailableClientModes { get; } =
    [
        "HTML5 Client (New)",
        "Flash Client (Legacy)"
    ];

    public bool IsHtml5Client => _mainViewModel.IsHtml5Client;

    public bool IsFlashClient => _mainViewModel.IsFlashClient;

    public string SelectedClientMode
    {
        get => IsHtml5Client ? "HTML5 Client (New)" : "Flash Client (Legacy)";
        set
        {
            ClientType target =
                value != null && value.StartsWith("HTML5", StringComparison.OrdinalIgnoreCase)
                    ? ClientType.Html5
                    : ClientType.Flash;
            if (Settings.ActiveClient != target)
            {
                _mainViewModel.SwitchClient(target);
                if (!IsHtml5Client)
                {
                    Settings.EnableProTankiPack = false;
                    Settings.EnableSkinAnimations = false;
                }
                else
                {
                    _ = RefreshHtml5ScriptTogglesAsync();
                }
                OnPropertyChanged(nameof(EnableProTankiPack));
                OnPropertyChanged(nameof(EnableSkinAnimations));
                OnPropertyChanged(nameof(SelectedClientMode));
                OnPropertyChanged(nameof(IsHtml5Client));
                OnPropertyChanged(nameof(IsFlashClient));
                OnPropertyChanged(nameof(ActiveCachePath));
                ExecuteRefreshLogs(null);
            }
        }
    }

    public string ActiveCachePath => _mainViewModel.CachePath;

    public ICommand BrowseCachePathCommand { get; }

    public ICommand ResetCachePathCommand { get; }

    public IReadOnlyList<string> AvailableShaderHotkeys
    {
        get
        {
            string current = SelectedShaderHotkey;
            return string.IsNullOrWhiteSpace(current)
                || Core.Constants.AllowedShaderHotkeys.Contains(current, StringComparer.OrdinalIgnoreCase)
                ? Core.Constants.AllowedShaderHotkeys
                : (IReadOnlyList<string>)[.. Core.Constants.AllowedShaderHotkeys, current];
        }
    }

    public bool EnablePackLogListener
    {
        get => Settings.EnablePackLogListener;
        set
        {
            if (Settings.EnablePackLogListener != value)
            {
                Settings.EnablePackLogListener = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnablePackLogListener));

                Services.PackLogListener? listener =
                    App.ServiceProvider?.GetService(typeof(Services.PackLogListener)) as Services.PackLogListener;
                if (value || Settings.EnableProTankiPack)
                {
                    listener?.Start();
                }
                else
                {
                    listener?.Stop();
                }
            }
        }
    }

    public bool EnableProTankiPack
    {
        get => Settings.EnableProTankiPack;
        set
        {
            if (Settings.EnableProTankiPack != value)
            {
                Settings.EnableProTankiPack = value;
                OnPropertyChanged(nameof(EnableProTankiPack));
                _ = HandleShadersToggleAsync(value, isHotkeyUpdate: false);
            }
        }
    }

    public string SelectedShaderHotkey
    {
        get => Core.Constants.NormalizeShaderHotkey(Settings.ProTankiPackHotkey);
        set
        {
            string normalized = Core.Constants.NormalizeShaderHotkey(value);
            if (
                !string.Equals(
                    Settings.ProTankiPackHotkey,
                    normalized,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                string previous = Settings.ProTankiPackHotkey;
                Settings.ProTankiPackHotkey = normalized;
                OnPropertyChanged(nameof(SelectedShaderHotkey));
                if (Settings.EnableProTankiPack && IsHtml5Client)
                {
                    _ = HandleShadersToggleAsync(true, isHotkeyUpdate: true, previousHotkey: previous);
                }
                else
                {
                    _settingsService.Save(Settings);
                }
            }
        }
    }

    public bool EnableSkinAnimations
    {
        get => Settings.EnableSkinAnimations;
        set
        {
            if (Settings.EnableSkinAnimations != value)
            {
                Settings.EnableSkinAnimations = value;
                OnPropertyChanged(nameof(EnableSkinAnimations));
                _ = HandleSkinAnimationsToggleAsync(value, isHotkeyUpdate: false);
            }
        }
    }

    public string SelectedSkinAnimHotkey
    {
        get => Core.Constants.NormalizeShaderHotkey(Settings.SkinAnimMenuHotkey);
        set
        {
            string normalized = Core.Constants.NormalizeShaderHotkey(value);
            if (
                !string.Equals(
                    Settings.SkinAnimMenuHotkey,
                    normalized,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                string previous = Settings.SkinAnimMenuHotkey;
                Settings.SkinAnimMenuHotkey = normalized;
                OnPropertyChanged(nameof(SelectedSkinAnimHotkey));
                if (Settings.EnableSkinAnimations && IsHtml5Client)
                {
                    _ = HandleSkinAnimationsToggleAsync(true, isHotkeyUpdate: true, previousHotkey: previous);
                }
                else
                {
                    _settingsService.Save(Settings);
                }
            }
        }
    }

    public SettingsViewModel(
        ISettingsService settingsService,
        MainViewModel mainViewModel,
        ILocalizationService localizationService,
        IPremiumService premiumService,
        IDeviceBanService deviceBanService
    )
    {
        _settingsService = settingsService;
        _mainViewModel = mainViewModel;
        _localizationService = localizationService;
        _premiumService = premiumService;
        _deviceBanService = deviceBanService;
        _packLogListener =
            App.ServiceProvider?.GetService(typeof(Services.PackLogListener)) as Services.PackLogListener;
        if (_packLogListener != null)
        {
            _packLogListener.MenuHotkeyChanged += OnPackMenuHotkeyChanged;
        }
        Settings = mainViewModel.Settings;

        if (!IsHtml5Client)
        {
            Settings.EnableProTankiPack = false;
            Settings.EnableSkinAnimations = false;
        }
        else
        {
            _ = RefreshHtml5ScriptTogglesAsync();
        }
        BrowseCachePathCommand = new RelayCommand(_ =>
        {
            _mainViewModel.BrowseCommand.Execute(null);
            OnPropertyChanged(nameof(ActiveCachePath));
            ExecuteRefreshLogs(null);
        });
        ResetCachePathCommand = new RelayCommand(_ =>
        {
            if (IsHtml5Client)
            {
                Settings.CustomHtml5CachePath = null;
            }
            else
            {
                Settings.CustomCachePath = null;
            }
            _settingsService.Save(Settings);
            _mainViewModel.Html5CacheService.InvalidateCache();
            _mainViewModel.CachePath = _mainViewModel.ResolveActiveCachePath();
            OnPropertyChanged(nameof(ActiveCachePath));
            Log.Information(
                "Reset {Client} cache path to auto-detected default: {Path}",
                Settings.ActiveClient,
                _mainViewModel.CachePath
            );
            ExecuteRefreshLogs(null);
        });
        _mainViewModel.ActiveClientChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedClientMode));
            OnPropertyChanged(nameof(IsHtml5Client));
            OnPropertyChanged(nameof(IsFlashClient));
            OnPropertyChanged(nameof(ActiveCachePath));
        };
        BuyPremiumCommand = new RelayCommand(_ =>
        {
            ExecuteBuyPremium();
        });
        UseWindowsUsernameCommand = new RelayCommand(_ =>
        {
            UserNickname = Environment.UserName;
        });
        ActivatePremiumCommand = new AsyncRelayCommand(ExecuteActivatePremium, _ => !IsLoading);
        StartFreeTrialCommand = new AsyncRelayCommand(
            ExecuteStartFreeTrial,
            _ => !IsLoading && CanStartTrial
        );
        CheckForUpdatesCommand = new AsyncRelayCommand(ExecuteCheckForUpdates, _ => !IsLoading);
        RefreshLogsCommand = new RelayCommand(ExecuteRefreshLogs);
        OpenLogsFolderCommand = new RelayCommand(ExecuteOpenLogsFolder);
        ToggleKeyVisibilityCommand = new RelayCommand(_ => IsKeyRevealed = !IsKeyRevealed);
        CopyLicenseKeyCommand = new RelayCommand(_ =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Settings.PremiumLicenseKey))
                {
                    Clipboard.SetText(Settings.PremiumLicenseKey);
                    _ = _mainViewModel.ShowNotificationAsync(
                        "License Key Copied",
                        "Your license key has been copied to clipboard.",
                        ControlAppearance.Success
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to copy license key.");
            }
        });
        SyncLicenseCommand = new AsyncRelayCommand(ExecuteSyncLicense, _ => !IsLoading);

        CopyHardwareIdCommand = new RelayCommand(_ =>
        {
            try
            {
                string deviceHardwareId = _deviceBanService.GetDeviceHardwareId();
                Clipboard.SetText(deviceHardwareId);
                _ = _mainViewModel.ShowNotificationAsync(
                    "Hardware ID Copied",
                    "Your device hardware ID has been copied to clipboard.",
                    ControlAppearance.Success
                );
            }
            catch (Exception exception)
            {
                Log.Error(exception, "Failed to copy Hardware ID.");
            }
        });
        _localizationService.LanguageChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(UpdateStatus));
            OnPropertyChanged(nameof(LastCheckedTime));
            OnPropertyChanged(nameof(PremiumExpirationDisplay));
        };
        _premiumService.PremiumStatusChanged += (_, isUnlocked) =>
        {
            Application current = Application.Current;
            current?.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsPremiumUnlocked));
                OnPropertyChanged(nameof(IsPremiumLocked));
                OnPropertyChanged(nameof(IsTrialActive));
                OnPropertyChanged(nameof(CanStartTrial));
                OnPropertyChanged(nameof(HasClaimedTrial));
                OnPropertyChanged(nameof(TrialRemainingDisplay));
                OnPropertyChanged(nameof(PremiumExpirationDisplay));
                OnPropertyChanged(nameof(PremiumLicenseKey));
                OnPropertyChanged(nameof(UserNickname));
                OnPropertyChanged(nameof(LicenseTierDisplay));
                OnPropertyChanged(nameof(DisplayLicenseKey));
                OnPropertyChanged(nameof(LastVerifiedDisplay));
                OnPropertyChanged(nameof(HasCryptographicSignature));
                OnPropertyChanged(nameof(ShortDeviceHardwareId));
            });
        };

        _mainViewModel.SeasonalThemeService.SeasonalThemeChanged += (sender, active) =>
        {
            OnPropertyChanged(nameof(AvailableThemes));
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(SeasonalSettingsHeader));
            OnPropertyChanged(nameof(SeasonalSettingsDescription));
            OnPropertyChanged(nameof(CanConfigureSeasonalSound));
            OnPropertyChanged(nameof(CanConfigureHalloweenSound));
            OnPropertyChanged(nameof(IsSeasonalCustomizationVisible));
            OnPropertyChanged(nameof(IsHalloweenCustomizationVisible));
            OnPropertyChanged(nameof(IsWithinSeasonalPeriod));
            OnPropertyChanged(nameof(IsWithinHalloweenPeriod));
        };

        ExecuteRefreshLogs(null);
    }

    private void ExecuteBuyPremium()
    {
        _ = TextureSwapper.Helpers.Utilities.PremiumPurchaseHelper.PromptPurchaseOptionsAsync();
    }

    private async Task ExecuteActivatePremium(object? parameter)
    {
        if (string.IsNullOrWhiteSpace(PremiumLicenseKey))
        {
            ActivationStatusText = "Please enter a valid license key.";
            return;
        }
        try
        {
            IsLoading = true;
            ActivationStatusText = "Activating key...";
            Log.Information("Attempting to activate premium key for user: {User}", UserNickname);
            PremiumActivationResult result = await _premiumService.ActivatePremiumKeyAsync(
                PremiumLicenseKey.Trim(),
                UserNickname
            );
            if (result.Success)
            {
                ActivationStatusText = string.Empty;
                OnPropertyChanged(nameof(IsPremiumLocked));
                OnPropertyChanged(nameof(IsPremiumUnlocked));
                OnPropertyChanged(nameof(IsTrialActive));
                OnPropertyChanged(nameof(CanStartTrial));
                OnPropertyChanged(nameof(HasClaimedTrial));
                OnPropertyChanged(nameof(TrialRemainingDisplay));
                OnPropertyChanged(nameof(PremiumExpirationDisplay));
                OnPropertyChanged(nameof(LicenseTierDisplay));
                OnPropertyChanged(nameof(DisplayLicenseKey));
                OnPropertyChanged(nameof(PremiumLicenseKey));
                OnPropertyChanged(nameof(LastVerifiedDisplay));
                OnPropertyChanged(nameof(HasExpirationDisplay));
                OnPropertyChanged(nameof(HasCryptographicSignature));
                await _mainViewModel.ShowNotificationAsync(
                    "Premium Activated",
                    "Thank you for supporting ProTanki Texture Swapper!",
                    ControlAppearance.Success
                );
            }
            else
            {
                ActivationStatusText = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Activation failed."
                    : result.ErrorMessage;
                await _mainViewModel.ShowNotificationAsync(
                    "Activation Failed",
                    ActivationStatusText,
                    ControlAppearance.Danger
                );
            }
        }
        catch (Exception ex)
        {
            ActivationStatusText = "Activation error: " + ex.Message;
            Log.Error(ex, "Failed to activate premium license.");
            await _mainViewModel.ShowNotificationAsync(
                "Error",
                ActivationStatusText,
                ControlAppearance.Danger
            );
        }
        finally
        {
            IsLoading = false;
            _settingsService.Save(Settings);
            ExecuteRefreshLogs(null);
        }
    }

    private async Task ExecuteStartFreeTrial(object? parameter)
    {
        if (IsLoading || !CanStartTrial)
        {
            return;
        }
        try
        {
            IsLoading = true;
            ActivationStatusText = _localizationService.GetString("Loc_Settings_StartingTrial");
            string nickname = string.IsNullOrWhiteSpace(UserNickname)
                ? Environment.UserName
                : UserNickname.Trim();
            Log.Information("Starting free trial for user: {User}", nickname);
            PremiumActivationResult result = await _premiumService.StartFreeTrialAsync(nickname);
            if (result.Success)
            {
                ActivationStatusText = string.Empty;
                OnPropertyChanged(nameof(IsPremiumLocked));
                OnPropertyChanged(nameof(IsPremiumUnlocked));
                OnPropertyChanged(nameof(IsTrialActive));
                OnPropertyChanged(nameof(CanStartTrial));
                OnPropertyChanged(nameof(HasClaimedTrial));
                OnPropertyChanged(nameof(TrialRemainingDisplay));
                OnPropertyChanged(nameof(PremiumExpirationDisplay));
                OnPropertyChanged(nameof(LicenseTierDisplay));
                OnPropertyChanged(nameof(DisplayLicenseKey));
                OnPropertyChanged(nameof(PremiumLicenseKey));
                OnPropertyChanged(nameof(LastVerifiedDisplay));
                OnPropertyChanged(nameof(HasExpirationDisplay));
                OnPropertyChanged(nameof(HasCryptographicSignature));
                await _mainViewModel.ShowNotificationAsync(
                    _localizationService.GetString("Loc_Msg_TrialActivatedTitle"),
                    _localizationService.GetString("Loc_Msg_TrialActivated"),
                    ControlAppearance.Success
                );
            }
            else
            {
                ActivationStatusText = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Trial activation failed."
                    : result.ErrorMessage;
                await _mainViewModel.ShowNotificationAsync(
                    "Trial Activation Failed",
                    ActivationStatusText,
                    ControlAppearance.Danger
                );
            }
        }
        catch (Exception ex)
        {
            ActivationStatusText = "Trial activation error: " + ex.Message;
            Log.Error(ex, "Failed to start free trial.");
            await _mainViewModel.ShowNotificationAsync(
                "Error",
                ActivationStatusText,
                ControlAppearance.Danger
            );
        }
        finally
        {
            IsLoading = false;
            _settingsService.Save(Settings);
            OnPropertyChanged(nameof(CanStartTrial));
            OnPropertyChanged(nameof(HasClaimedTrial));
            ExecuteRefreshLogs(null);
        }
    }

    private void ExecuteRefreshLogs(object? parameter)
    {
        try
        {
            string currentLogFilePath = App.CurrentLogFilePath;
            if (!string.IsNullOrEmpty(currentLogFilePath) && File.Exists(currentLogFilePath))
            {
                using FileStream fileStream = new(
                    currentLogFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );
                if (fileStream.Length > 100000)
                {
                    _ = fileStream.Seek(-100000L, SeekOrigin.End);
                }
                using StreamReader streamReader = new(fileStream);
                LogContent = streamReader.ReadToEnd();
                return;
            }
            LogContent = "No log file found.";
        }
        catch (Exception ex)
        {
            LogContent = "Error reading log file: " + ex.Message;
        }
    }

    private void ExecuteOpenLogsFolder(object? parameter)
    {
        try
        {
            string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (Directory.Exists(text))
            {
                _ = Process.Start(new ProcessStartInfo { FileName = text, UseShellExecute = true });
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to open logs directory.");
        }
    }

    private async Task ExecuteCheckForUpdates(object? parameter)
    {
        try
        {
            IsLoading = true;
            EngineStateDisplay = "Checking...";
            UpdateStatus = "Connecting to GitHub...";
            LastCheckedTime = DateTime.Now.ToString("g");
            Log.Information("User initiated manual app update check. Querying API...");

            GitHubReleaseModel? release =
                await _mainViewModel.UpdateService.CheckForAppUpdatesAsync();
            if (release != null)
            {
                _mainViewModel.LatestGitHubRelease = release.TagName;
                string currentVerStr = MainViewModel.FormatVersion(
                    Assembly.GetExecutingAssembly().GetName().Version
                );
                string latestVerStr = release.TagName.TrimStart('v');
                LatestGitHubVersion = release.TagName.StartsWith(
                    "v",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? release.TagName
                    : $"v{release.TagName}";

                bool newer = MainViewModel.IsNewerVersion(currentVerStr, latestVerStr);
                IsUpdateAvailable = newer;
                LatestVersionPill = newer ? "Update available" : "Up to date";
                EngineStateDisplay = "Ready";

                if (newer)
                {
                    UpdateStatus = $"Update available: {release.TagName}";
                    _ = await _mainViewModel.PerformUpdateCheckAsync(p => UpdateStatus = p);
                }
                else
                {
                    UpdateStatus = "You are up to date!";
                    Log.Information("Manual app update check complete. App is already up to date.");
                }
            }
            else
            {
                EngineStateDisplay = "Offline";
                UpdateStatus = "Error: Check connection.";
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Update check failed.");
            EngineStateDisplay = "Error";
            UpdateStatus = "Error: Check connection.";
            await _mainViewModel.ShowNotificationAsync(
                "Update Error",
                "Failed to reach GitHub API. Please check your internet connection.",
                ControlAppearance.Danger
            );
        }
        finally
        {
            IsLoading = false;
            _settingsService.Save(Settings);
            ExecuteRefreshLogs(null);
        }
    }

    public async Task InitializeOnLoadedAsync()
    {
        try
        {
            _ = await _premiumService.CheckAndSyncAssignedLicenseAsync(Settings, forceSync: true);
            OnPropertyChanged(nameof(IsPremiumUnlocked));
            OnPropertyChanged(nameof(IsPremiumLocked));
            OnPropertyChanged(nameof(IsTrialActive));
            OnPropertyChanged(nameof(CanStartTrial));
            OnPropertyChanged(nameof(HasClaimedTrial));
            OnPropertyChanged(nameof(TrialRemainingDisplay));
            OnPropertyChanged(nameof(PremiumExpirationDisplay));
            OnPropertyChanged(nameof(HasExpirationDisplay));
            OnPropertyChanged(nameof(LicenseTierDisplay));
            OnPropertyChanged(nameof(DisplayLicenseKey));
            OnPropertyChanged(nameof(LastVerifiedDisplay));
            OnPropertyChanged(nameof(HasCryptographicSignature));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Silent license sync on settings launch encountered an error.");
        }

        try
        {
            if (_mainViewModel.LatestGitHubRelease != null)
            {
                LatestGitHubVersion = _mainViewModel.LatestGitHubRelease.StartsWith(
                    "v",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? _mainViewModel.LatestGitHubRelease
                    : $"v{_mainViewModel.LatestGitHubRelease}";
                string currentVerStr = MainViewModel.FormatVersion(
                    Assembly.GetExecutingAssembly().GetName().Version
                );
                string latestVerStr = _mainViewModel.LatestGitHubRelease.TrimStart('v');
                bool newer = MainViewModel.IsNewerVersion(currentVerStr, latestVerStr);
                IsUpdateAvailable = newer;
                LatestVersionPill = newer ? "Update available" : "Up to date";
            }
            else
            {
                GitHubReleaseModel? release =
                    await _mainViewModel.UpdateService.CheckForAppUpdatesAsync();
                if (release != null)
                {
                    _mainViewModel.LatestGitHubRelease = release.TagName;
                    LatestGitHubVersion = release.TagName.StartsWith(
                        "v",
                        StringComparison.OrdinalIgnoreCase
                    )
                        ? release.TagName
                        : $"v{release.TagName}";
                    string currentVerStr = MainViewModel.FormatVersion(
                        Assembly.GetExecutingAssembly().GetName().Version
                    );
                    string latestVerStr = release.TagName.TrimStart('v');
                    bool newer = MainViewModel.IsNewerVersion(currentVerStr, latestVerStr);
                    IsUpdateAvailable = newer;
                    LatestVersionPill = newer ? "Update available" : "Up to date";
                }
                else
                {
                    LatestGitHubVersion = AppVersion;
                    LatestVersionPill = "Up to date";
                }
            }
        }
        catch
        {
            LatestGitHubVersion = AppVersion;
            LatestVersionPill = "Up to date";
        }
    }

    private async Task ExecuteSyncLicense(object? parameter)
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            bool isUnlocked = await _premiumService.CheckAndSyncAssignedLicenseAsync(
                Settings,
                forceSync: true
            );

            OnPropertyChanged(nameof(IsPremiumUnlocked));
            OnPropertyChanged(nameof(IsPremiumLocked));
            OnPropertyChanged(nameof(IsTrialActive));
            OnPropertyChanged(nameof(CanStartTrial));
            OnPropertyChanged(nameof(HasClaimedTrial));
            OnPropertyChanged(nameof(TrialRemainingDisplay));
            OnPropertyChanged(nameof(PremiumExpirationDisplay));
            OnPropertyChanged(nameof(LicenseTierDisplay));
            OnPropertyChanged(nameof(DisplayLicenseKey));
            OnPropertyChanged(nameof(LastVerifiedDisplay));
            OnPropertyChanged(nameof(HasCryptographicSignature));

            await _mainViewModel.ShowNotificationAsync(
                "License Synced",
                isUnlocked
                    ? "Your license was successfully verified and synced with the server."
                    : "No active server license is currently assigned to this device.",
                isUnlocked ? ControlAppearance.Success : ControlAppearance.Info
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to sync license.");
            await _mainViewModel.ShowNotificationAsync(
                "Sync Error",
                ex.Message,
                ControlAppearance.Danger
            );
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshHtml5ScriptTogglesAsync()
    {
        try
        {
            string cachePath = _mainViewModel.CachePath;
            (bool shadersEnabled, bool skinAnimEnabled) = await Task.Run(() =>
                _mainViewModel.Html5CacheService.GetAppliedScriptStatus(cachePath)
            );
            if (!IsHtml5Client)
            {
                return;
            }

            Settings.EnableProTankiPack = shadersEnabled;
            Settings.EnableSkinAnimations = skinAnimEnabled;
            OnPropertyChanged(nameof(EnableProTankiPack));
            OnPropertyChanged(nameof(EnableSkinAnimations));
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Failed to refresh HTML5 script toggles.");
        }
    }

    private async Task HandleShadersToggleAsync(
        bool enable,
        bool isHotkeyUpdate,
        string? previousHotkey = null
    )
    {
        await _scriptPatchLock.WaitAsync();
        try
        {
            if (!IsHtml5Client)
            {
                if (enable && !isHotkeyUpdate)
                {
                    Settings.EnableProTankiPack = false;
                    _settingsService.Save(Settings);
                    OnPropertyChanged(nameof(EnableProTankiPack));
                    await _mainViewModel.ShowNotificationAsync(
                        "HTML5 Client Only",
                        "ProTanki Pack is only supported on the HTML5 client.",
                        ControlAppearance.Caution
                    );
                }
                return;
            }

            IsLoading = true;
            UpdateStatus = enable
                ? (
                    isHotkeyUpdate
                        ? $"Updating ProTanki Pack Hotkey ({SelectedShaderHotkey})..."
                        : "Applying ProTanki Pack..."
                )
                : "Removing ProTanki Pack...";

            if (
                Helpers.Utilities.FileHelper.IsGameRunning()
                || Helpers.Utilities.FileHelper.IsCacheFileLocked(_mainViewModel.CachePath)
            )
            {
                if (!isHotkeyUpdate)
                {
                    Settings.EnableProTankiPack = !enable;
                    OnPropertyChanged(nameof(EnableProTankiPack));
                }
                else if (!string.IsNullOrEmpty(previousHotkey))
                {
                    Settings.ProTankiPackHotkey = previousHotkey;
                    OnPropertyChanged(nameof(SelectedShaderHotkey));
                }

                await _mainViewModel.ShowNotificationAsync(
                    "ProTanki Running",
                    "ProTanki is currently running or cache files are locked. Please close the game before changing ProTanki Pack.",
                    ControlAppearance.Caution
                );
                return;
            }

            string hotkeyToApply = SelectedShaderHotkey;
            (bool success, string? error) = enable
                ? await Task.Run(() =>
                    _mainViewModel.Html5CacheService.ApplyShaders(
                        _mainViewModel.CachePath,
                        hotkeyToApply
                    )
                )
                : await Task.Run(() =>
                    _mainViewModel.Html5CacheService.RestoreDefaultShaders(_mainViewModel.CachePath)
                );

            if (success)
            {
                Settings.EnableProTankiPack = enable;
                Settings.EnableSkinAnimations = false;
                _settingsService.Save(Settings);
                if (enable || Settings.EnablePackLogListener)
                {
                    _packLogListener?.Start();
                }
                else
                {
                    _packLogListener?.Stop();
                }
                await _mainViewModel.ShowNotificationAsync(
                    enable ? "ProTanki Pack Active" : "ProTanki Pack Removed",
                    enable
                        ? $"ProTanki Pack applied! Launch ProTanki and press [{hotkeyToApply}] in-game to hide or show the menu."
                        : "ProTanki Pack removed from client cache.",
                    enable ? ControlAppearance.Success : ControlAppearance.Info
                );
            }
            else
            {
                if (!isHotkeyUpdate)
                {
                    Settings.EnableProTankiPack = !enable;
                    _settingsService.Save(Settings);
                    OnPropertyChanged(nameof(EnableProTankiPack));
                }
                else if (!string.IsNullOrEmpty(previousHotkey))
                {
                    Settings.ProTankiPackHotkey = previousHotkey;
                    _settingsService.Save(Settings);
                    OnPropertyChanged(nameof(SelectedShaderHotkey));
                }

                await _mainViewModel.ShowNotificationAsync(
                    "Error",
                    error ?? "Failed to update HTML5 3D Shaders.",
                    ControlAppearance.Danger
                );
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to toggle HTML5 3D Shaders.");
            if (!isHotkeyUpdate)
            {
                Settings.EnableProTankiPack = !enable;
                OnPropertyChanged(nameof(EnableProTankiPack));
            }
            else if (!string.IsNullOrEmpty(previousHotkey))
            {
                Settings.ProTankiPackHotkey = previousHotkey;
                OnPropertyChanged(nameof(SelectedShaderHotkey));
            }

            await _mainViewModel.ShowNotificationAsync(
                "Error",
                ex.Message,
                ControlAppearance.Danger
            );
        }
        finally
        {
            IsLoading = false;
            _ = _scriptPatchLock.Release();
        }
    }

    private async Task HandleSkinAnimationsToggleAsync(
        bool enable,
        bool isHotkeyUpdate,
        string? previousHotkey = null
    )
    {
        await _scriptPatchLock.WaitAsync();
        try
        {
            if (!IsHtml5Client)
            {
                if (enable && !isHotkeyUpdate)
                {
                    Settings.EnableSkinAnimations = false;
                    _settingsService.Save(Settings);
                    OnPropertyChanged(nameof(EnableSkinAnimations));
                    await _mainViewModel.ShowNotificationAsync(
                        "HTML5 Client Only",
                        "3D Skin Animations are only supported on the HTML5 client.",
                        ControlAppearance.Caution
                    );
                }
                return;
            }

            IsLoading = true;
            UpdateStatus = enable
                ? (
                    isHotkeyUpdate
                        ? $"Updating Skin Animation Hotkey ({SelectedSkinAnimHotkey})..."
                        : "Applying 3D Skin Animations..."
                )
                : "Removing 3D Skin Animations...";

            if (
                Helpers.Utilities.FileHelper.IsGameRunning()
                || Helpers.Utilities.FileHelper.IsCacheFileLocked(_mainViewModel.CachePath)
            )
            {
                if (!isHotkeyUpdate)
                {
                    Settings.EnableSkinAnimations = !enable;
                    OnPropertyChanged(nameof(EnableSkinAnimations));
                }
                else if (!string.IsNullOrEmpty(previousHotkey))
                {
                    Settings.SkinAnimMenuHotkey = previousHotkey;
                    OnPropertyChanged(nameof(SelectedSkinAnimHotkey));
                }

                await _mainViewModel.ShowNotificationAsync(
                    "ProTanki Running",
                    "ProTanki is currently running or cache files are locked. Please close the game before changing Skin Animations.",
                    ControlAppearance.Caution
                );
                return;
            }

            string hotkeyToApply = SelectedSkinAnimHotkey;
            (bool success, string? error) = enable
                ? await Task.Run(() =>
                    _mainViewModel.Html5CacheService.ApplySkinAnimations(
                        _mainViewModel.CachePath,
                        hotkeyToApply
                    )
                )
                : await Task.Run(() =>
                    _mainViewModel.Html5CacheService.RestoreDefaultSkinAnimations(_mainViewModel.CachePath)
                );

            if (success)
            {
                Settings.EnableSkinAnimations = enable;
                _settingsService.Save(Settings);
                await _mainViewModel.ShowNotificationAsync(
                    enable ? "Skin Animations Active" : "Skin Animations Removed",
                    enable
                        ? $"3D Skin Animations applied! Launch ProTanki and press [{hotkeyToApply}] in-game to open the animation preset menu."
                        : "HTML5 3D Skin Animations removed from client cache.",
                    enable ? ControlAppearance.Success : ControlAppearance.Info
                );
            }
            else
            {
                if (!isHotkeyUpdate)
                {
                    Settings.EnableSkinAnimations = !enable;
                    _settingsService.Save(Settings);
                    OnPropertyChanged(nameof(EnableSkinAnimations));
                }
                else if (!string.IsNullOrEmpty(previousHotkey))
                {
                    Settings.SkinAnimMenuHotkey = previousHotkey;
                    _settingsService.Save(Settings);
                    OnPropertyChanged(nameof(SelectedSkinAnimHotkey));
                }

                await _mainViewModel.ShowNotificationAsync(
                    "Error",
                    error ?? "Failed to update HTML5 3D Skin Animations.",
                    ControlAppearance.Danger
                );
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to toggle HTML5 3D Skin Animations.");
            if (!isHotkeyUpdate)
            {
                Settings.EnableSkinAnimations = !enable;
                OnPropertyChanged(nameof(EnableSkinAnimations));
            }
            else if (!string.IsNullOrEmpty(previousHotkey))
            {
                Settings.SkinAnimMenuHotkey = previousHotkey;
                OnPropertyChanged(nameof(SelectedSkinAnimHotkey));
            }

            await _mainViewModel.ShowNotificationAsync(
                "Error",
                ex.Message,
                ControlAppearance.Danger
            );
        }
        finally
        {
            IsLoading = false;
            _ = _scriptPatchLock.Release();
        }
    }

    private void OnPackMenuHotkeyChanged(string hotkey)
    {
        _ = Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            OnPropertyChanged(nameof(AvailableShaderHotkeys));
            OnPropertyChanged(nameof(SelectedShaderHotkey));
        });
    }

    public void Dispose()
    {
        if (_packLogListener != null)
        {
            _packLogListener.MenuHotkeyChanged -= OnPackMenuHotkeyChanged;
        }
        GC.SuppressFinalize(this);
    }
}
