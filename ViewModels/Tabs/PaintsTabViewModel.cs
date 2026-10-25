using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Services;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public class PaintsTabViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private readonly JsonSerializerOptions options = new() { WriteIndented = true };
        private string _selectedItemName = string.Empty;
        private SkinModel? _selectedSkin;
        private InGamePaintModel? _selectedInGamePaint;
        private string _customPaintSearchQuery = string.Empty;
        private string _inGamePaintSearchQuery = string.Empty;

        public ObservableCollection<InGamePaintModel> InGamePaints { get; } = [];
        public List<InGamePaintModel> AllInGamePaints { get; set; } = [];
        public ObservableCollection<string> ItemNames { get; } = [];
        public ObservableCollection<SkinModel> FilteredSkins { get; } = [];
        public ObservableCollection<List<SkinModel>> FilteredSkinsRows { get; } = [];

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

        public InGamePaintModel? SelectedInGamePaint
        {
            get => _selectedInGamePaint;
            set
            {
                if (SetProperty(ref _selectedInGamePaint, value))
                {
                    OnPropertyChanged(nameof(ShowCustomPaints));
                    OnPropertyChanged(nameof(ShowInGamePaints));
                    if (value != null)
                    {
                        FilterItems();
                    }
                }
            }
        }

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

        public bool ShowCustomPaints => SelectedInGamePaint != null;
        public bool ShowInGamePaints => SelectedInGamePaint == null;
        public ICommand UploadCustomPaintCommand { get; }
        public ICommand SwapCommand { get; }
        public ICommand GoBackToInGamePaintsCommand { get; }
        public ICommand ToggleSkinSelectionCommand { get; }
        public ICommand ClearCacheCommand => _mainVM.ClearCacheCommand;
        public ICommand RestoreCommand => _mainVM.RestoreCommand;

        public PaintsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            UploadCustomPaintCommand = new AsyncRelayCommand(
                ExecuteUploadCustomPaintAsync,
                _ => !_mainVM.IsLoading
            );
            SwapCommand = new AsyncRelayCommand(
                ExecuteSwap,
                _ =>
                    !_mainVM.IsLoading
                    && _mainVM.AllSkins.Any(s => s.IsSelected && s.Category == "Paints")
            );
            GoBackToInGamePaintsCommand = new RelayCommand(_ => SelectedInGamePaint = null);
            ToggleSkinSelectionCommand = new RelayCommand(ExecuteToggleSkinSelection);
        }

        public void OnSkinsLoaded()
        {
            FilterItems();
        }

        public async Task LoadInGamePaintsAsync()
        {
            try
            {
                _mainVM.IsLoading = true;
                List<InGamePaintModel> paints = await Task.Run(() =>
                    _mainVM.SkinSyncService.LoadInGamePaints()
                );
                AllInGamePaints = paints;
                FilterInGamePaints();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load in-game paints.");
            }
            finally
            {
                _mainVM.IsLoading = false;
            }
        }

        public void FilterInGamePaints()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                InGamePaints.Clear();
                IEnumerable<InGamePaintModel> filtered = AllInGamePaints;
                if (!string.IsNullOrWhiteSpace(InGamePaintSearchQuery))
                {
                    filtered = filtered.Where(p =>
                        p.Name.Contains(InGamePaintSearchQuery, StringComparison.OrdinalIgnoreCase)
                    );
                }
                foreach (InGamePaintModel paint in filtered.OrderBy(p => p.Name))
                {
                    InGamePaints.Add(paint);
                }
            });
        }

        private void FilterItems()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_mainVM.AllSkins == null)
                {
                    return;
                }

                string allLabel = "All Paints";
                ItemNames.Clear();
                ItemNames.Add(allLabel);

                IOrderedEnumerable<string> matchingItems = _mainVM
                    .AllSkins.Where(s =>
                        s.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                    )
                    .Select(s => s.ItemName)
                    .Distinct()
                    .OrderBy(name => name);

                foreach (string itemName in matchingItems)
                {
                    ItemNames.Add(itemName);
                }

                SelectedItemName = ItemNames.Contains("Custom Paints")
                    ? "Custom Paints"
                    : ItemNames.First();
                FilterSkins();
            });
        }

        public void FilterSkins()
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_mainVM.AllSkins == null)
                {
                    return;
                }

                FilteredSkins.Clear();
                string allLabel = "All Paints";
                string currentQuery = CustomPaintSearchQuery?.Trim() ?? string.Empty;

                IOrderedEnumerable<SkinModel> matchingSkins = _mainVM
                    .AllSkins.Where(s =>
                        s.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                        && (SelectedItemName == allLabel || s.ItemName == SelectedItemName)
                        && (
                            string.IsNullOrEmpty(currentQuery)
                            || s.Name.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                            || s.ItemName.Contains(currentQuery, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    .OrderBy(s => s.Name);

                foreach (SkinModel skin in matchingSkins)
                {
                    FilteredSkins.Add(skin);
                }

                UpdateFilteredSkinsRows();
            });
        }

        private bool _isFoldersPaneVisible = true;
        public bool IsFoldersPaneVisible
        {
            get => _isFoldersPaneVisible;
            set => SetProperty(ref _isFoldersPaneVisible, value);
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
                    $"Toggled selection for paint: {skin.Name}. New state: {skin.IsSelected}"
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
                                $"Deselected conflicting paint {otherSkin.Name} because {skin.Name} was selected."
                            );
                        }
                    }
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task ExecuteUploadCustomPaintAsync(object? parameter)
        {
            Microsoft.Win32.OpenFileDialog dialog = new()
            {
                Title = "Upload Custom Paint Texture",
                Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg|All Files (*.*)|*.*",
                Multiselect = false,
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                try
                {
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext is not ".png" and not ".jpg" and not ".jpeg")
                    {
                        await _mainVM.NotificationService.ShowAsync(
                            "Invalid File Type",
                            "Only .png, .jpg, and .jpeg files are allowed.",
                            ControlAppearance.Danger
                        );
                        return;
                    }

                    FileInfo fileInfo = new(filePath);
                    int maximumFileLength = 5 * 1024 * 1024;
                    if (fileInfo.Length > maximumFileLength)
                    {
                        await _mainVM.NotificationService.ShowAsync(
                            "File Too Large",
                            "The texture file size must be less than 5 MB.",
                            ControlAppearance.Danger
                        );
                        return;
                    }

                    int width = 0;
                    int height = 0;
                    using (
                        FileStream stream = new(
                            filePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read
                        )
                    )
                    {
                        BitmapDecoder decoder = BitmapDecoder.Create(
                            stream,
                            BitmapCreateOptions.DelayCreation,
                            BitmapCacheOption.None
                        );
                        if (decoder.Frames.Count > 0)
                        {
                            BitmapFrame frame = decoder.Frames[0];
                            width = frame.PixelWidth;
                            height = frame.PixelHeight;
                        }
                    }

                    if (width != 256 || height != 256)
                    {
                        Log.Warning(
                            $"Invalid texture dimensions {width}x{height}. Must be exactly 256x256."
                        );
                        await _mainVM.NotificationService.ShowAsync(
                            "Invalid Dimensions",
                            $"Invalid texture dimensions {width}x{height}. Must be exactly 256x256.",
                            ControlAppearance.Danger
                        );
                        return;
                    }

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string uploadDirRelative = Path.Combine("Textures", "Paints", "Uploaded");
                    string uploadDir = Path.Combine(baseDir, uploadDirRelative);
                    _ = Directory.CreateDirectory(uploadDir);

                    string fileName = Path.GetFileName(filePath);
                    string sanitizedName = string.Join(
                        "_",
                        fileName.Split(Path.GetInvalidFileNameChars())
                    );
                    string targetFilePath = Path.Combine(uploadDir, sanitizedName);

                    await using (
                        FileStream sourceStream = new(
                            filePath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            8192,
                            true
                        )
                    )
                    await using (
                        FileStream targetStream = new(
                            targetFilePath,
                            FileMode.Create,
                            FileAccess.Write,
                            FileShare.None,
                            8192,
                            true
                        )
                    )
                    {
                        await sourceStream.CopyToAsync(targetStream);
                    }
                    Log.Information(
                        $"Successfully copied uploaded paint from {filePath} to {targetFilePath}"
                    );

                    string jsonPath = Path.Combine(baseDir, Constants.PaintsSkinsJson);
                    List<SkinModel> paints = [];
                    if (File.Exists(jsonPath))
                    {
                        try
                        {
                            string json = await File.ReadAllTextAsync(jsonPath);
                            paints = JsonSerializer.Deserialize<List<SkinModel>>(json) ?? [];
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to load skins_paints.json during upload.");
                        }
                    }

                    string skinName = Path.GetFileNameWithoutExtension(sanitizedName);
                    if (
                        !paints.Any(p =>
                            p.Name.Equals(skinName, StringComparison.OrdinalIgnoreCase)
                            && p.ItemName == "Uploaded"
                        )
                    )
                    {
                        paints.Add(
                            new SkinModel
                            {
                                Category = "Paints",
                                ItemName = "Uploaded",
                                Name = skinName,
                                SourceFolder = uploadDirRelative.Replace("\\", "/"),
                                PreviewImage = Path.Combine(uploadDirRelative, sanitizedName)
                                    .Replace("\\", "/"),
                            }
                        );

                        string updatedJson = JsonSerializer.Serialize(paints, options);
                        await File.WriteAllTextAsync(jsonPath, updatedJson);
                        Log.Information(
                            $"Registered new custom paint '{skinName}' in skins_paints.json"
                        );
                    }

                    await _mainVM.LoadSkinsAsync();
                    await _mainVM.NotificationService.ShowAsync(
                        "Upload Success",
                        $"Custom paint '{skinName}' was successfully uploaded!",
                        ControlAppearance.Success
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to process custom paint upload from {filePath}");
                    await _mainVM.NotificationService.ShowAsync(
                        "Upload Error",
                        $"Failed to process custom paint: {ex.Message}",
                        ControlAppearance.Danger
                    );
                }
            }
        }

        private async Task ExecuteSwap(object? parameter)
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            List<SkinModel> selectedSkins =
            [
                .. _mainVM.AllSkins.Where(s => s.IsSelected && s.Category == "Paints"),
            ];

            if (selectedSkins.Count == 0 && SelectedSkin == null)
            {
                await _mainVM.NotificationService.ShowAsync(
                    LocalizationService.Instance.GetString("Loc_Msg_Warning"),
                    LocalizationService.Instance.GetString("Loc_Msg_SelectPaintWarning"),
                    ControlAppearance.Danger
                );
                return;
            }

            if (SelectedInGamePaint == null)
            {
                await _mainVM.NotificationService.ShowAsync(
                    LocalizationService.Instance.GetString("Loc_Msg_Warning"),
                    LocalizationService.Instance.GetString("Loc_Msg_SelectInGamePaintWarning"),
                    ControlAppearance.Danger
                );
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                _mainVM.IsLoading = true;

                List<SkinModel> skinsToApply =
                    selectedSkins.Count != 0 ? selectedSkins : [SelectedSkin!];

                if (skinsToApply.Any(s => s.IsPremium) && !_mainVM.PremiumService.IsPremiumUnlocked)
                {
                    _mainVM.IsLoading = false;
                    IEnumerable<string> premiumSkinNames = skinsToApply
                        .Where(s => s.IsPremium)
                        .Select(s => s.Name);
                    await _mainVM.NotificationService.ShowAsync(
                        "Premium Required",
                        $"The following paint(s) require an active Premium license:\n- {string.Join("\n- ", premiumSkinNames)}\n\nPlease activate Premium in Settings to use them.",
                        ControlAppearance.Caution
                    );
                    return;
                }

                List<SkinModel> swapCopies = [];

                foreach (SkinModel skin in skinsToApply)
                {
                    swapCopies.Add(
                        new SkinModel
                        {
                            Category = skin.Category,
                            ItemName = skin.ItemName,
                            Name = skin.Name,
                            SourceFolder = skin.SourceFolder,
                            DetailsTarget = SelectedInGamePaint.TargetUrl,
                            PreviewImage = skin.PreviewImage,
                        }
                    );
                }

                string skinMessage = swapCopies.Count > 1 ? "skins" : "skin";
                string textureMessage = swapCopies.Count > 1 ? "textures" : "texture";

                _mainVM.UpdateStatus = $"Applying {swapCopies.Count} {textureMessage} to cache...";
                Log.Information(
                    $"Writing {swapCopies.Count} paints directly to cache disk path: {_mainVM.CachePath}"
                );

                string? error = await Task.Run(() =>
                    _mainVM.SwapService.SwapBatch(
                        _mainVM.CachePath,
                        swapCopies,
                        SelectedInGamePaint.Name
                    )
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
                        if (skin.Category == "Paints")
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
                Log.Error(ex, "Failed to apply paints.");
                notificationTitle = "Error";
                notificationMessage = $"Failed to apply paints: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                _mainVM.IsLoading = false;
                _mainVM.UpdateStatus = string.Empty;
                CommandManager.InvalidateRequerySuggested();
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
    }
}
