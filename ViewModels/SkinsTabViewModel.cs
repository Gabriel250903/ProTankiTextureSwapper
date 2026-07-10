using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class SkinsTabViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private string _selectedCategory = string.Empty;
        private string _searchQuery = string.Empty;

        public ObservableCollection<string> Categories { get; } = [];
        public ObservableCollection<string> SkinsCategories { get; } = [];
        public ObservableCollection<string> ItemNames { get; } = [];
        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
        public ObservableCollection<List<SkinModel>> FilteredSkinsRows { get; } = [];

        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    _mainVM.Settings.LastSelectedCategory = value;
                    if (!_mainVM.IsLoading)
                    {
                        _mainVM.SaveSettings();
                    }
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
                    _mainVM.Settings.LastSelectedItemName = value;
                    if (!_mainVM.IsLoading)
                    {
                        _mainVM.SaveSettings();
                    }
                    FilterSkins();
                }
            }
        }

        public SkinModel? SelectedSkin
        {
            get => _selectedSkin;
            set => SetProperty(ref _selectedSkin, value);
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

        public string SelectAllText => _mainVM.AllSkins.Any(s => s.IsSelected && s.Category != "Paints") ? "Deselect all textures" : "Select all available textures";
        public string SelectAllIcon => _mainVM.AllSkins.Any(s => s.IsSelected && s.Category != "Paints") ? "DismissCircle24" : "CheckmarkCircle24";

        public ICommand SwapCommand { get; }
        public ICommand SelectAllAvailableCommand { get; }
        public ICommand ToggleSkinSelectionCommand { get; }
        public ICommand ClearCacheCommand => _mainVM.ClearCacheCommand;
        public ICommand RestoreCommand => _mainVM.RestoreCommand;

        public SkinsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            SwapCommand = new AsyncRelayCommand(ExecuteSwap, _ => !_mainVM.IsLoading && _mainVM.AllSkins.Any(s => s.IsSelected && s.Category != "Paints"));
            SelectAllAvailableCommand = new RelayCommand(ExecuteSelectAllAvailable);
            ToggleSkinSelectionCommand = new RelayCommand(ExecuteToggleSkinSelection);
        }

        public void OnSkinsLoaded()
        {
            InitializeCategories();
            FilterItems();
        }

        private void InitializeCategories()
        {
            if (_mainVM.AllSkins == null || _mainVM.AllSkins.Count == 0)
            {
                return;
            }

            if (Categories.Count == 0)
            {
                string? currentCategory = SelectedCategory;
                Categories.Clear();
                SkinsCategories.Clear();
                List<string> uniqueCategories = [.. _mainVM.AllSkins.Select(s => s.Category).Distinct().OrderBy(c => c)];
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
                        : (_mainVM.Settings.LastSelectedCategory != null && Categories.Contains(_mainVM.Settings.LastSelectedCategory))
                            ? _mainVM.Settings.LastSelectedCategory
                            : Categories.First();

                    if (targetCategory.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                    {
                        targetCategory = SkinsCategories.FirstOrDefault() ?? Categories.First();
                    }

                    SelectedCategory = targetCategory;
                }
            }
        }

        private void FilterItems()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            string allLabel = $"All {SelectedCategory}";
            string? currentSelected = SelectedItemName;

            ItemNames.Clear();
            ItemNames.Add(allLabel);

            IOrderedEnumerable<string> matchingItems = _mainVM.AllSkins
                .Where(s => s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ItemName)
                .Distinct()
                .OrderBy(name => name);

            foreach (string itemName in matchingItems)
            {
                ItemNames.Add(itemName);
            }

            if (ItemNames.Any())
            {
                SelectedItemName = (_mainVM.Settings.LastSelectedItemName != null && ItemNames.Contains(_mainVM.Settings.LastSelectedItemName))
                    ? _mainVM.Settings.LastSelectedItemName
                    : ItemNames.First();
            }

            FilterSkins();
        }

        public void FilterSkins()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            FilteredSkins.Clear();
            string allLabel = $"All {SelectedCategory}";
            string currentQuery = SearchQuery?.Trim() ?? string.Empty;

            IOrderedEnumerable<SkinModel> matchingSkins = _mainVM.AllSkins
                .Where(s => s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase) &&
                            (SelectedItemName == allLabel || s.ItemName == SelectedItemName) &&
                            (string.IsNullOrEmpty(currentQuery) ||
                             s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase) ||
                             s.ItemName.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(s => s.Name);

            foreach (SkinModel skin in matchingSkins)
            {
                FilteredSkins.Add(skin);
            }

            UpdateFilteredSkinsRows();
            OnPropertyChanged(nameof(SelectAllText));
            OnPropertyChanged(nameof(SelectAllIcon));
        }

        private int _columns = 3;

        public void UpdateColumns(int cols)
        {
            if (_columns != cols)
            {
                _columns = cols;
                UpdateFilteredSkinsRows();
            }
        }

        public void UpdateFilteredSkinsRows()
        {
            FilteredSkinsRows.Clear();
            List<SkinModel>? currentChunk = null;
            int chunkSize = _columns;

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

        private void ExecuteToggleSkinSelection(object? parameter)
        {
            if (parameter is SkinModel skin)
            {
                skin.IsSelected = !skin.IsSelected;
                Log.Information($"Toggled selection for skin: {skin.Name}. New state: {skin.IsSelected}");

                if (skin.IsSelected)
                {
                    foreach (SkinModel otherSkin in _mainVM.AllSkins)
                    {
                        if (otherSkin != skin &&
                            otherSkin.Category == skin.Category &&
                            otherSkin.ItemName == skin.ItemName &&
                            otherSkin.IsSelected &&
                            (otherSkin.DetailsTarget == skin.DetailsTarget ||
                             otherSkin.LightmapTarget == skin.LightmapTarget ||
                             otherSkin.AlphaTarget == skin.AlphaTarget))
                        {
                            otherSkin.IsSelected = false;
                            Log.Information($"Deselected conflicting skin {otherSkin.Name} because {skin.Name} was selected.");
                        }
                    }
                }

                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(SelectAllText));
                OnPropertyChanged(nameof(SelectAllIcon));
            }
        }

        private void ExecuteSelectAllAvailable(object? parameter)
        {
            string mode = parameter as string ?? "XT";

            if (mode == "Deselect")
            {
                Log.Information("Deselecting all textures.");
                foreach (SkinModel skin in _mainVM.AllSkins)
                {
                    if (skin.Category != "Paints")
                    {
                        skin.IsSelected = false;
                    }
                }
                _ = _mainVM.NotificationService.ShowAsync("Deselected All", "All textures have been deselected.", ControlAppearance.Info);
            }
            else
            {
                Log.Information("Selecting all {Mode} skins.", mode);

                List<SkinModel> toSelect = [];
                var groups = _mainVM.AllSkins
                    .Where(s => s.Category != "Paints")
                    .GroupBy(s => new { s.Category, s.ItemName });

                foreach (var group in groups)
                {
                    SkinModel? targetSkin = null;

                    if (mode == "Legacy")
                    {
                        targetSkin = group.FirstOrDefault(s => (s.Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase) || s.Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase)) && !s.Name.EndsWith(" LC on XT", StringComparison.OrdinalIgnoreCase));
                    }
                    else if (mode == "XT")
                    {
                        targetSkin = group.FirstOrDefault(s => s.Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase));
                    }
                    else if (mode == "LC on XT")
                    {
                        targetSkin = group.FirstOrDefault(s => s.Name.EndsWith(" LC on XT", StringComparison.OrdinalIgnoreCase));
                    }

                    if (targetSkin != null)
                    {
                        toSelect.Add(targetSkin);
                    }
                }

                int selectedCount = 0;
                foreach (SkinModel skin in toSelect)
                {
                    foreach (SkinModel otherSkin in _mainVM.AllSkins)
                    {
                        if (otherSkin != skin &&
                            otherSkin.Category == skin.Category &&
                            otherSkin.ItemName == skin.ItemName &&
                            otherSkin.IsSelected &&
                            (otherSkin.DetailsTarget == skin.DetailsTarget ||
                             otherSkin.LightmapTarget == skin.LightmapTarget ||
                             otherSkin.AlphaTarget == skin.AlphaTarget))
                        {
                            otherSkin.IsSelected = false;
                        }
                    }

                    skin.IsSelected = true;
                    selectedCount++;
                }

                _ = _mainVM.NotificationService.ShowAsync("Selected All", $"Selected {selectedCount} textures ({mode} priority) for batch application.", ControlAppearance.Info);
            }

            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(SelectAllText));
            OnPropertyChanged(nameof(SelectAllIcon));
        }

        private async Task ExecuteSwap(object? parameter)
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            List<SkinModel> selectedSkins = [.. _mainVM.AllSkins.Where(s => s.IsSelected && s.Category != "Paints")];

            if (selectedSkins.Count == 0 && SelectedSkin == null)
            {
                await _mainVM.NotificationService.ShowAsync("Error", "Please select at least one skin first.", ControlAppearance.Danger);
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                _mainVM.IsLoading = true;

                List<SkinModel> skinsToApply = selectedSkins.Count != 0 ? selectedSkins : [SelectedSkin!];

                string skinMessage = skinsToApply.Count > 1 ? "skins" : "skin";
                string textureMessage = skinsToApply.Count > 1 ? "textures" : "texture";

                _mainVM.UpdateStatus = $"Applying {skinsToApply.Count} {textureMessage} to cache...";
                Log.Information($"Writing {skinsToApply.Count} skins directly to cache disk path: {_mainVM.CachePath}.");

                string? error = await Task.Run(() => _mainVM.SwapService.SwapBatch(_mainVM.CachePath, skinsToApply, null));
                if (error != null)
                {
                    notificationTitle = "Error";
                    notificationMessage = error;
                    notificationAppearance = ControlAppearance.Danger;
                }
                else
                {
                    _mainVM.BackupsTabVM.LoadBackups();
                    notificationTitle = "Success";
                    notificationMessage = $"{skinsToApply.Count} {skinMessage} applied successfully!";
                    notificationAppearance = ControlAppearance.Success;

                    foreach (SkinModel skin in _mainVM.AllSkins)
                    {
                        if (skin.Category != "Paints")
                        {
                            skin.IsSelected = false;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(notificationTitle) && notificationAppearance == ControlAppearance.Success)
                {
                    _mainVM.UpdateStatus = $"Finalizing applying {skinsToApply.Count} {skinMessage}...";
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
                _mainVM.IsLoading = false;
                _mainVM.UpdateStatus = string.Empty;
                CommandManager.InvalidateRequerySuggested();
                OnPropertyChanged(nameof(SelectAllText));
                OnPropertyChanged(nameof(SelectAllIcon));
            }

            if (!string.IsNullOrEmpty(notificationTitle))
            {
                await _mainVM.NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }
    }
}
