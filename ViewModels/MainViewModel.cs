using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Windows.Input;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services;
using TextureSwapper.Services.Interfaces;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SwapService _swapService;
        private readonly UpdateService _updateService;
        private readonly SkinSyncService _skinSyncService;
        private readonly CacheService _cacheService;
        internal readonly INotificationService _notificationService;
        internal readonly SettingsService _settingsService;
        private List<SkinModel> _allSkins = [];

        private string _cachePath = string.Empty;
        private string _selectedCategory = string.Empty;
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private bool _isLoading;
        private string _updateStatus = string.Empty;
        private string _searchQuery = string.Empty;
        private BackupModel? _selectedBackup;

        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
        public ObservableCollection<List<SkinModel>> FilteredSkinsRows { get; } = [];
        public ObservableCollection<string> Categories { get; } = [];
        public ObservableCollection<string> SkinsCategories { get; } = [];
        public ObservableCollection<string> ItemNames { get; } = [];
        public ObservableCollection<BackupModel> SnapshotBackups { get; } = [];
        public ObservableCollection<InGamePaintModel> InGamePaints { get; } = [];

        private List<InGamePaintModel> _allInGamePaints = [];
        private string _inGamePaintSearchQuery = string.Empty;
        public string InGamePaintSearchQuery
        {
            get => _inGamePaintSearchQuery;
            set
            {
                if (SetProperty(ref _inGamePaintSearchQuery, value))
                {
                    FilterInGamePaints();
                }
            }
        }

        private InGamePaintModel? _selectedInGamePaint;
        public InGamePaintModel? SelectedInGamePaint
        {
            get => _selectedInGamePaint;
            set
            {
                if (SetProperty(ref _selectedInGamePaint, value))
                {
                    OnPropertyChanged(nameof(ShowCustomPaints));
                    OnPropertyChanged(nameof(ShowInGamePaints));
                }
            }
        }

        public bool ShowCustomPaints => SelectedInGamePaint != null;
        public bool ShowInGamePaints => SelectedInGamePaint == null;

        public ICommand GoBackToInGamePaintsCommand { get; }

        public AppSettings Settings { get; }

        public BackupModel? SelectedBackup
        {
            get => _selectedBackup;
            set => SetProperty(ref _selectedBackup, value);
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    FilterSkins();
                }
            }
        }

        private string _customPaintSearchQuery = string.Empty;
        public string CustomPaintSearchQuery
        {
            get => _customPaintSearchQuery;
            set
            {
                if (SetProperty(ref _customPaintSearchQuery, value))
                {
                    FilterSkins();
                }
            }
        }

        public string CachePath
        {
            get => _cachePath;
            set => SetProperty(ref _cachePath, value);
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    Settings.LastSelectedCategory = value;
                    _settingsService.Save(Settings);
                    FilterItems();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string SelectedItemName
        {
            get => _selectedItemName;
            set
            {
                if (SetProperty(ref _selectedItemName, value))
                {
                    Settings.LastSelectedItemName = value;
                    _settingsService.Save(Settings);
                    FilterSkins();
                }
            }
        }

        public SkinModel? SelectedSkin
        {
            get => _selectedSkin;
            set => SetProperty(ref _selectedSkin, value);
        }

        private bool _isFoldersPaneVisible = true;
        public bool IsFoldersPaneVisible
        {
            get => _isFoldersPaneVisible;
            set => SetProperty(ref _isFoldersPaneVisible, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        public ICommand BrowseCommand { get; }
        public ICommand SwapCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ClearCacheCommand { get; }
        public ICommand SelectAllAvailableCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand RestoreBackupCommand { get; }
        public ICommand ToggleSkinSelectionCommand { get; }

        public string SelectAllText => _allSkins.Any(s => s.IsSelected) ? "Deselect all textures" : "Select all available textures";
        public string SelectAllIcon => _allSkins.Any(s => s.IsSelected) ? "DismissCircle24" : "CheckmarkCircle24";

        public event Action? RequestSettings;

        public AdminViewModel AdminVM { get; }

        public MainViewModel(INotificationService notificationService, UpdateService updateService)
        {
            _notificationService = notificationService;
            _updateService = updateService;
            _swapService = new SwapService();
            _settingsService = new SettingsService();
            _cacheService = new CacheService();
            _skinSyncService = new SkinSyncService(updateService);
            Settings = _settingsService.Load();
            AdminVM = new AdminViewModel(this, notificationService);

            CachePath = Settings.CustomCachePath ?? _swapService.DetectCachePath();

            BrowseCommand = new RelayCommand(ExecuteBrowse);
            SwapCommand = new AsyncRelayCommand(ExecuteSwap, _ =>
            {
                return !IsLoading && (SelectedCategory == "Paints"
                    ? _allSkins.Any(s => s.IsSelected && s.Category == "Paints")
                    : _allSkins.Any(s => s.IsSelected && s.Category != "Paints"));
            });
            RestoreCommand = new AsyncRelayCommand(ExecuteRestore, _ => !IsLoading);
            ClearCacheCommand = new AsyncRelayCommand(ExecuteClearCache, _ => !IsLoading);
            SelectAllAvailableCommand = new RelayCommand(ExecuteSelectAllAvailable);
            OpenSettingsCommand = new RelayCommand(_ => RequestSettings?.Invoke());
            RestoreBackupCommand = new AsyncRelayCommand(ExecuteRestoreBackup, _ => !IsLoading && SelectedBackup != null);
            ToggleSkinSelectionCommand = new RelayCommand(ExecuteToggleSkinSelection);
            GoBackToInGamePaintsCommand = new RelayCommand(_ => SelectedInGamePaint = null);

            LoadInGamePaints();

            _ = InitializeAsync();
        }

        private void ExecuteToggleSkinSelection(object? parameter)
        {
            if (parameter is SkinModel skin)
            {
                skin.IsSelected = !skin.IsSelected;
                Log.Information("Toggled selection for skin: {SkinName}. New state: {IsSelected}", skin.Name, skin.IsSelected);

                if (skin.IsSelected)
                {
                    foreach (SkinModel otherSkin in _allSkins)
                    {
                        if (otherSkin != skin && otherSkin.Category == skin.Category && otherSkin.ItemName == skin.ItemName && otherSkin.IsSelected)
                        {
                            otherSkin.IsSelected = false;
                            Log.Information("Deselected conflicting skin {OtherSkinName} because {SkinName} was selected.", otherSkin.Name, skin.Name);
                        }
                    }
                }

                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(SelectAllText));
                OnPropertyChanged(nameof(SelectAllIcon));
            }
        }

        private void LoadInGamePaints()
        {
            try
            {
                _allInGamePaints = _skinSyncService.LoadInGamePaints();
                FilterInGamePaints();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load in-game paints.");
            }
        }

        private void FilterInGamePaints()
        {
            InGamePaints.Clear();
            IEnumerable<InGamePaintModel> filtered = _allInGamePaints.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(InGamePaintSearchQuery))
            {
                filtered = filtered.Where(p => p.Name.Contains(InGamePaintSearchQuery, StringComparison.OrdinalIgnoreCase));
            }
            foreach (InGamePaintModel? paint in filtered)
            {
                InGamePaints.Add(paint);
            }
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            UpdateStatus = "Initializing...";

            try
            {
                await Task.Run(() => _swapService.PurgeOldBackups(Settings.MaxBackupRetentionDays));
                LoadBackups();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to purge or load backups.");
            }

            UpdateStatus = "Checking for updates...";

            await LoadSkinsAsync();
            await CheckForAppUpdatesAsync();

            IsLoading = false;
            UpdateStatus = string.Empty;
        }

        private void LoadBackups()
        {
            SnapshotBackups.Clear();
            try
            {
                List<BackupModel> backups = _skinSyncService.LoadBackups();
                foreach (BackupModel backup in backups)
                {
                    SnapshotBackups.Add(backup);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load backups.");
            }
        }

        private async Task ExecuteRestoreBackup(object? parameter)
        {
            if (SelectedBackup == null)
            {
                return;
            }

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
                UpdateStatus = $"Restoring from {SelectedBackup.DisplayName}...";
                Log.Information("Restoring cache snapshot from backup: {BackupDisplayName} (Path: {BackupPath})", SelectedBackup.DisplayName, SelectedBackup.FullPath);
                bool success = await Task.Run(() => _swapService.RestoreFromBackup(CachePath, SelectedBackup.FullPath));
                if (success)
                {
                    notificationTitle = "Success";
                    notificationMessage = $"Restored from {SelectedBackup.DisplayName}";
                    notificationAppearance = ControlAppearance.Success;

                    UpdateStatus = $"Restoring from backup: {SelectedBackup.DisplayName}";
                    await Task.Delay(2500);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore from snapshot.");
                notificationTitle = "Error";
                notificationMessage = $"Restore failed: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                IsLoading = false;
                UpdateStatus = string.Empty;
            }

            if (!string.IsNullOrEmpty(notificationTitle))
            {
                await _notificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }

        public async Task TriggerUpdateCheckAsync()
        {
            IsLoading = true;
            UpdateStatus = "Checking for updates...";
            await CheckForAppUpdatesAsync(p => UpdateStatus = p);
            IsLoading = false;
            UpdateStatus = string.Empty;
        }

        internal async Task PerformUpdateCheckAsync(Action<string> onProgress)
        {
            await CheckForAppUpdatesAsync(onProgress);
        }

        public void SaveTheme(Wpf.Ui.Appearance.ApplicationTheme theme)
        {
            Settings.Theme = theme;
            _settingsService.Save(Settings);
        }

        private async Task CheckForAppUpdatesAsync(Action<string>? onProgress = null)
        {
            onProgress?.Invoke("Checking for updates...");

            GitHubReleaseModel? release = await _updateService.CheckForAppUpdatesAsync();
            if (release != null)
            {
                string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
                string latestVersion = release.TagName.TrimStart('v');

                if (IsNewerVersion(currentVersion, latestVersion))
                {
                    MessageBox messageBox = new()
                    {
                        Title = "Update Available",
                        Content = $"A new version ({release.TagName}) is available. Would you like to download and install it now?",
                        PrimaryButtonText = "Update Now",
                        SecondaryButtonText = "Later",
                        MaxWidth = 450
                    };

                    MessageBoxResult result = await messageBox.ShowDialogAsync();
                    if (result == MessageBoxResult.Primary)
                    {
                        GitHubAssetModel? asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe")) ?? release.Assets.FirstOrDefault();
                        if (asset != null)
                        {
                            onProgress?.Invoke("Downloading update...");
                            await _updateService.DownloadAndRunInstallerAsync(asset.BrowserDownloadUrl, p => onProgress?.Invoke($"Downloading update ({p:F0}%)..."));
                        }
                    }
                }
            }
        }

        private bool IsNewerVersion(string current, string latest)
        {
            return Version.TryParse(current, out Version? vCurrent) && Version.TryParse(latest, out Version? vLatest) && vLatest > vCurrent;
        }

        public async Task ReloadSkinsDataAsync()
        {
            await LoadSkinsAsync();
        }

        private async Task LoadSkinsAsync()
        {
            try
            {
                IsLoading = true;

                void HandleProgressChanged(string status)
                {
                    UpdateStatus = status;
                }

                _skinSyncService.ProgressChanged += HandleProgressChanged;

                (List<SkinModel>? skins, List<InGamePaintModel>? remoteInGamePaints) = await _skinSyncService.SyncAndLoadSkinsAsync();

                _skinSyncService.ProgressChanged -= HandleProgressChanged;

                _allSkins = skins;
                InitializeCategories();
                FilterItems();

                if (remoteInGamePaints != null)
                {
                    LoadInGamePaints();
                }

                int missingPreviewsCount = InGamePaints.Count(p => !string.IsNullOrEmpty(p.PreviewImage) && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p.PreviewImage.Replace("\\", "/"))));
                if (missingPreviewsCount > 0)
                {
                    int completedPreviews = 0;
                    object progressLock = new();
                    using SemaphoreSlim semaphore = new(10);
                    List<Task> downloadTasks = [];

                    foreach (InGamePaintModel paint in InGamePaints)
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
                                HttpResponseMessage res = await _updateService.GetWithRetryAsync(remoteUrl);
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
                await _notificationService.ShowAsync("Error", $"Failed to load skins: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void ExecuteSelectAllAvailable(object? parameter)
        {
            string mode = parameter as string ?? "XT";

            if (mode == "Deselect")
            {
                Log.Information("Deselecting all textures.");
                foreach (SkinModel skin in _allSkins)
                {
                    skin.IsSelected = false;
                }
                _ = _notificationService.ShowAsync("Deselected All", "All textures have been deselected.", ControlAppearance.Info);
            }
            else
            {
                Log.Information("Selecting all {Mode} skins.", mode);

                foreach (SkinModel skin in _allSkins)
                {
                    skin.IsSelected = false;
                }

                var groups = _allSkins.GroupBy(s => new { s.Category, s.ItemName });
                int selectedCount = 0;

                foreach (var group in groups)
                {
                    SkinModel? targetSkin = null;

                    if (mode == "Legacy")
                    {
                        targetSkin = group.FirstOrDefault(s => s.Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase) || s.Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase));
                    }
                    else if (mode == "XT")
                    {
                        targetSkin = group.FirstOrDefault(s => s.Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase));
                    }

                    if (targetSkin != null)
                    {
                        targetSkin.IsSelected = true;
                        selectedCount++;
                    }
                }

                _ = _notificationService.ShowAsync("Selected All", $"Selected {selectedCount} textures ({mode} priority) for batch application.", ControlAppearance.Info);
            }

            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(SelectAllText));
            OnPropertyChanged(nameof(SelectAllIcon));
        }

        private void InitializeCategories()
        {
            if (_allSkins == null || _allSkins.Count == 0)
            {
                return;
            }

            string? currentCategory = SelectedCategory;
            Categories.Clear();
            SkinsCategories.Clear();
            List<string> uniqueCategories = [.. _allSkins.Select(s => s.Category).Distinct().OrderBy(c => c)];
            foreach (string category in uniqueCategories)
            {
                Categories.Add(category);
                if (!category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                {
                    SkinsCategories.Add(category);
                }
            }

            if (Categories.Any())
            {
                string targetCategory = !string.IsNullOrEmpty(currentCategory) && Categories.Contains(currentCategory)
                    ? currentCategory
                    : (Settings.LastSelectedCategory != null && Categories.Contains(Settings.LastSelectedCategory))
                        ? Settings.LastSelectedCategory
                        : Categories.First();

                if (targetCategory.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                {
                    targetCategory = SkinsCategories.FirstOrDefault() ?? Categories.First();
                }

                SelectedCategory = targetCategory;
            }
        }

        private void FilterItems()
        {
            ItemNames.Clear();
            string allLabel = SelectedCategory == "Paints" ? "All" : "All models";
            ItemNames.Add(allLabel);

            List<string> matchingItems = [.. _allSkins
                .Where(s => s.Category == SelectedCategory)
                .Select(s => s.ItemName)
                .Distinct()
                .OrderBy(i => i)];

            foreach (string itemName in matchingItems)
            {
                ItemNames.Add(itemName);
            }

            if (ItemNames.Any())
            {
                SelectedItemName = (Settings.LastSelectedItemName != null && ItemNames.Contains(Settings.LastSelectedItemName))
                    ? Settings.LastSelectedItemName
                    : allLabel;
            }
        }

        private void FilterSkins()
        {
            FilteredSkins.Clear();
            string currentQuery = SelectedCategory == "Paints" ? CustomPaintSearchQuery : SearchQuery;
            string allLabel = SelectedCategory == "Paints" ? "All" : "All models";

            IEnumerable<SkinModel> matchingSkins = _allSkins.Where(s =>
                s.Category == SelectedCategory &&
                (SelectedItemName == allLabel || s.ItemName == SelectedItemName) &&
                (string.IsNullOrWhiteSpace(currentQuery) ||
                 s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase) ||
                 s.ItemName.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)));

            foreach (SkinModel skin in matchingSkins)
            {
                FilteredSkins.Add(skin);
            }

            if (FilteredSkins.Any())
            {
                SelectedSkin = FilteredSkins.First();
            }

            UpdateFilteredSkinsRows();
        }

        private int _currentColumns = 4;

        public void UpdateColumns(int cols)
        {
            if (_currentColumns != cols)
            {
                _currentColumns = cols;
                UpdateFilteredSkinsRows();
            }
        }

        private void UpdateFilteredSkinsRows()
        {
            FilteredSkinsRows.Clear();
            List<SkinModel>? currentChunk = null;
            int chunkSize = _currentColumns;
            for (int i = 0; i < FilteredSkins.Count; i++)
            {
                if (i % chunkSize == 0)
                {
                    currentChunk = [];
                    FilteredSkinsRows.Add(currentChunk);
                }
                currentChunk?.Add(FilteredSkins[i]);
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
                _settingsService.Save(Settings);
                Log.Information("User selected cache path: {Path}", CachePath);
            }
        }

        private async Task<bool> EnsureSafeToOperate()
        {
            if (_cacheService.IsGameRunning())
            {
                await _notificationService.ShowAsync("Game Running", "Please close ProTanki and the Loader before proceeding.", ControlAppearance.Info);
                return false;
            }

            if (_cacheService.IsCacheFileLocked(CachePath))
            {
                await _notificationService.ShowAsync("Cache Locked", "Some cache files are still in use by another process. Please wait or close other apps.", ControlAppearance.Info);
                return false;
            }

            return true;
        }

        private async Task ExecuteSwap(object? parameter)
        {
            if (!await EnsureSafeToOperate())
            {
                return;
            }

            List<SkinModel> selectedSkins = [.. _allSkins.Where(s => s.IsSelected)];

            if (selectedSkins.Count == 0 && SelectedSkin == null)
            {
                await _notificationService.ShowAsync("Error", "Please select at least one skin first.", ControlAppearance.Danger);
                return;
            }
            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                IsLoading = true;

                List<SkinModel> skinsToApply = [];
                if (selectedSkins.Count != 0)
                {
                    skinsToApply.AddRange(selectedSkins);
                }
                else if (SelectedSkin != null)
                {
                    skinsToApply.Add(SelectedSkin);
                }

                if (SelectedCategory == "Paints")
                {
                    if (SelectedInGamePaint == null)
                    {
                        await _notificationService.ShowAsync("Error", "Please select an in-game paint first.", ControlAppearance.Danger);
                        IsLoading = false;
                        return;
                    }
                    foreach (SkinModel skin in skinsToApply)
                    {
                        skin.DetailsTarget = SelectedInGamePaint.TargetUrl;
                    }
                }

                string skinMessage = skinsToApply.Count > 1 ? "skins" : "skin";
                string textureMessage = skinsToApply.Count > 1 ? "textures" : "texture";


                UpdateStatus = $"Applying {skinsToApply.Count} {textureMessage} to cache...";
                Log.Information("Writing {Count} skins directly to cache disk path: {Path}", skinsToApply.Count, CachePath);

                string? inGamePaintName = SelectedCategory == "Paints" ? SelectedInGamePaint?.Name : null;
                string? error = await Task.Run(() => _swapService.SwapBatch(CachePath, skinsToApply, inGamePaintName));
                if (error != null)
                {
                    notificationTitle = "Error";
                    notificationMessage = error;
                    notificationAppearance = ControlAppearance.Danger;
                }
                else
                {
                    LoadBackups();
                    notificationTitle = "Success";
                    notificationMessage = $"{skinsToApply.Count} {skinMessage} applied successfully!";
                    notificationAppearance = ControlAppearance.Success;

                    foreach (SkinModel skin in _allSkins)
                    {
                        skin.IsSelected = false;
                    }
                }

                if (!string.IsNullOrEmpty(notificationTitle) && notificationAppearance == ControlAppearance.Success)
                {
                    UpdateStatus = $"Finalizing applying {skinsToApply.Count} {skinMessage}...";
                    await Task.Delay(1500);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply skins.");
                notificationTitle = "Error";
                notificationMessage = $"Failed to apply skins: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                IsLoading = false;
                UpdateStatus = string.Empty;
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(SelectAllText));
                OnPropertyChanged(nameof(SelectAllIcon));
            }

            if (!string.IsNullOrEmpty(notificationTitle))
            {
                await _notificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
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
                bool restored = await Task.Run(() => _swapService.RestoreFullCache(CachePath));
                if (restored)
                {
                    notificationTitle = "Restored";
                    notificationMessage = "Original textures restored successfully.";
                    notificationAppearance = ControlAppearance.Success;
                }
                else
                {
                    notificationTitle = "No Backup";
                    notificationMessage = "No original textures found to restore. Apply a skin first to create a backup.";
                    notificationAppearance = ControlAppearance.Info;
                }



            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore textures.");
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
                await _notificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }

        private async Task ExecuteClearCache(object? parameter)
        {
            if (!await EnsureSafeToOperate())
            {
                return;
            }

            if (string.IsNullOrEmpty(CachePath) || !Directory.Exists(CachePath))
            {
                await _notificationService.ShowAsync("Error", "Invalid cache path.", ControlAppearance.Danger);
                return;
            }

            MessageBox messageBox = new()
            {
                Title = "Confirm Clear Cache",
                Content = "Are you sure you want to clear the ProTanki cache? The game will need to re-download all assets.",
                PrimaryButtonText = "Clear Cache",
                SecondaryButtonText = "Cancel",
                CloseButtonText = string.Empty,
                IsCloseButtonEnabled = false,
                MaxWidth = 400
            };

            MessageBoxResult result = await messageBox.ShowDialogAsync();
            if (result != Wpf.Ui.Controls.MessageBoxResult.Primary)
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
                Log.Information("Clearing all cache files at path: {CachePath}", CachePath);
                await Task.Run(() => _swapService.ClearCache(CachePath));
                notificationTitle = "Cache Cleared";
                notificationMessage = "ProTanki cache has been emptied.";
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
                await _notificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }
    }
}
