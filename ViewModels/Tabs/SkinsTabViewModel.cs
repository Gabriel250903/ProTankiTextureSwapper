using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Serilog;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.Services;
using TextureSwapper.ViewModels.Base;
using TextureSwapper.Views.Windows;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace TextureSwapper.ViewModels.Tabs
{
    public class SkinsTabViewModel : ViewModelBase
    {
        private sealed record QueuedSkinSwap(
            SkinModel BaseSkin,
            string TargetTier,
            SkinModel ResolvedSkin
        );

        private readonly MainViewModel _mainVM;
        private readonly Dictionary<string, QueuedSkinSwap> _queuedSwaps = new(
            StringComparer.OrdinalIgnoreCase
        );
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private string _selectedCategory = string.Empty;
        private string _searchQuery = string.Empty;
        private string _selectedGarageTier = "M3";
        private CancellationTokenSource? _searchDebounceCts;

        public ObservableCollection<string> Categories { get; } = [];
        public ObservableCollection<string> SkinsCategories { get; } = [];
        public ObservableCollection<string> ItemNames { get; } = [];
        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
        public ObservableCollection<List<SkinModel>> FilteredSkinsRows { get; } = [];

        public ObservableCollection<string> AvailableTags { get; } = ["All"];
        public ObservableCollection<string> SortOptions { get; } =
        ["Name (A-Z)", "Name (Z-A)", "Tag / Theme", "Premium First"];

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

        private bool _showOnlyGiftedSkins;
        public bool ShowOnlyGiftedSkins
        {
            get => _showOnlyGiftedSkins;
            set
            {
                if (SetProperty(ref _showOnlyGiftedSkins, value))
                {
                    FilterSkins();
                }
            }
        }

        public bool HasGiftedSkins =>
            !_mainVM.PremiumService.IsPremiumUnlocked
            && _mainVM.AllSkins.Any(s => s.IsEventUnlocked);

        public string GiftedSkinsButtonText
        {
            get
            {
                int count = _mainVM.AllSkins.Count(s => s.IsEventUnlocked);
                return $"🎁 My Gifts ({count})";
            }
        }

        public ICommand ToggleGiftedFilterCommand =>
            new RelayCommand(_ => ShowOnlyGiftedSkins = !ShowOnlyGiftedSkins);

        public void RefreshGiftedSkinsState()
        {
            if (!HasGiftedSkins && _showOnlyGiftedSkins)
            {
                _showOnlyGiftedSkins = false;
                OnPropertyChanged(nameof(ShowOnlyGiftedSkins));
            }
            OnPropertyChanged(nameof(HasGiftedSkins));
            OnPropertyChanged(nameof(GiftedSkinsButtonText));
            FilterSkins();
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
                    OnPropertyChanged(nameof(IsGarageTierSelectorVisible));
                    OnPropertyChanged(nameof(ShowNonM3Banner));
                    OnPropertyChanged(nameof(IsNonM3PartialTargets));
                    OnPropertyChanged(nameof(NonM3BannerText));
                    FilterItems();
                }
            }
        }

        public string SelectedGarageTier
        {
            get => _selectedGarageTier;
            set
            {
                string next = string.IsNullOrWhiteSpace(value)
                    ? "M3"
                    : value.Trim().ToUpperInvariant();
                if (next is not ("M3" or "M2" or "M1"))
                {
                    next = "M3";
                }

                if (SetProperty(ref _selectedGarageTier, next))
                {
                    _mainVM.Settings.LastSelectedGarageTier = next;
                    if (!_mainVM.IsLoading)
                    {
                        _mainVM.SaveSettings();
                    }
                    RefreshTierStates();
                    RefreshAvailableTags();
                    FilterSkins();
                    NotifyTierPropertiesChanged();
                }
            }
        }

        public bool IsGarageTierSelectorVisible =>
            SelectedCategory.Equals("Hulls", StringComparison.OrdinalIgnoreCase)
            || SelectedCategory.Equals("Turrets", StringComparison.OrdinalIgnoreCase);

        public bool IsM3TierSelected => SelectedGarageTier == "M3";
        public bool IsM2TierSelected => SelectedGarageTier == "M2";
        public bool IsM1TierSelected => SelectedGarageTier == "M1";

        public bool IsM2TierEnabled => _mainVM.IsHtml5Client;
        public bool IsM1TierEnabled =>
            _mainVM.IsHtml5Client && _mainVM.EquipmentTargetService.HasAnyMappedTargets("M1");

        public string M2TierToolTip =>
            _mainVM.IsHtml5Client
                ? "Target M2 turrets & hulls in your garage (HTML5)"
                : "M2 tier is available for HTML5 Client only";

        public string M1TierToolTip =>
            !_mainVM.IsHtml5Client ? "M1 tier is available for HTML5 Client only"
            : _mainVM.EquipmentTargetService.HasAnyMappedTargets("M1")
                ? "Target M1 turrets & hulls in your garage (HTML5)"
            : "M1 support coming soon";

        public bool ShowNonM3Banner => IsGarageTierSelectorVisible && SelectedGarageTier != "M3";

        public bool IsNonM3PartialTargets =>
            SelectedGarageTier != "M3"
            && _mainVM.EquipmentTargetService.GetMappedCount(SelectedGarageTier)
                < _mainVM.EquipmentTargetService.GetTotalEquipmentCount();

        public string NonM3BannerText
        {
            get
            {
                if (SelectedGarageTier == "M3")
                {
                    return string.Empty;
                }

                int mapped = _mainVM.EquipmentTargetService.GetMappedCount(SelectedGarageTier);
                int total = _mainVM.EquipmentTargetService.GetTotalEquipmentCount();

                return mapped == 0
                        ? $"No {SelectedGarageTier} targets scanned yet (0/{total}). View {SelectedGarageTier} items in ProTanki HTML5 and run Html5TargetScanner to map their Resource IDs."
                    : mapped < total
                        ? $"Targeting {SelectedGarageTier} garage items ({mapped}/{total} mapped) — native {SelectedGarageTier} textures are used when available; otherwise M3 models are applied automatically."
                    : $"Targeting {SelectedGarageTier} garage items (HTML5) — native {SelectedGarageTier} textures are used when available; otherwise M3 models are applied automatically.";
            }
        }

        public bool IsXtQuickSelectVisible => SelectedGarageTier == "M3";

        public string SelectedQueueSummary
        {
            get
            {
                if (_queuedSwaps.Count == 0)
                {
                    return string.Empty;
                }

                bool hasEquipment = _queuedSwaps.Values.Any(q =>
                    q.BaseSkin.Category is "Hulls" or "Turrets"
                );
                if (!hasEquipment)
                {
                    return $"{_queuedSwaps.Count} selected";
                }

                if (_queuedSwaps.Count == 1)
                {
                    QueuedSkinSwap single = _queuedSwaps.Values.First();
                    return single.BaseSkin.Category is "Hulls" or "Turrets"
                        ? $"1 selected ({single.TargetTier})"
                        : "1 selected";
                }

                List<string> tierParts =
                [
                    .. _queuedSwaps
                        .Values.Where(q => q.BaseSkin.Category is "Hulls" or "Turrets")
                        .GroupBy(q => q.TargetTier)
                        .OrderByDescending(g => g.Key)
                        .Select(g => $"{g.Count()} {g.Key}"),
                ];
                int otherCount = _queuedSwaps.Values.Count(q =>
                    q.BaseSkin.Category is not ("Hulls" or "Turrets")
                );
                if (otherCount > 0)
                {
                    tierParts.Add($"{otherCount} Other");
                }

                return $"{_queuedSwaps.Count} selected ({string.Join(", ", tierParts)})";
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
                    && _mainVM.IsSeasonalThemeActive
                )
                {
                    _mainVM.SeasonalAudioService.PlayCardSelect();
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
            _queuedSwaps.Count > 0 ? "Deselect all textures" : "Select all available textures";
        public string SelectAllIcon =>
            _queuedSwaps.Count > 0 ? "DismissCircle24" : "CheckmarkCircle24";

        public ICommand SwapCommand { get; }
        public ICommand SelectAllAvailableCommand { get; }
        public ICommand ToggleSkinSelectionCommand { get; }
        public ICommand SelectTagFilterCommand { get; }
        public ICommand SetGarageTierCommand { get; }
        public ICommand ClearCacheCommand => _mainVM.ClearCacheCommand;
        public ICommand RestoreCommand => _mainVM.RestoreCommand;

        public SkinsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;

            string savedTier = _mainVM.Settings.LastSelectedGarageTier ?? "M3";
            _selectedGarageTier =
                _mainVM.IsHtml5Client
                && (
                    savedTier.Equals("M2", StringComparison.OrdinalIgnoreCase)
                    || (
                        savedTier.Equals("M1", StringComparison.OrdinalIgnoreCase)
                        && _mainVM.EquipmentTargetService.HasAnyMappedTargets("M1")
                    )
                )
                    ? savedTier.ToUpperInvariant()
                    : "M3";

            SwapCommand = new AsyncRelayCommand(
                ExecuteSwap,
                _ => !_mainVM.IsLoading && _queuedSwaps.Count > 0
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
            SetGarageTierCommand = new RelayCommand(param =>
            {
                if (param is string tier && !string.IsNullOrWhiteSpace(tier))
                {
                    string normalized = tier.Trim().ToUpperInvariant();
                    if (normalized == "M2" && !IsM2TierEnabled)
                    {
                        return;
                    }
                    if (normalized == "M1" && !IsM1TierEnabled)
                    {
                        return;
                    }
                    if (normalized is "M3" or "M2" or "M1")
                    {
                        SelectedGarageTier = normalized;
                    }
                }
            });

            _mainVM.ActiveClientChanged += (_, newClient) =>
            {
                if (newClient != ClientType.Html5)
                {
                    List<string> toRemove =
                    [
                        .. _queuedSwaps
                            .Where(kvp =>
                                !kvp.Value.TargetTier.Equals(
                                    "M3",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            .Select(kvp => kvp.Key),
                    ];
                    foreach (string key in toRemove)
                    {
                        _ = _queuedSwaps.Remove(key);
                    }

                    if (_selectedGarageTier != "M3")
                    {
                        SelectedGarageTier = "M3";
                    }
                    else
                    {
                        RefreshTierStates();
                    }
                }

                NotifyTierPropertiesChanged();
                OnPropertyChanged(nameof(SelectedQueueSummary));
                OnPropertyChanged(nameof(SelectAllText));
                OnPropertyChanged(nameof(SelectAllIcon));
                CommandManager.InvalidateRequerySuggested();
            };
        }

        public void OnSkinsLoaded()
        {
            if (_mainVM.AllSkins != null)
            {
                foreach (SkinModel skin in _mainVM.AllSkins)
                {
                    skin.RefreshAvailableModifications(force: true);
                }
            }
            RevalidateQueuedSwaps();
            RefreshTierStates();
            InitializeCategories();
            FilterItems();
            NotifyTierPropertiesChanged();
        }

        private void NotifyTierPropertiesChanged()
        {
            OnPropertyChanged(nameof(IsGarageTierSelectorVisible));
            OnPropertyChanged(nameof(IsM3TierSelected));
            OnPropertyChanged(nameof(IsM2TierSelected));
            OnPropertyChanged(nameof(IsM1TierSelected));
            OnPropertyChanged(nameof(IsM2TierEnabled));
            OnPropertyChanged(nameof(IsM1TierEnabled));
            OnPropertyChanged(nameof(M2TierToolTip));
            OnPropertyChanged(nameof(M1TierToolTip));
            OnPropertyChanged(nameof(ShowNonM3Banner));
            OnPropertyChanged(nameof(IsNonM3PartialTargets));
            OnPropertyChanged(nameof(NonM3BannerText));
            OnPropertyChanged(nameof(IsXtQuickSelectVisible));
        }

        private static string MakeQueueKey(SkinModel skin, string tier)
        {
            return $"{skin.Category}:{skin.ItemName}:{skin.Name}@{tier.ToUpperInvariant()}";
        }

        private void RevalidateQueuedSwaps()
        {
            if (_queuedSwaps.Count == 0)
            {
                return;
            }

            List<string> keys = [.. _queuedSwaps.Keys];
            foreach (string key in keys)
            {
                QueuedSkinSwap q = _queuedSwaps[key];
                SkinTierResolution res = _mainVM.EquipmentTargetService.ResolveSkinForTier(
                    q.BaseSkin,
                    q.TargetTier
                );
                if (res.IsAvailable && res.ResolvedSkin != null)
                {
                    _queuedSwaps[key] = q with { ResolvedSkin = res.ResolvedSkin };
                }
                else
                {
                    _ = _queuedSwaps.Remove(key);
                }
            }

            OnPropertyChanged(nameof(SelectedQueueSummary));
            OnPropertyChanged(nameof(SelectAllText));
            OnPropertyChanged(nameof(SelectAllIcon));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshTierStates()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            foreach (SkinModel skin in _mainVM.AllSkins)
            {
                if (skin.Category is "Hulls" or "Turrets")
                {
                    SkinTierResolution res = _mainVM.EquipmentTargetService.ResolveSkinForTier(
                        skin,
                        SelectedGarageTier
                    );
                    skin.UsesM3ModelFallback = res.UsesM3ModelFallback;
                    skin.IsAvailableForActiveTier = res.IsAvailable;
                    skin.ActiveTierUnavailableReason = res.UnavailableReason ?? string.Empty;

                    string desiredMod =
                        SelectedGarageTier != "M3"
                        && skin.HasModificationAvailable(SelectedGarageTier)
                            ? SelectedGarageTier
                        : skin.IsM3Available ? "M3"
                        : skin.SelectedModification;
                    if (!string.IsNullOrEmpty(desiredMod))
                    {
                        skin.SelectedModification = desiredMod;
                    }

                    skin.IsSelected = _queuedSwaps.ContainsKey(
                        MakeQueueKey(skin, SelectedGarageTier)
                    );
                }
                else if (skin.Category != "Paints")
                {
                    skin.UsesM3ModelFallback = false;
                    skin.IsAvailableForActiveTier = true;
                    skin.ActiveTierUnavailableReason = string.Empty;
                    skin.IsSelected = _queuedSwaps.ContainsKey(MakeQueueKey(skin, "M3"));
                }
            }
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
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            string allLabel = $"All {SelectedCategory}";
            bool hideXtTargets = IsGarageTierSelectorVisible && SelectedGarageTier != "M3";
            IEnumerable<SkinModel> categorySkins = _mainVM.AllSkins.Where(s =>
                s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)
                && (SelectedItemName == allLabel || s.ItemName == SelectedItemName)
                && (!hideXtTargets || !s.IsXtTargetSkin)
            );

            HashSet<string> tagsInView = new(StringComparer.OrdinalIgnoreCase);
            foreach (SkinModel skin in categorySkins)
            {
                foreach (string tag in skin.ComputedTags)
                {
                    _ = tagsInView.Add(tag);
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
                    _ = tagsInView.Remove(std);
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
                bool hideXtTargets = IsGarageTierSelectorVisible && SelectedGarageTier != "M3";

                IEnumerable<SkinModel> matching = ShowOnlyGiftedSkins
                    ? _mainVM.AllSkins.Where(s =>
                        s.IsEventUnlocked
                        && (!hideXtTargets || !s.IsXtTargetSkin)
                        && (
                            string.IsNullOrEmpty(currentQuery)
                            || s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            || s.ItemName.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    : _mainVM.AllSkins.Where(s =>
                        s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase)
                        && (SelectedItemName == allLabel || s.ItemName == SelectedItemName)
                        && (!hideXtTargets || !s.IsXtTargetSkin)
                        && (
                            string.IsNullOrEmpty(currentQuery)
                            || s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            || s.ItemName.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            || s.ComputedTags.Any(t =>
                                t.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        && (
                            SelectedTagFilter == "All"
                            || s.ComputedTags.Contains(
                                SelectedTagFilter,
                                StringComparer.OrdinalIgnoreCase
                            )
                        )
                    );

                matching = SelectedSortOption switch
                {
                    "Name (Z-A)" => matching.OrderByDescending(s => s.Name),
                    "Tag / Theme" => matching
                        .OrderBy(s => s.PrimaryThemeTag ?? "zzz")
                        .ThenBy(s => s.Name),
                    "Premium First" => matching
                        .OrderByDescending(s => s.IsPremium)
                        .ThenBy(s => s.Name),
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

        private static bool TargetsOverlap(SkinModel a, SkinModel b)
        {
            return
                Html5ResourceMapper.TryDecodeBase64Target(
                    a.DetailsTarget,
                    out Html5ResourceTarget? decA
                )
                && Html5ResourceMapper.TryDecodeBase64Target(
                    b.DetailsTarget,
                    out Html5ResourceTarget? decB
                )
                && decA != null
                && decB != null
                ? decA.ResourceId == decB.ResourceId
                : (!string.IsNullOrEmpty(a.DetailsTarget) && a.DetailsTarget == b.DetailsTarget)
                    || (
                        !string.IsNullOrEmpty(a.LightmapTarget)
                        && a.LightmapTarget == b.LightmapTarget
                    )
                    || (!string.IsNullOrEmpty(a.AlphaTarget) && a.AlphaTarget == b.AlphaTarget);
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
            if (parameter is not SkinModel skin)
            {
                return;
            }

            string tier = skin.Category is "Hulls" or "Turrets" ? SelectedGarageTier : "M3";
            string queueKey = MakeQueueKey(skin, tier);

            if (_queuedSwaps.Remove(queueKey))
            {
                skin.IsSelected = false;
                Log.Information(
                    "Deselected skin: {SkinName} ({ItemName} {Tier})",
                    skin.Name,
                    skin.ItemName,
                    tier
                );
            }
            else
            {
                SkinTierResolution res = _mainVM.EquipmentTargetService.ResolveSkinForTier(
                    skin,
                    tier
                );
                if (!res.IsAvailable || res.ResolvedSkin == null)
                {
                    _ = _mainVM.NotificationService.ShowAsync(
                        $"Unavailable for {tier}",
                        res.UnavailableReason ?? $"'{skin.Name}' is not available for {tier}.",
                        ControlAppearance.Caution
                    );
                    return;
                }

                EvictOverlappingQueuedSwaps(res.ResolvedSkin, tier);
                _queuedSwaps[queueKey] = new QueuedSkinSwap(skin, tier, res.ResolvedSkin);
                skin.IsSelected = true;

                Log.Information(
                    "Selected skin: {SkinName} ({ItemName} {Tier}, mode={Mode})",
                    skin.Name,
                    skin.ItemName,
                    tier,
                    res.Mode
                );
            }

            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(SelectAllText));
            OnPropertyChanged(nameof(SelectAllIcon));
            OnPropertyChanged(nameof(SelectedQueueSummary));
        }

        private void EvictOverlappingQueuedSwaps(SkinModel resolvedCandidate, string activeTier)
        {
            List<string> conflictingKeys =
            [
                .. _queuedSwaps
                    .Where(kvp => TargetsOverlap(resolvedCandidate, kvp.Value.ResolvedSkin))
                    .Select(kvp => kvp.Key),
            ];

            foreach (string conflictKey in conflictingKeys)
            {
                QueuedSkinSwap removed = _queuedSwaps[conflictKey];
                _ = _queuedSwaps.Remove(conflictKey);

                string expectedVisibleTier = removed.BaseSkin.Category is "Hulls" or "Turrets"
                    ? SelectedGarageTier
                    : "M3";
                if (
                    removed.TargetTier.Equals(
                        expectedVisibleTier,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    removed.BaseSkin.IsSelected = false;
                }

                Log.Information(
                    "Deselected conflicting skin {OtherName} ({OtherTier}) because {CandidateName} ({ActiveTier}) was selected.",
                    removed.BaseSkin.Name,
                    removed.TargetTier,
                    resolvedCandidate.Name,
                    activeTier
                );
            }
        }

        private void ExecuteSelectAllAvailable(object? parameter)
        {
            string mode = parameter as string ?? "XT";

            if (mode == "Deselect")
            {
                Log.Information("Deselecting all textures.");
                _queuedSwaps.Clear();
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
                if (mode == "LC on XT" && SelectedGarageTier != "M3")
                {
                    _ = _mainVM.NotificationService.ShowAsync(
                        "Unavailable for " + SelectedGarageTier,
                        "LC on XT targets are only available in M3 mode.",
                        ControlAppearance.Info
                    );
                    return;
                }

                Log.Information(
                    "Selecting all {Mode} skins for tier {Tier}.",
                    mode,
                    SelectedGarageTier
                );

                List<SkinModel> toSelect = [];
                var groups = _mainVM
                    .AllSkins.Where(s => s.Category != "Paints")
                    .GroupBy(s => new { s.Category, s.ItemName });

                foreach (var group in groups)
                {
                    bool hideXtForGroup =
                        group.Key.Category is "Hulls" or "Turrets" && SelectedGarageTier != "M3";
                    IEnumerable<SkinModel> pool = hideXtForGroup
                        ? group.Where(s => !s.IsXtTargetSkin)
                        : group;

                    List<SkinModel> matches =
                        mode == "Legacy"
                            ?
                            [
                                .. pool.Where(s =>
                                    (
                                        s.Name.EndsWith(" LC", StringComparison.OrdinalIgnoreCase)
                                        || s.Name.EndsWith(
                                            " Legacy",
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                    )
                                    && !s.Name.EndsWith(
                                        " LC on XT",
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                ),
                            ]
                        : mode == "XT"
                            ?
                            [
                                .. pool.Where(s =>
                                    s.Name.EndsWith(" XT", StringComparison.OrdinalIgnoreCase)
                                ),
                            ]
                        : mode == "LC on XT"
                            ?
                            [
                                .. pool.Where(s =>
                                    s.Name.EndsWith(" LC on XT", StringComparison.OrdinalIgnoreCase)
                                ),
                            ]
                        : mode == "Demonic"
                            ?
                            [
                                .. pool.Where(s =>
                                    s.Name.Contains("Demonic", StringComparison.OrdinalIgnoreCase)
                                    || s.Name.Contains(" DC", StringComparison.OrdinalIgnoreCase)
                                ),
                            ]
                        : mode == "Old"
                            ? group.Key.Category == "Supplies"
                                    ?
                                    [
                                        .. pool.Where(s =>
                                            s.Name.Contains(
                                                "Old",
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        ),
                                    ]
                                : []
                        : mode == "New"
                            ? group.Key.Category == "Supplies"
                                    ?
                                    [
                                        .. pool.Where(s =>
                                            !s.Name.Contains(
                                                "Old",
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        ),
                                    ]
                                : []
                        : [];
                    if (matches.Count > 0)
                    {
                        toSelect.AddRange(matches);
                    }
                }

                int selectedCount = 0;
                foreach (SkinModel skin in toSelect)
                {
                    string tier = skin.Category is "Hulls" or "Turrets" ? SelectedGarageTier : "M3";
                    SkinTierResolution res = _mainVM.EquipmentTargetService.ResolveSkinForTier(
                        skin,
                        tier
                    );
                    if (!res.IsAvailable || res.ResolvedSkin == null)
                    {
                        continue;
                    }

                    EvictOverlappingQueuedSwaps(res.ResolvedSkin, tier);
                    _queuedSwaps[MakeQueueKey(skin, tier)] = new QueuedSkinSwap(
                        skin,
                        tier,
                        res.ResolvedSkin
                    );
                    skin.IsSelected = true;
                    selectedCount++;
                }

                _ = _mainVM.NotificationService.ShowAsync(
                    "Selected All",
                    $"Selected {selectedCount} textures ({mode}, {SelectedGarageTier}) for batch application.",
                    ControlAppearance.Info
                );
            }

            CommandManager.InvalidateRequerySuggested();
            OnPropertyChanged(nameof(SelectAllText));
            OnPropertyChanged(nameof(SelectAllIcon));
            OnPropertyChanged(nameof(SelectedQueueSummary));
        }

        private async Task ExecuteSwap(object? parameter)
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            List<SkinModel> skinsToApply = [.. _queuedSwaps.Values.Select(q => q.ResolvedSkin)];
            if (skinsToApply.Count == 0 && SelectedSkin != null)
            {
                string tier = SelectedSkin.Category is "Hulls" or "Turrets"
                    ? SelectedGarageTier
                    : "M3";
                SkinTierResolution res = _mainVM.EquipmentTargetService.ResolveSkinForTier(
                    SelectedSkin,
                    tier
                );
                if (!res.IsAvailable || res.ResolvedSkin == null)
                {
                    await _mainVM.NotificationService.ShowAsync(
                        $"Unavailable for {tier}",
                        res.UnavailableReason
                            ?? $"'{SelectedSkin.Name}' is not available for {tier}.",
                        ControlAppearance.Caution
                    );
                    return;
                }
                skinsToApply.Add(res.ResolvedSkin);
            }

            if (skinsToApply.Count == 0)
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

            if (skinsToApply.Any(s => s.ItemName == "Gold Box"))
            {
                bool isThematicGoldActive =
                    _mainVM.SeasonalThemeService.IsThematicGoldActive
                    && skinsToApply.Any(s =>
                        s.ItemName == "Gold Box" && !string.IsNullOrEmpty(s.SecondaryDetailsTarget)
                    );

                if (isThematicGoldActive)
                {
                    GoldBoxTargetDialog dialog = new() { Owner = Application.Current?.MainWindow };

                    if (dialog.ShowDialog() != true)
                    {
                        Log.Information("User canceled gold box target selection.");
                        return;
                    }

                    skinsToApply = [.. skinsToApply];
                    for (int i = 0; i < skinsToApply.Count; i++)
                    {
                        if (skinsToApply[i].ItemName == "Gold Box")
                        {
                            skinsToApply[i] = CloneSkinForTargetChoice(
                                skinsToApply[i],
                                dialog.SelectedChoice
                            );
                        }
                    }
                }
                else
                {
                    skinsToApply = [.. skinsToApply];
                    for (int i = 0; i < skinsToApply.Count; i++)
                    {
                        if (skinsToApply[i].ItemName == "Gold Box")
                        {
                            skinsToApply[i] = CloneSkinForTargetChoice(
                                skinsToApply[i],
                                GoldBoxTargetChoice.NormalOnly
                            );
                        }
                    }
                }
            }

            List<SkinModel> highDetailSkins = [.. skinsToApply.Where(IsHighDetailSkin)];
            if (highDetailSkins.Count > 0)
            {
                string skinList =
                    highDetailSkins.Count > 5
                        ? string.Join("\n• ", highDetailSkins.Take(5).Select(s => s.Name))
                            + $"\n\n ... and {highDetailSkins.Count - 5} more skins"
                        : string.Join("\n• ", highDetailSkins.Select(s => s.Name));

                MessageBox warnBox = new()
                {
                    Title = "Quick heads-up",
                    Content =
                        $"You're applying high-detail skin textures:\n\n• {skinList}\n\nA few things to keep in mind:\n• The game might take a bit longer to load.\n• You might face occasional FPS stutters or drops in battle.\n\nWant to apply anyway?",
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

                List<SkinModel> blockedPremiumSkins = skinsToApply
                    .Where(s =>
                        s.IsPremium
                        && !_mainVM.PremiumService.UnlockedChristmasSkinIds.Contains(
                            s.Name,
                            StringComparer.OrdinalIgnoreCase
                        )
                    )
                    .ToList();

                if (
                    blockedPremiumSkins.Count == 1
                    && !_mainVM.PremiumService.IsPremiumUnlocked
                    && _mainVM.ChatTabVM.HasPendingSkinVoucher
                )
                {
                    SkinModel chosen = blockedPremiumSkins[0];
                    (bool claimed, string? unlockedId, string? claimErr) =
                        await _mainVM.ChatService.ClaimSkinVoucherAsync(chosen.Name, chosen.Name);
                    if (claimed && !string.IsNullOrEmpty(unlockedId))
                    {
                        _mainVM.PremiumService.SetUnlockedChristmasSkinId(unlockedId);
                        blockedPremiumSkins.Clear();
                    }
                    else
                    {
                        _mainVM.IsLoading = false;
                        await _mainVM.NotificationService.ShowAsync(
                            "Voucher Claim Failed",
                            claimErr ?? "Could not claim your 1-Skin Voucher.",
                            ControlAppearance.Danger
                        );
                        return;
                    }
                }

                if (blockedPremiumSkins.Count > 0 && !_mainVM.PremiumService.IsPremiumUnlocked)
                {
                    _mainVM.IsLoading = false;
                    IEnumerable<string> premiumSkinNames = blockedPremiumSkins.Select(s => s.Name);
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
                    if (_mainVM.IsSeasonalThemeActive)
                    {
                        _mainVM.SeasonalAudioService.PlayApplySuccess();
                    }

                    _queuedSwaps.Clear();
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
                OnPropertyChanged(nameof(SelectedQueueSummary));
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

            bool isDemonic =
                tags.Contains("Demonic", StringComparer.OrdinalIgnoreCase)
                || name.Contains("Demonic", StringComparison.OrdinalIgnoreCase)
                || name.Contains(" DC", StringComparison.OrdinalIgnoreCase);

            bool isXtHd =
                name.Contains("XT HD", StringComparison.OrdinalIgnoreCase)
                || (
                    tags.Contains("XT", StringComparer.OrdinalIgnoreCase)
                    && tags.Contains("HD", StringComparer.OrdinalIgnoreCase)
                )
                || (
                    name.Contains("XT", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("HD", StringComparison.OrdinalIgnoreCase)
                );

            bool isTankiX =
                tags.Contains("Tanki X", StringComparer.OrdinalIgnoreCase)
                || name.Contains("Tanki X", StringComparison.OrdinalIgnoreCase)
                || name.Contains("TX", StringComparison.OrdinalIgnoreCase);

            return isDemonic || isXtHd || isTankiX;
        }

        private static SkinModel CloneSkinForTargetChoice(
            SkinModel original,
            GoldBoxTargetChoice choice
        )
        {
            SkinModel copy = new()
            {
                Category = original.Category,
                SubCategory = original.SubCategory,
                ItemName = original.ItemName,
                Name = original.Name,
                SourceFolder = original.SourceFolder,
                PreviewImage = original.PreviewImage,
                LightmapTarget = original.LightmapTarget,
                AlphaTarget = original.AlphaTarget,
                TracksTarget = original.TracksTarget,
                ImagesXmlTarget = original.ImagesXmlTarget,
                IsPremium = original.IsPremium,
                IsExperimental = original.IsExperimental,
                SelectedModification = original.SelectedModification,
                TargetTier = original.TargetTier,
                FallbackModelPath = original.FallbackModelPath,
            };

            switch (choice)
            {
                case GoldBoxTargetChoice.NormalOnly:
                    copy.DetailsTarget = original.DetailsTarget;
                    copy.ModelTarget = original.ModelTarget;
                    copy.SecondaryDetailsTarget = string.Empty;
                    copy.SecondaryModelTarget = string.Empty;
                    break;

                case GoldBoxTargetChoice.ThematicOnly:
                    copy.DetailsTarget = original.SecondaryDetailsTarget;
                    copy.ModelTarget = original.SecondaryModelTarget;
                    copy.SecondaryDetailsTarget = string.Empty;
                    copy.SecondaryModelTarget = string.Empty;
                    break;

                case GoldBoxTargetChoice.Both:
                default:
                    copy.DetailsTarget = original.DetailsTarget;
                    copy.ModelTarget = original.ModelTarget;
                    copy.SecondaryDetailsTarget = original.SecondaryDetailsTarget;
                    copy.SecondaryModelTarget = original.SecondaryModelTarget;
                    break;
            }

            return copy;
        }
    }
}
