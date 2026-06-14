using Serilog;
using System.Collections.ObjectModel;
using System.IO;
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
        private readonly INotificationService _notificationService;
        private readonly SettingsService _settingsService;
        private List<SkinModel> _allSkins = [];

        private string _cachePath = string.Empty;
        private string _selectedCategory = string.Empty;
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private bool _isLoading;
        private string _updateStatus = string.Empty;

        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
        public ObservableCollection<string> Categories { get; } = [];
        public ObservableCollection<string> ItemNames { get; } = [];

        public AppSettings Settings { get; }

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
                    FilterItems();
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

        public MainViewModel(INotificationService notificationService, UpdateService updateService)
        {
            _notificationService = notificationService;
            _updateService = updateService;
            _swapService = new SwapService();
            _settingsService = new SettingsService();
            Settings = _settingsService.Load();

            CachePath = _swapService.DetectCachePath();

            BrowseCommand = new RelayCommand(ExecuteBrowse);
            SwapCommand = new AsyncRelayCommand(ExecuteSwap, _ => !IsLoading);
            RestoreCommand = new AsyncRelayCommand(ExecuteRestore, _ => !IsLoading);
            ClearCacheCommand = new AsyncRelayCommand(ExecuteClearCache, _ => !IsLoading);
            SelectAllAvailableCommand = new RelayCommand(ExecuteSelectAllAvailable);

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            IsLoading = true;
            UpdateStatus = "Checking for updates...";

            await LoadSkinsAsync();
            await CheckForAppUpdatesAsync();

            IsLoading = false;
            UpdateStatus = string.Empty;
        }

        public async Task TriggerUpdateCheckAsync()
        {
            IsLoading = true;
            UpdateStatus = "Checking for updates...";
            await CheckForAppUpdatesAsync();
            IsLoading = false;
            UpdateStatus = string.Empty;
        }

        public void SaveTheme(Wpf.Ui.Appearance.ApplicationTheme theme)
        {
            Settings.Theme = theme;
            _settingsService.Save(Settings);
        }

        private async Task CheckForAppUpdatesAsync()
        {
            GitHubRelease? release = await _updateService.CheckForAppUpdatesAsync();
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
                        GitHubAsset? asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe")) ?? release.Assets.FirstOrDefault();
                        if (asset != null)
                        {
                            IsLoading = true;
                            UpdateStatus = "Downloading update...";
                            await _updateService.DownloadAndRunInstallerAsync(asset.BrowserDownloadUrl, p => UpdateStatus = $"Downloading update ({p:F0}%)...");
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
                UpdateStatus = "Syncing skins...";
                List<SkinModel>? remoteSkins = await _updateService.FetchRemoteSkinsAsync();

                if (remoteSkins != null)
                {
                    _allSkins = remoteSkins;
                }
                else
                {
                    string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.SkinsJson);
                    if (File.Exists(jsonPath))
                    {
                        string json = await File.ReadAllTextAsync(jsonPath);
                        _allSkins = JsonSerializer.Deserialize<List<SkinModel>>(json) ?? [];
                    }
                }

                if (_allSkins.Count == 0)
                {
                    return;
                }
                Categories.Clear();
                List<string> uniqueCategories = [.. _allSkins.Select(s => s.Category).Distinct().OrderBy(c => c)];
                foreach (string category in uniqueCategories)
                {
                    Categories.Add(category);
                }

                if (Categories.Any())
                {
                    SelectedCategory = Categories.First();
                }

                _ = Task.Run(async () =>
                {
                    foreach (SkinModel skin in _allSkins)
                    {
                        if (IsSkinMissingAssets(skin))
                        {
                            await _updateService.EnsureAssetsExistAsync(skin);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load skins.");
                await _notificationService.ShowAsync("Error", $"Failed to load skins: {ex.Message}", ControlAppearance.Danger);
            }
        }

        private void ExecuteSelectAllAvailable(object? parameter)
        {
            Log.Information("Selecting all available textures globally.");

            IEnumerable<SkinModel> itemsToSelect = _allSkins
                .GroupBy(s => s.ItemName)
                .Select(g => g.First());

            foreach (SkinModel skin in _allSkins)
            {
                skin.IsSelected = false;
            }

            foreach (SkinModel? skin in itemsToSelect)
            {
                skin.IsSelected = true;
            }

            _ = _notificationService.ShowAsync("Selected All", $"Selected {itemsToSelect.Count()} textures for batch application.", ControlAppearance.Info);
        }

        private bool IsSkinMissingAssets(SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string previewPath = Path.GetFullPath(Path.Combine(baseDir, skin.PreviewImage.Replace("\\", "/")));
            if (!File.Exists(previewPath))
            {
                return true;
            }

            string[] files = ["details.png", "lightmap.png", "alpha.png"];
            foreach (string file in files)
            {
                string relativePath = Path.Combine(skin.SourceFolder, file).Replace("\\", "/");
                string fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                if (!File.Exists(fullPath))
                {
                    return true;
                }
            }

            return false;
        }

        private void FilterItems()
        {
            ItemNames.Clear();
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
                SelectedItemName = ItemNames.First();
            }
        }

        private void FilterSkins()
        {
            FilteredSkins.Clear();
            IEnumerable<SkinModel> matchingSkins = _allSkins.Where(s => s.Category == SelectedCategory && s.ItemName == SelectedItemName);
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

            try
            {
                IsLoading = true;
                if (selectedSkins.Count != 0)
                {
                    await Task.Run(() => _swapService.SwapBatch(CachePath, selectedSkins));
                    await _notificationService.ShowAsync("Success", $"{selectedSkins.Count} skins applied successfully!", ControlAppearance.Success);
                }
                else if (SelectedSkin != null)
                {
                    await Task.Run(() => _swapService.Swap(CachePath, SelectedSkin));
                    await _notificationService.ShowAsync("Success", $"{SelectedSkin.Name} applied successfully!", ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply skins.");
                await _notificationService.ShowAsync("Error", $"Failed to apply skins: {ex.Message}", ControlAppearance.Danger);
            }
            finally
            {
                IsLoading = false;
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

            try
            {
                IsLoading = true;
                bool restored = await Task.Run(() => _swapService.RestoreFullCache(CachePath));
                if (restored)
                {
                    await _notificationService.ShowAsync("Restored", "Original textures restored successfully.", ControlAppearance.Success);
                }
                else
                {
                    await _notificationService.ShowAsync("No Backup", "No original textures found to restore. Apply a skin first to create a backup.", ControlAppearance.Info);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore textures.");
                await _notificationService.ShowAsync("Error", $"Failed to restore: {ex.Message}", ControlAppearance.Danger);
            }
            finally
            {
                IsLoading = false;
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

            try
            {
                IsLoading = true;
                await Task.Run(() => _swapService.ClearCache(CachePath));
                await _notificationService.ShowAsync("Cache Cleared", "ProTanki cache has been emptied.", ControlAppearance.Success);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear cache.");
                await _notificationService.ShowAsync("Error", $"Failed to clear cache: {ex.Message}", ControlAppearance.Danger);
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
