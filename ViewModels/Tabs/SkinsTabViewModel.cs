using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Serilog;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Services;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace TextureSwapper.ViewModels.Tabs
{
    public class SkinsTabViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private string _selectedCategory = string.Empty;
        private string _searchQuery = string.Empty;
        private CancellationTokenSource? _searchDebounceCts;

        public ObservableCollection<string> Categories { get; } = [];
        public ObservableCollection<string> SkinsCategories { get; } = [];
        public ObservableCollection<string> ItemNames { get; } = [];
        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
        public ObservableCollection<List<SkinModel>> FilteredSkinsRows { get; } = [];

        public ObservableCollection<string> AvailableTags { get; } = ["All"];
        public ObservableCollection<string> SortOptions { get; } = ["Name (A-Z)", "Name (Z-A)", "Tag / Theme", "Premium First"];

        private string _selectedTagFilter = "All";
        public string SelectedTagFilter
        {
            get => _selectedTagFilter;
            set
            {
                if (SetProperty(ref _selectedTagFilter, value))
                {
                    FilterSkins();
                }
            }
        }

        private string _selectedSortOption = "Name (A-Z)";
        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (SetProperty(ref _selectedSortOption, value))
                {
                    FilterSkins();
                }
            }
        }

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
                    RefreshAvailableTags();
                    FilterSkins();
                }
            }
        }

        public SkinModel? SelectedSkin
        {
            get => _selectedSkin;
            set
            {
                if (
                    SetProperty(ref _selectedSkin, value)
                    && value != null
                    && _mainVM.IsHalloweenThemeActive
                )
                {
                    _mainVM.HalloweenSoundService.PlayCardSelect();
                }
            }
        }

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _searchDebounceCts?.Cancel();
                    _searchDebounceCts = new CancellationTokenSource();
                    CancellationToken token = _searchDebounceCts.Token;

                    _ = Task.Delay(200, token)
                        .ContinueWith(
                            t =>
                            {
                                if (!t.IsCanceled)
                                {
                                    _ = (Application.Current?.Dispatcher.InvokeAsync(FilterSkins));
                                }
                            },
                            TaskScheduler.Default
                        );
                }
            }
        }

        public string SelectAllText =>
            _mainVM.AllSkins.Any(s => s.IsSelected && s.Category != "Paints")
                ? "Deselect all textures"
                : "Select all available textures";
        public string SelectAllIcon =>
            _mainVM.AllSkins.Any(s => s.IsSelected && s.Category != "Paints")
                ? "DismissCircle24"
                : "CheckmarkCircle24";

        public ICommand SwapCommand { get; }
        public ICommand SelectAllAvailableCommand { get; }
        public ICommand ToggleSkinSelectionCommand { get; }
        public ICommand SelectTagFilterCommand { get; }
        public ICommand ClearCacheCommand => _mainVM.ClearCacheCommand;
        public ICommand RestoreCommand => _mainVM.RestoreCommand;

        public SkinsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            SwapCommand = new AsyncRelayCommand(
                ExecuteSwap,
                _ =>
                    !_mainVM.IsLoading
                    && _mainVM.AllSkins.Any(s => s.IsSelected && s.Category != "Paints")
            );
            SelectAllAvailableCommand = new RelayCommand(ExecuteSelectAllAvailable);
            ToggleSkinSelectionCommand = new RelayCommand(ExecuteToggleSkinSelection);
            SelectTagFilterCommand = new RelayCommand(param =>
            {
                if (param is string tag)
                {
                    SelectedTagFilter = tag;
                }
            });
        }

        public void OnSkinsLoaded()
        {
            if (_mainVM.AllSkins != null)
            {
                foreach (SkinModel skin in _mainVM.AllSkins)
                {
                    skin.RefreshAvailableModifications();
                }
            }
            InitializeCategories();
            FilterItems();
        }

        private void InitializeCategories()
        {
            if (_mainVM.AllSkins == null || _mainVM.AllSkins.Count == 0)
            {
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Categories.Count == 0)
                {
                    string? currentCategory = SelectedCategory;
                    Categories.Clear();
                    SkinsCategories.Clear();
                    string[] preferredOrder = ["Hulls", "Turrets", "Supplies", "Spawners"];
                    List<string> uniqueCategories =
                    [
                        .. _mainVM.AllSkins.Select(s => s.Category).Distinct(),
                    ];
                    IOrderedEnumerable<string> orderedCategories = uniqueCategories
                        .OrderBy(c =>
                        {
                            int index = Array.IndexOf(preferredOrder, c);
                            return index >= 0 ? index : int.MaxValue;
                        })
                        .ThenBy(c => c);

                    foreach (string category in orderedCategories)
                    {
                        Categories.Add(category);
                        if (!category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                        {
                            SkinsCategories.Add(category);
                        }
                    }

                    if (Categories.Any())
                    {
                        string targetCategory =
                            !string.IsNullOrEmpty(currentCategory)
                            && Categories.Contains(currentCategory)
                                ? currentCategory
                            : (
                                _mainVM.Settings.LastSelectedCategory != null
                                && Categories.Contains(_mainVM.Settings.LastSelectedCategory)
                            )
                                ? _mainVM.Settings.LastSelectedCategory
                            : Categories.First();

                        if (targetCategory.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                        {
                            targetCategory = SkinsCategories.FirstOrDefault() ?? Categories.First();
                        }

                        SelectedCategory = targetCategory;
                    }
                }
            });
        }

        private void FilterItems()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                string allLabel = $"All {SelectedCategory}";

                ItemNames.Clear();
                ItemNames.Add(allLabel);

                IOrderedEnumerable<string> matchingItems = _mainVM
                    .AllSkins.Where(s =>
                        s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(s => s.ItemName)
                    .Distinct()
                    .OrderBy(name => name);

                foreach (string itemName in matchingItems)
                {
                    ItemNames.Add(itemName);
                }

                if (ItemNames.Any())
                {
                    SelectedItemName =
                        (
                            _mainVM.Settings.LastSelectedItemName != null
                            && ItemNames.Contains(_mainVM.Settings.LastSelectedItemName)
                        )
                            ? _mainVM.Settings.LastSelectedItemName
                            : ItemNames.First();
                }

                RefreshAvailableTags();
                FilterSkins();
            });
        }

        private void RefreshAvailableTags()
        {
            if (_mainVM.AllSkins == null) return;

            string allLabel = $"All {SelectedCategory}";
            var categorySkins = _mainVM.AllSkins.Where(s =>
                s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)
                && (SelectedItemName == allLabel || s.ItemName == SelectedItemName)
            );

            HashSet<string> tagsInView = new(StringComparer.OrdinalIgnoreCase);
            foreach (SkinModel skin in categorySkins)
            {
                foreach (string tag in skin.ComputedTags)
                {
                    tagsInView.Add(tag);
                }
            }

            string[] standardOrder =
            [
                "Tanki X",
                "Demonic",
                "Legacy",
                "XT",
                "HD",
                "Prime",
                "IC",
                "SP",
                "VT",
                "RT",
                "CS",
                "Experimental",
                "Old",
            ];
            List<string> orderedTags = ["All"];

            foreach (string std in standardOrder)
            {
                if (tagsInView.Contains(std))
                {
                    orderedTags.Add(std);
                    tagsInView.Remove(std);
                }
            }

            foreach (string custom in tagsInView.OrderBy(t => t))
            {
                orderedTags.Add(custom);
            }

            if (!AvailableTags.SequenceEqual(orderedTags))
            {
                AvailableTags.Clear();
                foreach (string t in orderedTags)
                {
                    AvailableTags.Add(t);
                }
            }

            if (!AvailableTags.Contains(SelectedTagFilter))
            {
                _selectedTagFilter = "All";
                OnPropertyChanged(nameof(SelectedTagFilter));
            }
        }

        public void FilterSkins()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            Application.Current?.Dispatcher.Invoke(() =>
            {
                FilteredSkins.Clear();
                string allLabel = $"All {SelectedCategory}";
                string currentQuery = SearchQuery?.Trim() ?? string.Empty;

                IEnumerable<SkinModel> matching = _mainVM
                    .AllSkins.Where(s =>
                        s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)
                        && (SelectedItemName == allLabel || s.ItemName == SelectedItemName)
                        && (
                            string.IsNullOrEmpty(currentQuery)
                            || s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            || s.ItemName.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            || s.ComputedTags.Any(t => t.Contains(currentQuery, StringComparison.OrdinalIgnoreCase))
                        )
                        && (
                            SelectedTagFilter == "All"
                            || s.ComputedTags.Contains(SelectedTagFilter, StringComparer.OrdinalIgnoreCase)
                        )
                    );

                matching = SelectedSortOption switch
                {
                    "Name (Z-A)" => matching.OrderByDescending(s => s.Name),
                    "Tag / Theme" => matching.OrderBy(s => s.PrimaryThemeTag ?? "zzz").ThenBy(s => s.Name),
                    "Premium First" => matching.OrderByDescending(s => s.IsPremium).ThenBy(s => s.Name),
                    _ => matching.OrderBy(s => s.Name),
                };

                foreach (SkinModel skin in matching)
                {
                    skin.RefreshAvailableModifications();
                    FilteredSkins.Add(skin);
                }

                UpdateFilteredSkinsRows();
                OnPropertyChanged(nameof(SelectAllText));
                OnPropertyChanged(nameof(SelectAllIcon));
            });
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
            Application.Current?.Dispatcher.Invoke(() =>
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
            });
        }

        private void ExecuteToggleSkinSelection(object? parameter)
        {
            if (parameter is SkinModel skin)
            {
                skin.IsSelected = !skin.IsSelected;
                Log.Information(
                    $"Toggled selection for skin: {skin.Name}. New state: {skin.IsSelected}"
                );

                if (skin.IsSelected)
                {
                    foreach (SkinModel otherSkin in _mainVM.AllSkins)
                    {
                        if (
                            otherSkin != skin
                            && otherSkin.Category == skin.Category
                            && otherSkin.ItemName == skin.ItemName
                            && otherSkin.IsSelected
                            && (
                                otherSkin.DetailsTarget == skin.DetailsTarget
                                || otherSkin.LightmapTarget == skin.LightmapTarget
                                || otherSkin.AlphaTarget == skin.AlphaTarget
                            )
                        )
                        {
                            otherSkin.IsSelected = false;
                            Log.Information(
                                $"Deselected conflicting skin {otherSkin.Name} because {skin.Name} was selected."
                            );
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
                _ = _mainVM.NotificationService.ShowAsync(
                    "Deselected All",
                    "All textures have been deselected.",
                    ControlAppearance.Info
                );
            }
            else
            {
                Log.Information("Selecting all {Mode} skins.", mode);

                List<SkinModel> toSelect = [];
                var groups = _mainVM
                    .AllSkins.Where(s => s.Category != "Paints")
                    .GroupBy(s => new { s.Category, s.ItemName });

                foreach (var group in groups)
                {
                    SkinModel? targetSkin = null;

                    if (mode == "Legacy")
                    {
                        targetSkin = group.FirstOrDefault(s =>
                            (
                                s.Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase)
                                || s.Name.EndsWith(" Legacy", StringComparison.OrdinalIgnoreCase)
                            ) && !s.Name.EndsWith(" LC on XT", StringComparison.OrdinalIgnoreCase)
                        );
                    }
                    else if (mode == "XT")
                    {
                        targetSkin = group.FirstOrDefault(s =>
                            s.Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase)
                        );
                    }
                    else if (mode == "LC on XT")
                    {
                        targetSkin = group.FirstOrDefault(s =>
                            s.Name.EndsWith(" LC on XT", StringComparison.OrdinalIgnoreCase)
                        );
                    }
                    else if (mode == "Demonic")
                    {
                        targetSkin = group.FirstOrDefault(s =>
                            s.Name.Contains("Demonic", StringComparison.OrdinalIgnoreCase)
                            || s.Name.Contains(" DC", StringComparison.OrdinalIgnoreCase)
                        );
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
                        if (
                            otherSkin != skin
                            && otherSkin.Category == skin.Category
                            && otherSkin.ItemName == skin.ItemName
                            && otherSkin.IsSelected
                            && (
                                otherSkin.DetailsTarget == skin.DetailsTarget
                                || otherSkin.LightmapTarget == skin.LightmapTarget
                                || otherSkin.AlphaTarget == skin.AlphaTarget
                            )
                        )
                        {
                            otherSkin.IsSelected = false;
                        }
                    }

                    skin.IsSelected = true;
                    selectedCount++;
                }

                _ = _mainVM.NotificationService.ShowAsync(
                    "Selected All",
                    $"Selected {selectedCount} textures ({mode} priority) for batch application.",
                    ControlAppearance.Info
                );
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

            List<SkinModel> selectedSkins =
            [
                .. _mainVM.AllSkins.Where(s => s.IsSelected && s.Category != "Paints"),
            ];

            if (selectedSkins.Count == 0 && SelectedSkin == null)
            {
                await _mainVM.NotificationService.ShowAsync(
                    LocalizationService.Instance.GetString("Loc_Msg_Warning"),
                    LocalizationService.Instance.GetString("Loc_Msg_SelectSkinWarning"),
                    ControlAppearance.Danger
                );
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            List<SkinModel> skinsToApply =
                selectedSkins.Count != 0 ? selectedSkins : [SelectedSkin!];

            List<SkinModel> highDetailSkins = [.. skinsToApply.Where(IsHighDetailSkin)];
            if (highDetailSkins.Count > 0)
            {
                string skinList = highDetailSkins.Count > 5
                    ? string.Join("\n• ", highDetailSkins.Take(5).Select(s => s.Name)) + $"\n• ...and {highDetailSkins.Count - 5} more"
                    : string.Join("\n• ", highDetailSkins.Select(s => s.Name));

                MessageBox warnBox = new()
                {
                    Title = "Quick heads-up",
                    Content =
                        $"You're applying high-detail skin textures:\n• {skinList}\n\nA few things to keep in mind:\n• The game might take a bit longer to load.\n• You might face occasional FPS stutters or drops in battle.\n• In some cases, the client might crash.\n\nWant to apply anyway?",
                    PrimaryButtonText = "Apply anyway",
                    CloseButtonText = "Cancel",
                    MaxWidth = 540,
                };

                MessageBoxResult result = await warnBox.ShowDialogAsync();
                if (result != MessageBoxResult.Primary)
                {
                    Log.Information("User canceled swap of high-detail skins.");
                    return;
                }
            }

            try
            {
                _mainVM.IsLoading = true;

                if (skinsToApply.Any(s => s.IsPremium) && !_mainVM.PremiumService.IsPremiumUnlocked)
                {
                    _mainVM.IsLoading = false;
                    IEnumerable<string> premiumSkinNames = skinsToApply
                        .Where(s => s.IsPremium)
                        .Select(s => s.Name);
                    await _mainVM.NotificationService.ShowAsync(
                        "Premium Required",
                        $"The following skin(s) require an active Premium license:\n- {string.Join("\n- ", premiumSkinNames)}\n\nPlease activate Premium in Settings to use them.",
                        ControlAppearance.Caution
                    );
                    return;
                }

                string skinMessage = skinsToApply.Count > 1 ? "skins" : "skin";
                string textureMessage = skinsToApply.Count > 1 ? "textures" : "texture";

                _mainVM.UpdateStatus =
                    $"Applying {skinsToApply.Count} {textureMessage} to cache...";
                Log.Information(
                    $"Writing {skinsToApply.Count} skins directly to cache disk path: {_mainVM.CachePath}."
                );

                string? error = await Task.Run(() =>
                    _mainVM.SwapService.SwapBatch(_mainVM.CachePath, skinsToApply, null)
                );
                if (error != null)
                {
                    notificationTitle = LocalizationService.Instance.GetString("Loc_Msg_Error");
                    notificationMessage = error;
                    notificationAppearance = ControlAppearance.Danger;
                }
                else
                {
                    _mainVM.BackupsTabVM.LoadBackups();
                    notificationTitle = LocalizationService.Instance.GetString("Loc_Msg_Success");
                    notificationMessage = LocalizationService.Instance.GetString(
                        "Loc_Msg_SwapSuccess"
                    );
                    notificationAppearance = ControlAppearance.Success;
                    if (_mainVM.IsHalloweenThemeActive)
                    {
                        _mainVM.HalloweenSoundService.PlayApplySuccess();
                    }

                    foreach (SkinModel skin in _mainVM.AllSkins)
                    {
                        if (skin.Category != "Paints")
                        {
                            skin.IsSelected = false;
                        }
                    }
                }

                if (
                    !string.IsNullOrEmpty(notificationTitle)
                    && notificationAppearance == ControlAppearance.Success
                )
                {
                    _mainVM.UpdateStatus =
                        $"Finalizing applying {skinsToApply.Count} {skinMessage}...";
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
                await _mainVM.NotificationService.ShowAsync(
                    notificationTitle,
                    notificationMessage,
                    notificationAppearance
                );
            }
        }

        private static bool IsHighDetailSkin(SkinModel s)
        {
            if (s.IsExperimental)
            {
                return true;
            }

            string name = s.Name ?? string.Empty;
            IReadOnlyList<string> tags = s.ComputedTags;

            bool isDemonic = tags.Contains("Demonic", StringComparer.OrdinalIgnoreCase)
                || name.Contains("Demonic", StringComparison.OrdinalIgnoreCase)
                || name.Contains(" DC", StringComparison.OrdinalIgnoreCase);

            bool isXtHd = name.Contains("XT HD", StringComparison.OrdinalIgnoreCase)
                || (tags.Contains("XT", StringComparer.OrdinalIgnoreCase) && tags.Contains("HD", StringComparer.OrdinalIgnoreCase))
                || (name.Contains("XT", StringComparison.OrdinalIgnoreCase) && name.Contains("HD", StringComparison.OrdinalIgnoreCase));

            bool isTankiX = tags.Contains("Tanki X", StringComparer.OrdinalIgnoreCase)
                || name.Contains("Tanki X", StringComparison.OrdinalIgnoreCase)
                || name.Contains("TX", StringComparison.OrdinalIgnoreCase);

            return isDemonic || isXtHd || isTankiX;
        }
    }
}
