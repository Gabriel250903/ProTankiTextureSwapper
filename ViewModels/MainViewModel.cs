using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows.Input;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SwapService _swapService;
        private readonly UpdateService _updateService;
        internal readonly INotificationService _notificationService;
        internal readonly SettingsService _settingsService;
        private List<SkinModel> _allSkins = [];

        private string _cachePath = string.Empty;
        private string _selectedCategory = string.Empty;
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private bool _isLoading;
        private string _updateStatus = string.Empty;
        private readonly string _searchQuery = string.Empty;
        private BackupModel? _selectedBackup;

        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
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
            get => Settings.LastSearchQuery ?? string.Empty;
            set
            {
                Settings.LastSearchQuery = value;
                _settingsService.Save(Settings);
                OnPropertyChanged();
                FilterSkins();
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

        public MainViewModel(INotificationService notificationService, UpdateService updateService)
        {
            _notificationService = notificationService;
            _updateService = updateService;
            _swapService = new SwapService();
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            CachePath = Settings.CustomCachePath ?? _swapService.DetectCachePath();

            BrowseCommand = new RelayCommand(ExecuteBrowse);
            SwapCommand = new AsyncRelayCommand(ExecuteSwap, _ =>
            {
                if (IsLoading) return false;
                if (SelectedCategory == "Paints")
                {
                    return _allSkins.Any(s => s.IsSelected && s.Category == "Paints");
                }
                return _allSkins.Any(s => s.IsSelected && s.Category != "Paints");
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
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ingame_paints.json");
                List<InGamePaintModel> list = [];

                if (File.Exists(jsonPath))
                {
                    try
                    {
                        string json = File.ReadAllText(jsonPath);
                        list = JsonSerializer.Deserialize<List<InGamePaintModel>>(json) ?? [];
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to deserialize ingame_paints.json");
                    }
                }

                // Scan directory Textures/Paints/InGame for any new paint images
                string inGameDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures", "Paints", "InGame");
                if (Directory.Exists(inGameDir))
                {
                    string[] files = Directory.GetFiles(inGameDir, "*.*")
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    bool modified = false;
                    foreach (string file in files)
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if (!list.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                        {
                            string relativePreview = Path.Combine("Textures", "Paints", "InGame", Path.GetFileName(file)).Replace("\\", "/");
                            string targetUrl = "";
                            if (name.Equals("Tiger", StringComparison.OrdinalIgnoreCase))
                                targetUrl = "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8xNi8yMzIvMzE2NDQ3NTQzMTM0NDMvaW1hZ2UuanBn";
                            else if (name.Equals("Irbis", StringComparison.OrdinalIgnoreCase))
                                targetUrl = "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8xNC8yNTYvMzE2NDQ3NTQzMTAwMzIvaW1hZ2UuanBn";

                            list.Add(new InGamePaintModel
                            {
                                Name = name,
                                PreviewImage = relativePreview,
                                TargetUrl = targetUrl
                            });
                            modified = true;
                        }
                    }

                    if (modified || !File.Exists(jsonPath))
                    {
                        try
                        {
                            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                            File.WriteAllText(jsonPath, json);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to write ingame_paints.json");
                        }
                    }
                }

                _allInGamePaints = list;
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
            var filtered = _allInGamePaints.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(InGamePaintSearchQuery))
            {
                filtered = filtered.Where(p => p.Name.Contains(InGamePaintSearchQuery, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var paint in filtered)
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
            string backupsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir);
            if (!Directory.Exists(backupsRoot))
            {
                return;
            }

            IOrderedEnumerable<DirectoryInfo> dirs = Directory.GetDirectories(backupsRoot)
                .Select(d => new DirectoryInfo(d))
                .Where(di => !di.Name.Equals(Constants.OriginalsDir, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(di => di.CreationTime);

            foreach (DirectoryInfo? di in dirs)
            {
                SnapshotBackups.Add(new BackupModel
                {
                    FolderName = di.Name,
                    DisplayName = di.Name.Replace("_", " "),
                    CreationDate = di.CreationTime,
                    FullPath = di.FullName
                });
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
                    if (result == Wpf.Ui.Controls.MessageBoxResult.Primary)
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

        private async Task LoadSkinsAsync()
        {
            try
            {
                IsLoading = true;

                string localHullsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.HullsSkinsJson);
                string localTurretsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.TurretsSkinsJson);
                string localSuppliesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.SuppliesSkinsJson);
                string localPaintsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.PaintsSkinsJson);

                var categories = new[]
                {
                    (Name: "Hulls", LocalPath: localHullsPath, RemoteFileName: Constants.HullsSkinsJson),
                    (Name: "Turrets", LocalPath: localTurretsPath, RemoteFileName: Constants.TurretsSkinsJson),
                    (Name: "Supplies", LocalPath: localSuppliesPath, RemoteFileName: Constants.SuppliesSkinsJson),
                    (Name: "Paints", LocalPath: localPaintsPath, RemoteFileName: Constants.PaintsSkinsJson)
                };

                List<SkinModel> combinedSkins = [];
                bool anyAssetsSynced = false;

                foreach (var cat in categories)
                {
                    // 1. Load local copy if exists
                    string localJson = File.Exists(cat.LocalPath) ? await File.ReadAllTextAsync(cat.LocalPath) : string.Empty;
                    List<SkinModel> catSkins = [];
                    if (!string.IsNullOrEmpty(localJson))
                    {
                        try
                        {
                            catSkins = JsonSerializer.Deserialize<List<SkinModel>>(localJson) ?? [];
                        }
                        catch (JsonException ex)
                        {
                            Log.Error(ex, $"Failed to deserialize local {cat.Name} skins.");
                        }
                    }

                    // 2. Fetch remote DB
                    UpdateStatus = $"Syncing {cat.Name.ToLower()} database...";
                    (List<SkinModel>? remoteSkins, string? remoteJson) = await _updateService.FetchRemoteSkinsFileAsync(cat.RemoteFileName);

                    if (remoteSkins != null && remoteJson != localJson)
                    {
                        Log.Information($"Remote {cat.RemoteFileName} is different from local. Updating...");
                        await File.WriteAllTextAsync(cat.LocalPath, remoteJson!);
                        catSkins = remoteSkins;
                    }

                    // 3. Check and sync missing assets for this category
                    List<SkinModel> missing = catSkins.Where(IsSkinMissingAssets).ToList();
                    if (missing.Count > 0)
                    {
                        anyAssetsSynced = true;
                        int completed = 0;
                        foreach (SkinModel skin in missing)
                        {
                            UpdateStatus = $"Syncing {cat.Name.ToLower()} ({completed + 1}/{missing.Count}): {skin.Name}...";
                            await _updateService.EnsureAssetsExistAsync(skin, p =>
                            {
                                UpdateStatus = $"Syncing {cat.Name.ToLower()} ({completed + 1}/{missing.Count}): {skin.Name} - {p}";
                            });
                            skin.NotifyPreviewChanged();
                            completed++;
                        }
                    }

                    combinedSkins.AddRange(catSkins);
                }

                _allSkins = combinedSkins;
                InitializeCategories();
                FilterItems();

                // Now sync ingame_paints.json
                try
                {
                    string localInGamePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.InGamePaintsJson);
                    string localInGameJson = File.Exists(localInGamePath) ? await File.ReadAllTextAsync(localInGamePath) : string.Empty;
                    UpdateStatus = "Syncing in-game paints database...";
                    (List<InGamePaintModel>? remoteInGamePaints, string? remoteInGameJson) = await _updateService.FetchRemoteInGamePaintsFileAsync(Constants.InGamePaintsJson);
                    if (remoteInGamePaints != null && remoteInGameJson != localInGameJson)
                    {
                        Log.Information("Remote ingame_paints.json is different from local. Updating...");
                        await File.WriteAllTextAsync(localInGamePath, remoteInGameJson!);
                        LoadInGamePaints();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to sync remote ingame_paints.json");
                }

                // Download missing in-game paint previews if needed
                int missingPreviewsCount = InGamePaints.Count(p => !string.IsNullOrEmpty(p.PreviewImage) && !File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, p.PreviewImage.Replace("\\", "/"))));
                if (missingPreviewsCount > 0)
                {
                    anyAssetsSynced = true;
                    int completedPreviews = 0;
                    foreach (var paint in InGamePaints)
                    {
                        if (!string.IsNullOrEmpty(paint.PreviewImage))
                        {
                            string localPreview = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, paint.PreviewImage.Replace("\\", "/"));
                            if (!File.Exists(localPreview))
                            {
                                UpdateStatus = $"Syncing in-game paints ({completedPreviews + 1}/{missingPreviewsCount}): {paint.Name}...";
                                string remoteUrl = $"{Constants.GitHubRawUrl}/{paint.PreviewImage.Replace("\\", "/")}";
                                try
                                {
                                    Log.Information("Downloading missing in-game paint preview: {Path}", paint.PreviewImage);
                                    Directory.CreateDirectory(Path.GetDirectoryName(localPreview)!);
                                    HttpResponseMessage res = await _updateService.GetWithRetryAsync(remoteUrl);
                                    byte[] data = await res.Content.ReadAsByteArrayAsync();
                                    await File.WriteAllBytesAsync(localPreview, data);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning("Failed to download in-game paint preview {Name}: {Message}", paint.Name, ex.Message);
                                }
                                completedPreviews++;
                            }
                        }
                    }
                }

                if (anyAssetsSynced)
                {
                    UpdateStatus = "Sync complete!";
                    await Task.Delay(1000);
                }
                else
                {
                    UpdateStatus = string.Empty;
                }

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

        private bool IsSkinMissingAssets(SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string previewPath = Path.GetFullPath(Path.Combine(baseDir, skin.PreviewImage.Replace("\\", "/")));
            if (!File.Exists(previewPath))
            {
                return true;
            }

            string[] suffixes = (skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase) || skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                ? ["details"]
                : ["details", "lightmap", "alpha"];

            foreach (string suffix in suffixes)
            {
                string folder = Path.Combine(baseDir, skin.SourceFolder.Replace("\\", "/"));
                bool found = false;
                if (Directory.Exists(folder))
                {
                    string[] matchingFiles = Directory.GetFiles(folder, $"{suffix}.*");
                    foreach (string file in matchingFiles)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(skin.ModelTarget))
            {
                string modelPath = Path.GetFullPath(Path.Combine(baseDir, skin.SourceFolder.Replace("\\", "/"), "object.3ds"));
                if (!File.Exists(modelPath))
                {
                    return true;
                }
            }

            return false;
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
                SelectedCategory = !string.IsNullOrEmpty(currentCategory) && Categories.Contains(currentCategory)
                    ? currentCategory
                    : (Settings.LastSelectedCategory != null && Categories.Contains(Settings.LastSelectedCategory))
                        ? Settings.LastSelectedCategory
                        : Categories.First();
            }
        }

        private void FilterItems()
        {
            ItemNames.Clear();
            ItemNames.Add("All models");

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
                    : "All models";
            }
        }

        private void FilterSkins()
        {
            FilteredSkins.Clear();
            IEnumerable<SkinModel> matchingSkins = _allSkins.Where(s =>
                s.Category == SelectedCategory &&
                (SelectedItemName == "All models" || s.ItemName == SelectedItemName) &&
                (string.IsNullOrWhiteSpace(SearchQuery) ||
                 s.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                 s.ItemName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)));

            foreach (SkinModel skin in matchingSkins)
            {
                FilteredSkins.Add(skin);
            }

            if (FilteredSkins.Any())
            {
                SelectedSkin = FilteredSkins.First();
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
            if (FileHelper.IsGameRunning())
            {
                await _notificationService.ShowAsync("Game Running", "Please close ProTanki and the Loader before proceeding.", ControlAppearance.Info);
                return false;
            }

            if (FileHelper.IsCacheFileLocked(CachePath))
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

            if (string.IsNullOrEmpty(CachePath) || !Directory.Exists(CachePath))
            {
                await _notificationService.ShowAsync("Error", "Invalid cache path.", ControlAppearance.Danger);
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                IsLoading = true;
                if (selectedSkins.Count != 0)
                {
                    if (SelectedCategory == "Paints")
                    {
                        if (SelectedInGamePaint == null)
                        {
                            await _notificationService.ShowAsync("Error", "Please select an in-game paint first.", ControlAppearance.Danger);
                            IsLoading = false;
                            return;
                        }
                        foreach (var skin in selectedSkins)
                        {
                            skin.DetailsTarget = SelectedInGamePaint.TargetUrl;
                        }
                    }

                    string skinMessage = selectedSkins.Count > 1 ? "skins" : "skin";
                    string textureMessage = selectedSkins.Count > 1 ? "textures" : "texture";

                    UpdateStatus = $"Applying {selectedSkins.Count} {textureMessage}...";
                    await Task.Run(() => _swapService.SwapBatch(CachePath, selectedSkins));
                    LoadBackups();
                    notificationTitle = "Success";
                    notificationMessage = $"{selectedSkins.Count} {skinMessage} applied successfully!";
                    notificationAppearance = ControlAppearance.Success;

                    foreach (SkinModel skin in _allSkins)
                    {
                        skin.IsSelected = false;
                    }
                }
                else if (SelectedSkin != null)
                {
                    UpdateStatus = $"Applying {SelectedSkin.Name}...";
                    await Task.Run(() => _swapService.Swap(CachePath, SelectedSkin));
                    LoadBackups();
                    notificationTitle = "Success";
                    notificationMessage = $"{SelectedSkin.Name} applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                }

                string countSkins = selectedSkins.Count < 2 ? "skin" : "skins";
                if (!string.IsNullOrEmpty(notificationTitle) && notificationAppearance == ControlAppearance.Success)
                {
                    UpdateStatus = $"Finalizing applying {selectedSkins.Count} {countSkins}...";
                    await Task.Delay(2500);
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

            if (string.IsNullOrEmpty(CachePath) || !Directory.Exists(CachePath))
            {
                await _notificationService.ShowAsync("Error", "Invalid cache path.", ControlAppearance.Danger);
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                IsLoading = true;
                UpdateStatus = "Restoring original textures...";
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

                if (!string.IsNullOrEmpty(notificationTitle))
                {
                    UpdateStatus = "Original textures are being restored...";
                    await Task.Delay(2500);
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
