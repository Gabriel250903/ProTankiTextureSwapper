using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    private readonly MainViewModel _mainViewModel;

    private readonly ILocalizationService _localizationService;

    private readonly IPremiumService _premiumService;

    private readonly IDeviceBanService _deviceBanService;

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

    public string PremiumExpirationDisplay
    {
        get
        {
            if (!IsPremiumUnlocked)
            {
                return string.Empty;
            }

            if (IsTrialActive)
            {
                return "Free Trial (" + TrialRemainingDisplay + ")";
            }

            if (string.IsNullOrWhiteSpace(Settings.PremiumExpiresAtUtc)
                || (Settings.PremiumLicenseKey?.StartsWith("HALLOWEEN-", StringComparison.OrdinalIgnoreCase) == true))
            {
                return string.Empty;
            }

            if (DateTime.TryParse(Settings.PremiumExpiresAtUtc, out DateTime result2))
            {
                return _localizationService.GetString(
                    "Loc_Settings_ExpiresFormat",
                    result2.ToLocalTime().ToString("g")
                );
            }

            return _localizationService.GetString(
                "Loc_Settings_ExpiresFormat",
                Settings.PremiumExpiresAtUtc
            );
        }
    }

    public bool HasExpirationDisplay => !string.IsNullOrWhiteSpace(PremiumExpirationDisplay);

    public string ActivationStatusText
    {
        get => _activationStatusText;
        set => SetProperty(ref _activationStatusText, value, nameof(ActivationStatusText));
    }

    public string LicenseTierDisplay
    {
        get
        {
            if (!IsPremiumUnlocked)
            {
                return "Free Tier";
            }
            if (IsTrialActive)
            {
                return "3-Day Free Trial";
            }
            if (
                !string.IsNullOrEmpty(Settings.PremiumLicenseKey)
                && Settings.PremiumLicenseKey.StartsWith(
                    "HALLOWEEN-",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return "Lifetime Supporter";
            }
            if (!string.IsNullOrWhiteSpace(Settings.PremiumExpiresAtUtc))
            {
                return "Time-Limited Premium";
            }
            return "Lifetime Supporter";
        }
    }

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
            if (string.IsNullOrWhiteSpace(key))
            {
                return "—";
            }
            if (IsKeyRevealed)
            {
                return key;
            }
            if (key.Length <= 8)
            {
                return "••••••••";
            }
            return key[..4] + "-••••-••••-" + key[^4..];
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

    public string LastVerifiedDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Settings.LastVerifiedUtc))
            {
                return "Not verified online";
            }
            if (DateTime.TryParse(Settings.LastVerifiedUtc, out DateTime dt))
            {
                return dt.ToLocalTime().ToString("g");
            }
            return Settings.LastVerifiedUtc;
        }
    }

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
        get
        {
            return AvailableLanguages.FirstOrDefault(
                l => l.Code.Equals(Settings.Language, StringComparison.OrdinalIgnoreCase)
            )
            ?? AvailableLanguages.FirstOrDefault(
                l => l.Code.Equals("en", StringComparison.OrdinalIgnoreCase)
            )
            ?? (AvailableLanguages.Count > 0 ? AvailableLanguages[0] : null);
        }

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
            _mainViewModel.HalloweenThemeService.IsHalloweenActive ? "Halloween"
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

            if (string.Equals(value, "Halloween", StringComparison.OrdinalIgnoreCase))
            {
                Settings.IsHalloweenThemeSelected = true;
                _settingsService.Save(Settings);
                _mainViewModel.HalloweenThemeService.CheckAndApply(notify: true, force: true);
                Log.Information("Theme changed to Halloween.");
                _ = _mainViewModel.NotificationService.ShowAsync(
                    "Halloween Theme Active 🎃",
                    "Live visual effects are active. Higher CPU, RAM, and GPU usage is normal for animations.",
                    Wpf.Ui.Controls.ControlAppearance.Info
                );
            }
            else
            {
                Settings.IsHalloweenThemeSelected = false;
                Settings.ForceHalloweenPreview = false;
                ApplicationTheme theme = string.Equals(
                    value,
                    "Light",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? ApplicationTheme.Light
                    : ApplicationTheme.Dark;
                Settings.Theme = theme;
                _settingsService.Save(Settings);
                _mainViewModel.HalloweenThemeService.CheckAndApply(notify: true, force: true);
                Log.Information("Theme changed to {Theme}.", theme);
            }
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(CanConfigureHalloweenSound));
            OnPropertyChanged(nameof(IsHalloweenCustomizationVisible));
            ExecuteRefreshLogs(null);
        }
    }

    public IEnumerable<string> AvailableThemes
    {
        get
        {
            List<string> list = ["Dark", "Light"];
            if (_mainViewModel.HalloweenThemeService.IsWithinHalloweenWindow)
            {
                list.Add("Halloween");
            }
            return list;
        }
    }

    public bool IsWithinHalloweenPeriod =>
        _mainViewModel.HalloweenThemeService.IsWithinHalloweenWindow;

    public bool IsHalloweenCustomizationVisible =>
        _mainViewModel.HalloweenThemeService.IsHalloweenActive;

    public bool CanConfigureHalloweenSound => IsHalloweenCustomizationVisible;

    public bool EnableHalloweenAtmosphereAnimation
    {
        get => Settings.EnableHalloweenAtmosphereAnimation;
        set
        {
            if (Settings.EnableHalloweenAtmosphereAnimation != value)
            {
                Settings.EnableHalloweenAtmosphereAnimation = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableHalloweenAtmosphereAnimation));
                _mainViewModel.NotifyHalloweenVisualsChanged();
            }
        }
    }

    public bool EnableHalloweenSpookyFont
    {
        get => Settings.EnableHalloweenSpookyFont;
        set
        {
            if (Settings.EnableHalloweenSpookyFont != value)
            {
                Settings.EnableHalloweenSpookyFont = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableHalloweenSpookyFont));
                _mainViewModel.HalloweenThemeService.CheckAndApply(notify: true, force: true);
            }
        }
    }

    public bool EnableHalloweenAppLogo
    {
        get => Settings.EnableHalloweenAppLogo;
        set
        {
            if (Settings.EnableHalloweenAppLogo != value)
            {
                Settings.EnableHalloweenAppLogo = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableHalloweenAppLogo));
                _mainViewModel.NotifyHalloweenVisualsChanged();
            }
        }
    }

    public bool EnableHalloweenSound
    {
        get => Settings.EnableHalloweenSound;
        set
        {
            if (Settings.EnableHalloweenSound != value)
            {
                Settings.EnableHalloweenSound = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableHalloweenSound));
                if (value && _mainViewModel.IsHalloweenThemeActive)
                {
                    _mainViewModel.HalloweenSoundService.PlayHollowClick();
                    _mainViewModel.HalloweenSoundService.SetAmbientDrone(true);
                }
                else
                {
                    _mainViewModel.HalloweenSoundService.SetAmbientDrone(false);
                }
            }
        }
    }

    public bool EnableHalloweenRandomScares
    {
        get => Settings.EnableHalloweenRandomScares;
        set
        {
            if (Settings.EnableHalloweenRandomScares != value)
            {
                Settings.EnableHalloweenRandomScares = value;
                _settingsService.Save(Settings);
                OnPropertyChanged(nameof(EnableHalloweenRandomScares));
            }
        }
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
        $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0"}";

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
            string result;
            if (
                string.IsNullOrEmpty(lastUpdateStatus)
                || lastUpdateStatus.Equals("Idle", StringComparison.OrdinalIgnoreCase)
            )
            {
                result = _localizationService.GetString("Loc_Msg_Idle");
            }
            else if (
                lastUpdateStatus.Equals("You are up to date!", StringComparison.OrdinalIgnoreCase)
            )
            {
                result = _localizationService.GetString("Loc_Msg_UpToDate");
            }
            else
            {
                result =
                    lastUpdateStatus.Equals(
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
            }
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

    public ICommand CheckForUpdatesCommand { get; }

    public ICommand CopyHardwareIdCommand { get; }

    public string LogContent
    {
        get => _logContent;
        set => SetProperty(ref _logContent, value, nameof(LogContent));
    }

    public ICommand RefreshLogsCommand { get; }

    public ICommand OpenLogsFolderCommand { get; }

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
        Settings = mainViewModel.Settings;
        BuyPremiumCommand = new RelayCommand(
            _ =>
            {
                ExecuteBuyPremium();
            }
        );
        UseWindowsUsernameCommand = new RelayCommand(
            _ =>
            {
                UserNickname = Environment.UserName;
            }
        );
        ActivatePremiumCommand = new AsyncRelayCommand(
            ExecuteActivatePremium,
            _ => !IsLoading
        );
        StartFreeTrialCommand = new AsyncRelayCommand(
            ExecuteStartFreeTrial,
            _ => !IsLoading && CanStartTrial
        );
        CheckForUpdatesCommand = new AsyncRelayCommand(
            ExecuteCheckForUpdates,
            _ => !IsLoading
        );
        RefreshLogsCommand = new RelayCommand(ExecuteRefreshLogs);
        OpenLogsFolderCommand = new RelayCommand(ExecuteOpenLogsFolder);
        ToggleKeyVisibilityCommand = new RelayCommand(
            _ => IsKeyRevealed = !IsKeyRevealed
        );
        CopyLicenseKeyCommand = new RelayCommand(
            _ =>
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
            }
        );
        SyncLicenseCommand = new AsyncRelayCommand(
            ExecuteSyncLicense,
            _ => !IsLoading
        );
        
        CopyHardwareIdCommand = new RelayCommand(
            _ =>
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
            }
        );
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

        _mainViewModel.HalloweenThemeService.HalloweenThemeChanged += (sender, active) =>
        {
            OnPropertyChanged(nameof(AvailableThemes));
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(CanConfigureHalloweenSound));
            OnPropertyChanged(nameof(IsHalloweenCustomizationVisible));
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
                OnPropertyChanged(nameof(PremiumExpirationDisplay));
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

            GitHubReleaseModel? release = await _mainViewModel.UpdateService.CheckForAppUpdatesAsync();
            if (release != null)
            {
                _mainViewModel.LatestGitHubRelease = release.TagName;
                string currentVerStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
                string latestVerStr = release.TagName.TrimStart('v');
                LatestGitHubVersion = release.TagName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? release.TagName
                    : $"v{release.TagName}";

                bool newer = MainViewModel.IsNewerVersion(currentVerStr, latestVerStr);
                IsUpdateAvailable = newer;
                LatestVersionPill = newer ? "Update available" : "Up to date";
                EngineStateDisplay = "Ready";

                if (newer)
                {
                    UpdateStatus = $"Update available: {release.TagName}";
                    await _mainViewModel.PerformUpdateCheckAsync(p => UpdateStatus = p);
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
            await _premiumService.CheckAndSyncAssignedLicenseAsync(
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
                LatestGitHubVersion = _mainViewModel.LatestGitHubRelease.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                    ? _mainViewModel.LatestGitHubRelease
                    : $"v{_mainViewModel.LatestGitHubRelease}";
                string currentVerStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
                string latestVerStr = _mainViewModel.LatestGitHubRelease.TrimStart('v');
                bool newer = MainViewModel.IsNewerVersion(currentVerStr, latestVerStr);
                IsUpdateAvailable = newer;
                LatestVersionPill = newer ? "Update available" : "Up to date";
            }
            else
            {
                GitHubReleaseModel? release = await _mainViewModel.UpdateService.CheckForAppUpdatesAsync();
                if (release != null)
                {
                    _mainViewModel.LatestGitHubRelease = release.TagName;
                    LatestGitHubVersion = release.TagName.StartsWith("v", StringComparison.OrdinalIgnoreCase)
                        ? release.TagName
                        : $"v{release.TagName}";
                    string currentVerStr = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.0.0";
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
        if (IsLoading) return;
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
}
