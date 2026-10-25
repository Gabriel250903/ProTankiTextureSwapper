using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.Services;
using TextureSwapper.ViewModels.Base;
using TextureSwapper.ViewModels.Tabs;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace TextureSwapper.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private bool _isLoading;
        private string _updateStatus = string.Empty;
        private string _cachePath = string.Empty;

        public ISwapService SwapService { get; }
        public IUpdateService UpdateService { get; }
        public ITextureDownloadService TextureDownloadService { get; }
        public ISkinSyncService SkinSyncService { get; }
        public ISettingsService SettingsService { get; }
        public IWindowService WindowService { get; }
        public INotificationService NotificationService { get; }
        public ICacheService CacheService { get; }
        public IPremiumService PremiumService { get; }
        public IDeviceBanService DeviceBanService { get; }
        public ILocalizationService LocalizationService { get; }
        public IShotEffectComposerRegistry ShotEffectComposers { get; }
        public IFirebirdShotComposer FirebirdShotComposer => ShotEffectComposers.Firebird;
        public IFreezeShotComposer FreezeShotComposer => ShotEffectComposers.Freeze;
        public IRicochetShotComposer RicochetShotComposer => ShotEffectComposers.Ricochet;
        public ITwinsShotComposer TwinsShotComposer => ShotEffectComposers.Twins;
        public IRailgunShotComposer RailgunShotComposer => ShotEffectComposers.Railgun;
        public IHammerShotComposer HammerShotComposer => ShotEffectComposers.Hammer;
        public IIsidaShotComposer IsidaShotComposer => ShotEffectComposers.Isida;
        public IShaftShotComposer ShaftShotComposer => ShotEffectComposers.Shaft;
        public IVulcanShotComposer VulcanShotComposer => ShotEffectComposers.Vulcan;
        public IThunderShotComposer ThunderShotComposer => ShotEffectComposers.Thunder;
        public IDustEffectComposer DustEffectComposer => ShotEffectComposers.DustEffect;
        public ISmokyShotComposer SmokyShotComposer => ShotEffectComposers.Smoky;
        public IParachuteComposer ParachuteComposer { get; }
        public IPropComposer PropComposer { get; }
        public HalloweenThemeService HalloweenThemeService { get; }
        public HalloweenSoundService HalloweenSoundService { get; }
        public bool IsHalloweenThemeActive => HalloweenThemeService.IsHalloweenActive;
        public bool IsHalloweenAtmosphereVisible =>
            IsHalloweenThemeActive && Settings.EnableHalloweenAtmosphereAnimation;

        public string AppLogoSource =>
            (IsHalloweenThemeActive && Settings.EnableHalloweenAppLogo)
                ? "pack://application:,,,/Assets/halloween_logo.png"
                : "pack://application:,,,/Assets/logo.png";

        public string NavSkinsText => LocalizationService.GetString("Loc_Nav_Skins") ?? "Skins";
        public string NavPaintsText => LocalizationService.GetString("Loc_Nav_Paints") ?? "Paints";
        public string NavBackupsText =>
            LocalizationService.GetString("Loc_Nav_BackupHistory") ?? "Backup History";
        public string NavShotEffectsText =>
            LocalizationService.GetString("Loc_Nav_ShotEffects") ?? "Shot Effects";
        public string NavParachutesText =>
            LocalizationService.GetString("Loc_Nav_ParachuteEditor") ?? "Parachute Editor";
        public string NavPropsText =>
            LocalizationService.GetString("Loc_Nav_PropsEditor") ?? "Props Editor";
        public string NavAdminText =>
            LocalizationService.GetString("Loc_Nav_AdminPanel") ?? "Admin Panel";
        public string NavSettingsText =>
            LocalizationService.GetString("Loc_Nav_Settings") ?? "Settings";

        public string AppTitleText =>
            (IsHalloweenThemeActive && Settings.EnableHalloweenAppLogo)
                ? "🎃 ProTanki Texture Swapper 🎃"
                : "ProTanki Texture Swapper";

        public void NotifyHalloweenVisualsChanged()
        {
            OnPropertyChanged(nameof(IsHalloweenAtmosphereVisible));
            OnPropertyChanged(nameof(AppLogoSource));
            OnPropertyChanged(nameof(AppTitleText));
        }

        public string DeviceHwidDisplay => DeviceBanService.GetDeviceHardwareId();

        private bool _isDeviceBanned;
        public bool IsDeviceBanned
        {
            get => _isDeviceBanned;
            set => SetProperty(ref _isDeviceBanned, value);
        }

        private bool _isDeviceTempBanned;
        public bool IsDeviceTempBanned
        {
            get => _isDeviceTempBanned;
            set => SetProperty(ref _isDeviceTempBanned, value);
        }

        private string _deviceBanTagText = "SUSPENDED";
        public string DeviceBanTagText
        {
            get => _deviceBanTagText;
            set => SetProperty(ref _deviceBanTagText, value);
        }

        private string _deviceBanHeading = "Device Access Restricted";
        public string DeviceBanHeading
        {
            get => _deviceBanHeading;
            set => SetProperty(ref _deviceBanHeading, value);
        }

        private string _deviceBanExpiryDisplay = string.Empty;
        public string DeviceBanExpiryDisplay
        {
            get => _deviceBanExpiryDisplay;
            set => SetProperty(ref _deviceBanExpiryDisplay, value);
        }

        private string _deviceBanReason = string.Empty;
        public string DeviceBanReason
        {
            get => _deviceBanReason;
            set => SetProperty(ref _deviceBanReason, value);
        }

        public SkinsTabViewModel SkinsTabVM { get; }
        public PaintsTabViewModel PaintsTabVM { get; }
        public ShotEffectsTabViewModel ShotEffectsTabVM { get; }
        public BackupsTabViewModel BackupsTabVM { get; }
        public AdminViewModel AdminVM { get; }

        public AppSettings Settings { get; }

        public List<SkinModel> AllSkins { get; set; } = [];

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        public string CachePath
        {
            get => _cachePath;
            set => SetProperty(ref _cachePath, value);
        }

        public double UIScale
        {
            get => Settings.UIScale;
            set
            {
                if (Settings.UIScale != value)
                {
                    Settings.UIScale = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public ICommand BrowseCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ClearCacheCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public MainViewModel(
            INotificationService notificationService,
            ISettingsService settingsService,
            ISwapService swapService,
            ISkinSyncService skinSyncService,
            IUpdateService updateService,
            ITextureDownloadService textureDownloadService,
            IWindowService windowService,
            ICacheService cacheService,
            IPremiumService premiumService,
            IDeviceBanService deviceBanService,
            ILocalizationService localizationService,
            IShotEffectComposerRegistry shotEffectComposers,
            IParachuteComposer parachuteComposer,
            IPropComposer propComposer,
            HalloweenThemeService halloweenThemeService,
            HalloweenSoundService halloweenSoundService
        )
        {
            NotificationService = notificationService;
            SettingsService = settingsService;
            SwapService = swapService;
            SkinSyncService = skinSyncService;
            UpdateService = updateService;
            TextureDownloadService = textureDownloadService;
            WindowService = windowService;
            CacheService = cacheService;
            PremiumService = premiumService;
            DeviceBanService = deviceBanService;
            LocalizationService = localizationService;
            ShotEffectComposers = shotEffectComposers;
            ParachuteComposer = parachuteComposer;
            PropComposer = propComposer;
            HalloweenThemeService = halloweenThemeService;
            HalloweenSoundService = halloweenSoundService;
            HalloweenThemeService.HalloweenThemeChanged += (_, active) =>
            {
                if (active)
                {
                    HalloweenSoundService.PlayGhostWail();
                    HalloweenSoundService.SetAmbientDrone(true);
                }
                else
                {
                    HalloweenSoundService.SetAmbientDrone(false);
                }
            };
            HalloweenThemeService.HalloweenThemeChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsHalloweenThemeActive));
                OnPropertyChanged(nameof(IsHalloweenAtmosphereVisible));
                OnPropertyChanged(nameof(AppLogoSource));
                OnPropertyChanged(nameof(AppTitleText));
                OnPropertyChanged(nameof(NavSkinsText));
                OnPropertyChanged(nameof(NavPaintsText));
                OnPropertyChanged(nameof(NavBackupsText));
                OnPropertyChanged(nameof(NavShotEffectsText));
                OnPropertyChanged(nameof(NavParachutesText));
                OnPropertyChanged(nameof(NavPropsText));
                OnPropertyChanged(nameof(NavAdminText));
                OnPropertyChanged(nameof(NavSettingsText));
            };

            Settings = SettingsService.Load();
            HalloweenThemeService.CheckAndApply(notify: false);

            SkinsTabVM = new SkinsTabViewModel(this);
            PaintsTabVM = new PaintsTabViewModel(this);
            ShotEffectsTabVM = new ShotEffectsTabViewModel(this);
            BackupsTabVM = new BackupsTabViewModel(this);
            AdminVM = new AdminViewModel(this, notificationService);

            CachePath = Settings.CustomCachePath ?? SwapService.DetectCachePath();

            BrowseCommand = new RelayCommand(ExecuteBrowse);
            RestoreCommand = new AsyncRelayCommand(ExecuteRestore, _ => !IsLoading);
            ClearCacheCommand = new AsyncRelayCommand(ExecuteClearCache, _ => !IsLoading);
            OpenSettingsCommand = new RelayCommand(_ => WindowService.ShowSettingsDialog());

            DeviceBanService.DeviceBanned += (_, res) =>
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyBanResult(res));
            };
            DeviceBanService.DeviceUnbanned += (_, _) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsDeviceBanned = false;
                    IsDeviceTempBanned = false;
                });
            };

            PremiumService.PremiumStatusChanged += (_, isUnlocked) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    Settings.IsPremiumUnlocked = isUnlocked;
                    SettingsService.Save(Settings);
                    OnPropertyChanged(nameof(Settings));
                });
            };

            _ = InitializeAsync()
                .ContinueWith(
                    t =>
                    {
                        if (t.IsFaulted && t.Exception != null)
                        {
                            Log.Fatal(t.Exception, "Critical initialization failure.");
                            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
                            {
                                IsLoading = false;
                                UpdateStatus = "Initialization failed.";
                            });
                        }
                    },
                    TaskScheduler.Default
                );
        }

        private void ApplyBanResult(DeviceBanCheckResult res)
        {
            if (res.IsBanned)
            {
                IsDeviceBanned = true;
                IsDeviceTempBanned = res.IsTemporary;
                DeviceBanReason = string.IsNullOrWhiteSpace(res.Reason)
                    ? "Violating Terms of Service"
                    : res.Reason;
                DeviceBanExpiryDisplay = res.ExpiresAt;
                DeviceBanTagText = res.IsTemporary ? "TEMPORARY BAN" : "PERMANENT BAN";
                DeviceBanHeading = res.IsTemporary
                    ? "Device Temporarily Suspended"
                    : "Device Permanently Suspended";
            }
            else
            {
                IsDeviceBanned = false;
                IsDeviceTempBanned = false;
            }
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            UpdateStatus = "Initializing...";

            try
            {
                try
                {
                    DeviceBanCheckResult banCheck =
                        await DeviceBanService.VerifyDeviceStatusAsync();
                    ApplyBanResult(banCheck);
                    if (banCheck.IsBanned)
                    {
                        IsLoading = false;
                        return;
                    }
                    DeviceBanService.StartPeriodicVerification(TimeSpan.FromMinutes(5));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed initial device ban verification.");
                }

                try
                {
                    bool isUnlocked = await PremiumService.CheckAndSyncAssignedLicenseAsync(
                        Settings,
                        forceSync: true
                    );
                    Log.Information(
                        "Initial premium license sync completed. IsUnlocked: {IsUnlocked}",
                        isUnlocked
                    );
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed initial premium license sync.");
                }

                try
                {
                    await Task.Run(() =>
                        SwapService.PurgeOldBackups(Settings.MaxBackupRetentionDays)
                    );
                    BackupsTabVM.LoadBackups();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to purge or load backups.");
                }

                if (!TextureDownloadService.AreTexturesInstalled())
                {
                    UpdateStatus = "Downloading textures...";
                    bool downloaded = await TextureDownloadService.DownloadAndExtractTexturesAsync(
                        (status, percent) =>
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                UpdateStatus = status;
                            });
                        }
                    );

                    if (!downloaded)
                    {
                        Log.Warning(
                            "Could not download initial texture pack. App will run in offline mode."
                        );
                        _ = NotificationService.ShowAsync(
                            "Texture Pack",
                            "Could not download texture pack. The app will continue in offline mode.",
                            ControlAppearance.Caution
                        );
                    }
                }

                await PaintsTabVM.LoadInGamePaintsAsync();
                await ShotEffectsTabVM.LoadShotEffectsAsync();

                UpdateStatus = "Checking for updates...";

                await LoadSkinsAsync();
                _ = await CheckForAppUpdatesAsync();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled exception during initialization.");
            }
            finally
            {
                IsLoading = false;
                UpdateStatus = string.Empty;
            }
        }

        public async Task ReloadSkinsDataAsync()
        {
            await LoadSkinsAsync();
        }

        public async Task LoadSkinsAsync()
        {
            try
            {
                IsLoading = true;

                void HandleProgressChanged(string status)
                {
                    _ = (Application.Current?.Dispatcher.Invoke(() => UpdateStatus = status));
                }

                SkinSyncService.ProgressChanged += HandleProgressChanged;

                (List<SkinModel>? skins, List<InGamePaintModel>? remoteInGamePaints) =
                    await SkinSyncService.SyncAndLoadSkinsAsync();

                SkinSyncService.ProgressChanged -= HandleProgressChanged;

                if (UpdateService.IsOffline)
                {
                    await NotificationService.ShowAsync(
                        "Offline Mode",
                        "Running in offline mode. Local databases and cached textures will be used.",
                        ControlAppearance.Info
                    );
                }

                AllSkins = skins ?? [];
                SkinsTabVM.OnSkinsLoaded();
                PaintsTabVM.OnSkinsLoaded();

                if (remoteInGamePaints != null)
                {
                    await PaintsTabVM.LoadInGamePaintsAsync();
                }

                int missingPreviewsCount = PaintsTabVM.InGamePaints.Count(p =>
                    !string.IsNullOrEmpty(p.PreviewImage)
                    && !File.Exists(
                        Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            p.PreviewImage.Replace("\\", "/")
                        )
                    )
                );
                if (missingPreviewsCount > 0 && !UpdateService.IsOffline)
                {
                    int completedPreviews = 0;
                    object progressLock = new();
                    using SemaphoreSlim semaphore = new(10);
                    List<Task> downloadTasks = [];

                    foreach (InGamePaintModel paint in PaintsTabVM.InGamePaints)
                    {
                        if (string.IsNullOrEmpty(paint.PreviewImage))
                        {
                            continue;
                        }

                        string localPreview = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            paint.PreviewImage.Replace("\\", "/")
                        );
                        if (File.Exists(localPreview))
                        {
                            continue;
                        }

                        string remoteUrl =
                            $"{Constants.GitHubRawUrl}/{paint.PreviewImage.Replace("\\", "/")}";
                        downloadTasks.Add(
                            Task.Run(async () =>
                            {
                                await semaphore.WaitAsync();
                                try
                                {
                                    lock (progressLock)
                                    {
                                        UpdateStatus =
                                            $"Syncing in-game paints ({completedPreviews + 1}/{missingPreviewsCount}): {paint.Name}...";
                                    }

                                    Log.Information(
                                        "Downloading missing in-game paint preview: {Path}",
                                        paint.PreviewImage
                                    );
                                    _ = Directory.CreateDirectory(
                                        Path.GetDirectoryName(localPreview)!
                                    );
                                    using HttpResponseMessage res =
                                        await UpdateService.GetWithRetryAsync(remoteUrl);
                                    byte[] data = await res.Content.ReadAsByteArrayAsync();
                                    await File.WriteAllBytesAsync(localPreview, data);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(
                                        "Failed to download in-game paint preview {Name}: {Message}",
                                        paint.Name,
                                        ex.Message
                                    );
                                }
                                finally
                                {
                                    int currentCompleted = Interlocked.Increment(
                                        ref completedPreviews
                                    );
                                    lock (progressLock)
                                    {
                                        UpdateStatus =
                                            $"Syncing in-game paints ({currentCompleted}/{missingPreviewsCount})...";
                                    }
                                    _ = semaphore.Release();
                                }
                            })
                        );
                    }

                    await Task.WhenAll(downloadTasks);
                }

                UpdateStatus = string.Empty;
                IsLoading = false;
            }
            catch (Exception ex)
            {
                IsLoading = false;
                Log.Error(ex, "Failed to load skins.");
                await NotificationService.ShowAsync(
                    "Error",
                    $"Failed to load skins: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
        }

        public async Task<bool> EnsureSafeToOperate()
        {
            if (string.IsNullOrWhiteSpace(CachePath) || !Directory.Exists(CachePath))
            {
                await NotificationService.ShowAsync(
                    "Error",
                    "ProTanki cache folder path is not set or invalid. Please configure it in Settings.",
                    ControlAppearance.Danger
                );
                return false;
            }

            if (CacheService.IsGameRunning())
            {
                MessageBox messageBox = new()
                {
                    Title = "Game is Running",
                    Content =
                        "ProTanki is currently running. Please close the game before swapping textures to avoid cache file access locks.",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = string.Empty,
                };
                _ = await messageBox.ShowDialogAsync();
                return false;
            }

            return true;
        }

        public void SaveTheme(ApplicationTheme theme)
        {
            Settings.Theme = theme;
            SaveSettings();
        }

        public void SaveSettings()
        {
            SettingsService.Save(Settings);
        }

        public async Task TriggerUpdateCheckAsync()
        {
            IsLoading = true;
            UpdateStatus = "Checking for updates...";
            _ = await CheckForAppUpdatesAsync(p => UpdateStatus = p);
            IsLoading = false;
            UpdateStatus = string.Empty;
        }

        internal async Task<bool> PerformUpdateCheckAsync(Action<string> onProgress)
        {
            return await CheckForAppUpdatesAsync(onProgress);
        }

        public string? LatestGitHubRelease { get; internal set; }

        public async Task ShowNotificationAsync(
            string title,
            string message,
            ControlAppearance appearance
        )
        {
            await NotificationService.ShowAsync(title, message, appearance);
        }

        private async Task<bool> CheckForAppUpdatesAsync(Action<string>? onProgress = null)
        {
            onProgress?.Invoke("Checking for updates...");

            GitHubReleaseModel? release = await UpdateService.CheckForAppUpdatesAsync();
            if (release != null)
            {
                LatestGitHubRelease = release.TagName;
                string currentVersion =
                    Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";
                string latestVersion = release.TagName.TrimStart('v');

                Log.Information(
                    $"Current local version = {currentVersion}, Latest GitHub version = {latestVersion}"
                );

                if (IsNewerVersion(currentVersion, latestVersion))
                {
                    onProgress?.Invoke($"Update available: {release.TagName}");

                    string updateContent =
                        $"A new version ({release.TagName}) is available. Download and install it now?";
                    if (!string.IsNullOrWhiteSpace(release.Body))
                    {
                        updateContent += $"\n\nRelease notes:\n\n{release.Body}";
                    }

                    FlowDocumentScrollViewer docViewer = new()
                    {
                        MaxHeight = 350,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Document = MarkdownParser.ParseToFlowDocument(updateContent),
                        Background = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                    };

                    MessageBox messageBox = new()
                    {
                        Title = "Update Available",
                        Content = docViewer,
                        PrimaryButtonText = "Update Now",
                        SecondaryButtonText = "Later",
                        MaxWidth = 580,
                    };

                    MessageBoxResult result = await messageBox.ShowDialogAsync();
                    if (result == MessageBoxResult.Primary)
                    {
                        onProgress?.Invoke("Downloading update...");
                        try
                        {
                            GitHubAssetModel exeAsset =
                                release.Assets.FirstOrDefault(a =>
                                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                                )
                                ?? throw new FileNotFoundException(
                                    "No installer executable (.exe) found in release assets."
                                );
                            void HandleProgress(double p)
                            {
                                onProgress?.Invoke($"Downloading update ({p:F0}%)...");
                            }

                            await UpdateService.DownloadAndRunInstallerAsync(
                                exeAsset.BrowserDownloadUrl,
                                HandleProgress
                            );
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to download update installer.");
                            await NotificationService.ShowAsync(
                                "Update Error",
                                $"Failed to download update installer: {ex.Message}",
                                ControlAppearance.Danger
                            );
                        }
                    }
                    return true;
                }
            }
            onProgress?.Invoke("App is up-to-date.");
            return false;
        }

        internal static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            try
            {
                Version current = new(currentVersion);
                Version latest = new(latestVersion);
                return latest > current;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"Failed to parse versions: {currentVersion} vs {latestVersion}");
                return false;
            }
        }

        private void ExecuteBrowse(object? parameter)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new()
            {
                Title = "Select ProTanki Cache Folder",
                InitialDirectory = string.IsNullOrEmpty(CachePath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                    : CachePath,
            };

            if (dialog.ShowDialog() == true)
            {
                CachePath = dialog.FolderName;
                Settings.CustomCachePath = CachePath;
                SettingsService.Save(Settings);
                Log.Information("User selected cache path: {Path}", CachePath);
            }
        }

        private async Task ExecuteRestore(object? parameter)
        {
            if (!await EnsureSafeToOperate())
            {
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                IsLoading = true;

                UpdateStatus = "Restoring original textures...";
                Log.Information(
                    "Restoring original game textures from 'Originals' backup folder to cache path: {CachePath}",
                    CachePath
                );
                bool restored = await Task.Run(() => SwapService.RestoreFullCache(CachePath));
                if (restored)
                {
                    notificationTitle = "Success";
                    notificationMessage = "Original game textures successfully restored!";
                    notificationAppearance = ControlAppearance.Success;

                    UpdateStatus = "Restoring original textures...";
                    await Task.Delay(2500);
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage =
                        "Failed to restore textures. Ensure you have applied swaps first so that backups exist.";
                    notificationAppearance = ControlAppearance.Danger;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore original textures.");
                notificationTitle = "Error";
                notificationMessage = $"Failed to restore: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                IsLoading = false;
                UpdateStatus = string.Empty;
            }

            if (!string.IsNullOrEmpty(notificationTitle))
            {
                await NotificationService.ShowAsync(
                    notificationTitle,
                    notificationMessage,
                    notificationAppearance
                );
            }
        }

        private async Task ExecuteClearCache(object? parameter)
        {
            if (!await EnsureSafeToOperate())
            {
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                IsLoading = true;
                UpdateStatus = "Clearing ProTanki cache...";
                Log.Information($"Clearing ProTanki cache directory: {CachePath}");

                await Task.Run(() => SwapService.ClearCache(CachePath));

                notificationTitle = "Success";
                notificationMessage =
                    "ProTanki cache successfully cleared! Launch the game to re-cache textures.";
                notificationAppearance = ControlAppearance.Success;

                UpdateStatus = "Clearing ProTanki cache...";
                await Task.Delay(2500);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear cache.");
                notificationTitle = "Error";
                notificationMessage = $"Failed to clear cache: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                IsLoading = false;
                UpdateStatus = string.Empty;
            }

            if (!string.IsNullOrEmpty(notificationTitle))
            {
                await NotificationService.ShowAsync(
                    notificationTitle,
                    notificationMessage,
                    notificationAppearance
                );
            }
        }
    }
}
