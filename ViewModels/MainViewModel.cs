using Serilog;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;
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
        public ISkinSyncService SkinSyncService { get; }
        public ISettingsService SettingsService { get; }
        public IWindowService WindowService { get; }
        public INotificationService NotificationService { get; }
        public IAiTextureService AiTextureService { get; }

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
            IWindowService windowService,
            IAiTextureService aiTextureService)
        {
            NotificationService = notificationService;
            SettingsService = settingsService;
            SwapService = swapService;
            SkinSyncService = skinSyncService;
            UpdateService = updateService;
            WindowService = windowService;
            AiTextureService = aiTextureService;

            Settings = SettingsService.Load();

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

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            UpdateStatus = "Initializing...";

            try
            {
                try
                {
                    await Task.Run(() => SwapService.PurgeOldBackups(Settings.MaxBackupRetentionDays));
                    BackupsTabVM.LoadBackups();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to purge or load backups.");
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

                (List<SkinModel>? skins, List<InGamePaintModel>? remoteInGamePaints) = await SkinSyncService.SyncAndLoadSkinsAsync();

                SkinSyncService.ProgressChanged -= HandleProgressChanged;

                if (UpdateService.IsOffline)
                {
                    await NotificationService.ShowAsync("Offline Mode", "Running in offline mode. Local databases and cached textures will be used.", ControlAppearance.Info);
                }

                AllSkins = skins ?? [];
                SkinsTabVM.OnSkinsLoaded();
                PaintsTabVM.OnSkinsLoaded();

                if (remoteInGamePaints != null)
                {
                    await PaintsTabVM.LoadInGamePaintsAsync();
                }

                int missingPreviewsCount = PaintsTabVM.InGamePaints.Count(p => !string.IsNullOrEmpty(p.PreviewImage) && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p.PreviewImage.Replace("\\", "/"))));
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

                        string localPreview = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, paint.PreviewImage.Replace("\\", "/"));
                        if (File.Exists(localPreview))
                        {
                            continue;
                        }

                        string remoteUrl = $"{Constants.GitHubRawUrl}/{paint.PreviewImage.Replace("\\", "/")}";
                        downloadTasks.Add(Task.Run(async () =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                lock (progressLock)
                                {
                                    UpdateStatus = $"Syncing in-game paints ({completedPreviews + 1}/{missingPreviewsCount}): {paint.Name}...";
                                }

                                Log.Information("Downloading missing in-game paint preview: {Path}", paint.PreviewImage);
                                _ = Directory.CreateDirectory(Path.GetDirectoryName(localPreview)!);
                                using HttpResponseMessage res = await UpdateService.GetWithRetryAsync(remoteUrl);
                                byte[] data = await res.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(localPreview, data);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning("Failed to download in-game paint preview {Name}: {Message}", paint.Name, ex.Message);
                            }
                            finally
                            {
                                int currentCompleted = Interlocked.Increment(ref completedPreviews);
                                lock (progressLock)
                                {
                                    UpdateStatus = $"Syncing in-game paints ({currentCompleted}/{missingPreviewsCount})...";
                                }
                                _ = semaphore.Release();
                            }
                        }));
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
                await NotificationService.ShowAsync("Error", $"Failed to load skins: {ex.Message}", ControlAppearance.Danger);
            }
        }

        public async Task<bool> EnsureSafeToOperate()
        {
            if (string.IsNullOrWhiteSpace(CachePath) || !Directory.Exists(CachePath))
            {
                await NotificationService.ShowAsync("Error", "ProTanki cache folder path is not set or invalid. Please configure it in Settings.", ControlAppearance.Danger);
                return false;
            }

            if (IsGameRunning())
            {
                MessageBox messageBox = new()
                {
                    Title = "Game is Running",
                    Content = "ProTanki is currently running. Please close the game before swapping textures to avoid cache file access locks.",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = string.Empty
                };
                _ = await messageBox.ShowDialogAsync();
                return false;
            }

            return true;
        }

        private bool IsGameRunning()
        {
            try
            {
                Process[] processes = System.Diagnostics.Process.GetProcessesByName("ProTanki");
                if (processes.Length > 0)
                {
                    return true;
                }
                Process[] standalone = System.Diagnostics.Process.GetProcessesByName("ProTanki Standalone");
                return standalone.Length > 0;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to verify running ProTanki processes.");
                return false;
            }
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

        public async Task ShowNotificationAsync(string title, string message, ControlAppearance appearance)
        {
            await NotificationService.ShowAsync(title, message, appearance);
        }

        private async Task<bool> CheckForAppUpdatesAsync(Action<string>? onProgress = null)
        {
            onProgress?.Invoke("Checking for updates...");

            GitHubReleaseModel? release = await UpdateService.CheckForAppUpdatesAsync();
            if (release != null)
            {
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "Unknown";
                string latestVersion = release.TagName.TrimStart('v');

                Log.Information($"Current local version = {currentVersion}, Latest GitHub version = {latestVersion}");

                if (IsNewerVersion(currentVersion, latestVersion))
                {
                    onProgress?.Invoke($"Update available: {release.TagName}");

                    string updateContent = $"A new version ({release.TagName}) is available. Download and install it now?";
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
                        BorderThickness = new Thickness(0)
                    };

                    MessageBox messageBox = new()
                    {
                        Title = "Update Available",
                        Content = docViewer,
                        PrimaryButtonText = "Update Now",
                        SecondaryButtonText = "Later",
                        MaxWidth = 580
                    };

                    MessageBoxResult result = await messageBox.ShowDialogAsync();
                    if (result == MessageBoxResult.Primary)
                    {
                        onProgress?.Invoke("Downloading update...");
                        try
                        {
                            GitHubAssetModel exeAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("No installer executable (.exe) found in release assets.");
                            void HandleProgress(double p)
                            {
                                onProgress?.Invoke($"Downloading update ({p:F0}%)...");
                            }

                            await UpdateService.DownloadAndRunInstallerAsync(exeAsset.BrowserDownloadUrl, HandleProgress);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to download update installer.");
                            await NotificationService.ShowAsync("Update Error", $"Failed to download update installer: {ex.Message}", ControlAppearance.Danger);
                        }
                    }
                    return true;
                }
            }
            onProgress?.Invoke("App is up-to-date.");
            return false;
        }

        private bool IsNewerVersion(string currentVersion, string latestVersion)
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
                InitialDirectory = string.IsNullOrEmpty(CachePath) ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : CachePath
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
                Log.Information("Restoring original game textures from 'Originals' backup folder to cache path: {CachePath}", CachePath);
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
                    notificationMessage = "Failed to restore textures. Ensure you have applied swaps first so that backups exist.";
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
                await NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
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
                notificationMessage = "ProTanki cache successfully cleared! Launch the game to re-cache textures.";
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
                await NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }
    }
}
