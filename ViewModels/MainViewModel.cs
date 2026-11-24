using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
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
using TextureSwapper.Seasonal;
using TextureSwapper.ViewModels.Base;
using TextureSwapper.ViewModels.Tabs;
using TextureSwapper.Views.Windows;
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
        public IHtml5CacheService Html5CacheService { get; }
        public IBntArchiveService BntArchiveService { get; }
        public IEquipmentTargetService EquipmentTargetService { get; }
        public SeasonalThemeService SeasonalThemeService { get; }
        public SeasonalAudioService SeasonalAudioService { get; }

        public event EventHandler<ClientType>? ActiveClientChanged;

        public bool IsHtml5Client => Settings.ActiveClient == ClientType.Html5;
        public bool IsFlashClient => Settings.ActiveClient == ClientType.Flash;
        public string ActiveClientDisplayName => IsHtml5Client ? "HTML5 Client" : "Flash Client";

        public bool IsSeasonalThemeActive => SeasonalThemeService.IsSeasonalActive;
        public bool IsSeasonalAtmosphereVisible =>
            IsSeasonalThemeActive && Settings.EnableSeasonalAtmosphereAnimation;
        public string AppLogoSource =>
            (IsSeasonalThemeActive && Settings.EnableSeasonalAppLogo)
                ? SeasonalThemeConfig.Current.AppLogoSource
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
            IsGlobalAuroraThemeVisible
                ? "❄️ ProTanki Texture Swapper"
                : (
                    (IsSeasonalThemeActive && Settings.EnableSeasonalAppLogo)
                        ? SeasonalThemeConfig.Current.AppTitle
                        : "ProTanki Texture Swapper"
                );

        public void NotifySeasonalVisualsChanged()
        {
            OnPropertyChanged(nameof(IsSeasonalAtmosphereVisible));
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
        public ChatTabViewModel ChatTabVM { get; }
        public IChatService ChatService { get; }
        public AdminViewModel AdminVM { get; }
        public AppSettings Settings { get; }
        public List<SkinModel> AllSkins { get; set; } = [];

        private bool _isChristmasChatVisible;
        public bool IsChristmasChatVisible
        {
            get => _isChristmasChatVisible;
            private set => _ = SetProperty(ref _isChristmasChatVisible, value);
        }

        public bool IsGlobalAuroraThemeVisible => ChatTabVM.IsSpecialThemeActive;

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
            IHtml5CacheService html5CacheService,
            IBntArchiveService bntArchiveService,
            SeasonalThemeService seasonalThemeService,
            SeasonalAudioService seasonalAudioService,
            IChatService chatService,
            ChatTabViewModel chatTabViewModel,
            IEquipmentTargetService? equipmentTargetService = null
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
            Html5CacheService = html5CacheService;
            BntArchiveService = bntArchiveService;
            EquipmentTargetService =
                equipmentTargetService ?? new Services.EquipmentTargetService();
            SeasonalThemeService = seasonalThemeService;
            SeasonalAudioService = seasonalAudioService;
            ChatService = chatService;
            ChatTabVM = chatTabViewModel;
            ChatTabVM.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ChatTabViewModel.IsSpecialThemeActive))
                {
                    OnPropertyChanged(nameof(IsGlobalAuroraThemeVisible));
                    OnPropertyChanged(nameof(AppTitleText));
                }
            };
            _ = CheckChristmasChatAccessAsync();
            SeasonalThemeService.SeasonalThemeChanged += (_, active) =>
            {
                if (active)
                {
                    SeasonalAudioService.PlaySpecialCue();
                    SeasonalAudioService.SetAmbientDrone(true);
                }
                else
                {
                    SeasonalAudioService.SetAmbientDrone(false);
                }
            };
            SeasonalThemeService.SeasonalThemeChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(IsSeasonalThemeActive));
                OnPropertyChanged(nameof(IsSeasonalAtmosphereVisible));
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
            SeasonalThemeService.CheckAndApply(notify: false);

            SkinsTabVM = new SkinsTabViewModel(this);
            PaintsTabVM = new PaintsTabViewModel(this);
            ShotEffectsTabVM = new ShotEffectsTabViewModel(this);
            BackupsTabVM = new BackupsTabViewModel(this);
            AdminVM = new AdminViewModel(this, notificationService);

            CachePath = ResolveActiveCachePath();

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
                    RefreshUnlockedChristmasSkins();
                });
            };

            PremiumService.UnlockedChristmasSkinsChanged += (_, _) =>
            {
                Application.Current?.Dispatcher.Invoke(RefreshUnlockedChristmasSkins);
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
                    bool shouldDownload = true;
                    string initialPreference = Settings?.PreferredTextureMirror ?? "Cloudflare";

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        TextureSourceSelectionDialog dialog = new(initialPreference);
                        if (
                            Application.Current?.MainWindow != null
                            && Application.Current.MainWindow.IsVisible
                        )
                        {
                            dialog.Owner = Application.Current.MainWindow;
                        }

                        bool? dialogResult = dialog.ShowDialog();
                        if (dialogResult == true)
                        {
                            if (Settings != null)
                            {
                                Settings.PreferredTextureMirror = dialog.SelectedMirror;
                                SettingsService.Save(Settings);
                            }
                            shouldDownload = true;
                        }
                        else
                        {
                            shouldDownload = false;
                        }
                    });

                    if (shouldDownload)
                    {
                        UpdateStatus = "Connecting to download server...";
                        bool downloaded =
                            await TextureDownloadService.DownloadAndExtractTexturesAsync(
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
                    else
                    {
                        Log.Information(
                            "User skipped initial texture pack download. Running in offline mode."
                        );
                        _ = NotificationService.ShowAsync(
                            "Texture Pack",
                            "Texture download was skipped. Texture swapping will be unavailable until textures are downloaded.",
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

                await SyncHtml5ScriptsAsync();

                if (UpdateService.IsOffline)
                {
                    await NotificationService.ShowAsync(
                        "Offline Mode",
                        "Running in offline mode. Local databases and cached textures will be used.",
                        ControlAppearance.Info
                    );
                }

                AllSkins = skins ?? [];
                RefreshUnlockedChristmasSkins();
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

        private int _deferredPackSyncActive;

        private async Task SyncHtml5ScriptsAsync()
        {
            try
            {
                bool scriptFileUpdated = false;
                if (!UpdateService.IsOffline)
                {
                    string[] scriptPaths = ["Scripts/protanki_pack.js"];

                    foreach (string relativePath in scriptPaths)
                    {
                        string? remoteContent = await UpdateService.FetchRemoteTextFileAsync(
                            relativePath
                        );
                        if (string.IsNullOrWhiteSpace(remoteContent))
                        {
                            continue;
                        }

                        string localPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            relativePath.Replace('/', Path.DirectorySeparatorChar)
                        );
                        string? existing = File.Exists(localPath)
                            ? await File.ReadAllTextAsync(localPath, Encoding.UTF8)
                            : null;

                        if (!string.Equals(existing, remoteContent, StringComparison.Ordinal))
                        {
                            string? dir = Path.GetDirectoryName(localPath);
                            if (!string.IsNullOrEmpty(dir))
                            {
                                _ = Directory.CreateDirectory(dir);
                            }

                            await File.WriteAllTextAsync(localPath, remoteContent, Encoding.UTF8);
                            scriptFileUpdated = true;
                            Log.Information(
                                "Updated local HTML5 script from remote: {Path}",
                                relativePath
                            );
                        }
                    }
                }

                if (
                    IsHtml5Client
                    && !string.IsNullOrWhiteSpace(CachePath)
                    && Directory.Exists(CachePath)
                )
                {
                    if (!CacheService.IsGameRunning())
                    {
                        await Task.Run(() => ApplyHtml5ScriptsToCache(CachePath));
                    }
                    else
                    {
                        string packHotkey = string.IsNullOrWhiteSpace(Settings.ProTankiPackHotkey)
                            ? "F8"
                            : Settings.ProTankiPackHotkey;
                        bool updatePending = await Task.Run(() =>
                            scriptFileUpdated
                            || Html5CacheService.IsShadersUpdatePending(CachePath, packHotkey)
                        );
                        if (updatePending)
                        {
                            ScheduleDeferredPackSync(CachePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to sync HTML5 scripts.");
            }
        }

        private void ApplyHtml5ScriptsToCache(string cachePath)
        {
            (bool detectedShaders, bool detectedSkinAnim) =
                Html5CacheService.GetAppliedScriptStatus(cachePath);
            bool shadersEnabled = Settings.EnableProTankiPack || detectedShaders;
            bool skinAnimEnabled = Settings.EnableSkinAnimations || detectedSkinAnim;
            string shaderHotkey = string.IsNullOrWhiteSpace(Settings.ProTankiPackHotkey)
                ? "F8"
                : Settings.ProTankiPackHotkey;
            string skinAnimHotkey = string.IsNullOrWhiteSpace(Settings.SkinAnimMenuHotkey)
                ? "F9"
                : Settings.SkinAnimMenuHotkey;

            Html5CacheService.SyncAppliedScripts(
                cachePath,
                shadersEnabled,
                shaderHotkey,
                skinAnimEnabled,
                skinAnimHotkey
            );
        }

        private void ScheduleDeferredPackSync(string cachePath)
        {
            const string updateMsg =
                "ProTanki Pack update is ready. Please restart ProTanki to apply the changes.";
            Services.PackLogListener.PendingUpdateMessage = updateMsg;

            Services.PackLogListener? listener =
                App.ServiceProvider?.GetService(typeof(Services.PackLogListener))
                as Services.PackLogListener;
            bool startedTemporaryListener = false;
            if (listener != null && !listener.IsRunning)
            {
                listener.Start();
                startedTemporaryListener = listener.IsRunning;
            }

            _ = NotificationService.ShowAsync(
                "ProTanki Pack Update Ready",
                "An updated ProTanki Pack was downloaded while ProTanki is running. It will be applied automatically as soon as ProTanki closes.",
                ControlAppearance.Info
            );

            if (Interlocked.CompareExchange(ref _deferredPackSyncActive, 1, 0) != 0)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    while (CacheService.IsGameRunning())
                    {
                        await Task.Delay(3000).ConfigureAwait(false);
                    }

                    Services.PackLogListener.PendingUpdateMessage = null;
                    if (
                        startedTemporaryListener
                        && !Settings.EnablePackLogListener
                        && !Settings.EnableProTankiPack
                    )
                    {
                        listener?.Stop();
                    }

                    if (Directory.Exists(cachePath))
                    {
                        ApplyHtml5ScriptsToCache(cachePath);
                        Log.Information("Deferred ProTanki Pack sync completed after game exit.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Deferred ProTanki Pack sync failed.");
                }
                finally
                {
                    _ = Interlocked.Exchange(ref _deferredPackSyncActive, 0);
                }
            });
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
                string currentVersion = FormatVersion(
                    Assembly.GetExecutingAssembly().GetName().Version
                );
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

        public static string FormatVersion(Version? version)
        {
            return version == null ? "3.0"
                : version.Build > 0 ? $"{version.Major}.{version.Minor}.{version.Build}"
                : $"{version.Major}.{version.Minor}";
        }

        internal static bool IsNewerVersion(string currentVersion, string latestVersion)
        {
            try
            {
                Version current = NormalizeVersion(new Version(currentVersion));
                Version latest = NormalizeVersion(new Version(latestVersion));
                return latest > current;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"Failed to parse versions: {currentVersion} vs {latestVersion}");
                return false;
            }
        }

        private static Version NormalizeVersion(Version v)
        {
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build), Math.Max(0, v.Revision));
        }

        public string ResolveActiveCachePath()
        {
            return Settings.ActiveClient == ClientType.Html5
                ? !string.IsNullOrWhiteSpace(Settings.CustomHtml5CachePath)
                    ? Settings.CustomHtml5CachePath
                    : SwapService.DetectCachePath(ClientType.Html5)
                : !string.IsNullOrWhiteSpace(Settings.CustomCachePath)
                    ? Settings.CustomCachePath
                    : SwapService.DetectCachePath(ClientType.Flash);
        }

        public void SwitchClient(ClientType newClient)
        {
            if (Settings.ActiveClient == newClient)
            {
                return;
            }

            Settings.ActiveClient = newClient;
            SettingsService.Save(Settings);
            Html5CacheService.InvalidateCache();
            CachePath = ResolveActiveCachePath();
            BackupsTabVM.LoadBackups();

            OnPropertyChanged(nameof(IsHtml5Client));
            OnPropertyChanged(nameof(IsFlashClient));
            OnPropertyChanged(nameof(ActiveClientDisplayName));
            ActiveClientChanged?.Invoke(this, newClient);

            Log.Information(
                "Switched active ProTanki client to {Client}. CachePath: {CachePath}",
                newClient,
                CachePath
            );
        }

        private void ExecuteBrowse(object? parameter)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new()
            {
                Title = IsHtml5Client
                    ? "Select ProTanki HTML5 Cache Folder"
                    : "Select ProTanki Flash Cache Folder",
                InitialDirectory = string.IsNullOrEmpty(CachePath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                    : CachePath,
            };

            if (dialog.ShowDialog() == true)
            {
                CachePath = dialog.FolderName;
                if (IsHtml5Client)
                {
                    Settings.CustomHtml5CachePath = CachePath;
                }
                else
                {
                    Settings.CustomCachePath = CachePath;
                }
                SettingsService.Save(Settings);
                Html5CacheService.InvalidateCache();
                Log.Information(
                    "User selected custom {Client} cache path: {Path}",
                    Settings.ActiveClient,
                    CachePath
                );
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

            string title = LocalizationService.GetString("Loc_ClearCache_ConfirmTitle");
            if (string.IsNullOrWhiteSpace(title) || title == "Loc_ClearCache_ConfirmTitle")
            {
                title = "Clear Cache";
            }

            string message = LocalizationService.GetString("Loc_ClearCache_ConfirmMessage");
            if (string.IsNullOrWhiteSpace(message) || message == "Loc_ClearCache_ConfirmMessage")
            {
                message =
                    "Are you sure you want to clear the ProTanki cache? All cached game textures will be removed.";
            }

            string yesText = LocalizationService.GetString("Loc_Common_Yes");
            if (string.IsNullOrWhiteSpace(yesText) || yesText == "Loc_Common_Yes")
            {
                yesText = "Yes";
            }

            string noText = LocalizationService.GetString("Loc_Common_No");
            if (string.IsNullOrWhiteSpace(noText) || noText == "Loc_Common_No")
            {
                noText = "No";
            }

            MessageBox messageBox = new()
            {
                Title = title,
                Content = message,
                PrimaryButtonText = yesText,
                CloseButtonText = noText,
                Owner = Application.Current?.MainWindow,
            };

            if (await messageBox.ShowDialogAsync() != MessageBoxResult.Primary)
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

        public void RefreshUnlockedChristmasSkins()
        {
            IReadOnlyList<string> unlocked = PremiumService.UnlockedChristmasSkinIds;
            bool isFullPremium = PremiumService.IsPremiumUnlocked;

            foreach (SkinModel skin in AllSkins)
            {
                skin.IsEventUnlocked =
                    skin.IsPremium
                    && !isFullPremium
                    && unlocked.Contains(skin.Name, StringComparer.OrdinalIgnoreCase);
            }

            SkinsTabVM.RefreshGiftedSkinsState();
            ChatTabVM.RefreshGiftedSkins();
        }

        private async Task CheckChristmasChatAccessAsync()
        {
            bool allowed = await ChatService.CheckAccessAsync();
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsChristmasChatVisible = allowed;
                ChatTabVM.RefreshSpecialThemeAccess();
                OnPropertyChanged(nameof(IsGlobalAuroraThemeVisible));
                OnPropertyChanged(nameof(AppTitleText));
            });
        }
    }
}
