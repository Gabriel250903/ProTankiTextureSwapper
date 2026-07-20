using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Views;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;

namespace TextureSwapper.ViewModels
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

        private string _aiPrompt = string.Empty;
        private bool _isGenerating;
        private byte[]? _generatedImageBytes;
        private ImageSource? _generatedImageSource;
        private DateTime _lastGenerationTime = DateTime.MinValue;

        public string AiPrompt
        {
            get => _aiPrompt;
            set => SetProperty(ref _aiPrompt, value);
        }

        public bool IsGenerating
        {
            get => _isGenerating;
            set => SetProperty(ref _isGenerating, value);
        }

        public ImageSource? GeneratedImageSource
        {
            get => _generatedImageSource;
            set => SetProperty(ref _generatedImageSource, value);
        }

        public bool HasGeneratedTexture => _generatedImageBytes != null;

        public ICommand UploadCustomPaintCommand { get; }
        public ICommand SwapCommand { get; }
        public ICommand GoBackToInGamePaintsCommand { get; }
        public ICommand ToggleSkinSelectionCommand { get; }
        public ICommand GenerateAiTextureCommand { get; }
        public ICommand SaveGeneratedTextureCommand { get; }
        public ICommand PreviewGeneratedTextureCommand { get; }

        public PaintsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            UploadCustomPaintCommand = new AsyncRelayCommand(ExecuteUploadCustomPaintAsync, _ => !_mainVM.IsLoading);
            SwapCommand = new AsyncRelayCommand(ExecuteSwap, _ => !_mainVM.IsLoading && _mainVM.AllSkins.Any(s => s.IsSelected && s.Category == "Paints"));
            GoBackToInGamePaintsCommand = new RelayCommand(_ => SelectedInGamePaint = null);
            ToggleSkinSelectionCommand = new RelayCommand(ExecuteToggleSkinSelection);
            GenerateAiTextureCommand = new AsyncRelayCommand(ExecuteGenerateAiTexture, _ => !_mainVM.IsLoading && !IsGenerating);
            SaveGeneratedTextureCommand = new AsyncRelayCommand(ExecuteSaveGeneratedTexture, _ => !_mainVM.IsLoading && HasGeneratedTexture);
            PreviewGeneratedTextureCommand = new RelayCommand(ExecutePreviewGeneratedTexture, _ => HasGeneratedTexture);
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
                List<InGamePaintModel> paints = await Task.Run(() => _mainVM.SkinSyncService.LoadInGamePaints());
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
            InGamePaints.Clear();
            IEnumerable<InGamePaintModel> filtered = AllInGamePaints;
            if (!string.IsNullOrWhiteSpace(InGamePaintSearchQuery))
            {
                filtered = filtered.Where(p => p.Name.Contains(InGamePaintSearchQuery, StringComparison.OrdinalIgnoreCase));
            }
            foreach (InGamePaintModel paint in filtered.OrderBy(p => p.Name))
            {
                InGamePaints.Add(paint);
            }
        }

        private void FilterItems()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            string allLabel = "All Paints";
            ItemNames.Clear();
            ItemNames.Add(allLabel);

            IOrderedEnumerable<string> matchingItems = _mainVM.AllSkins
                .Where(s => s.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                .Select(s => s.ItemName)
                .Distinct()
                .OrderBy(name => name);

            foreach (string itemName in matchingItems)
            {
                ItemNames.Add(itemName);
            }

            SelectedItemName = ItemNames.Contains("Custom Paints") ? "Custom Paints" : ItemNames.First();
            FilterSkins();
        }

        public void FilterSkins()
        {
            if (_mainVM.AllSkins == null)
            {
                return;
            }

            FilteredSkins.Clear();
            string allLabel = "All Paints";
            string currentQuery = CustomPaintSearchQuery?.Trim() ?? string.Empty;

            IOrderedEnumerable<SkinModel> matchingSkins = _mainVM.AllSkins
                .Where(s => s.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase) &&
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
                Log.Information($"Toggled selection for paint: {skin.Name}. New state: {skin.IsSelected}");

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
                            Log.Information($"Deselected conflicting paint {otherSkin.Name} because {skin.Name} was selected.");
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
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                try
                {
                    string ext = Path.GetExtension(filePath).ToLowerInvariant();
                    if (ext is not ".png" and not ".jpg" and not ".jpeg")
                    {
                        await _mainVM.NotificationService.ShowAsync("Invalid File Type", "Only .png, .jpg, and .jpeg files are allowed.", ControlAppearance.Danger);
                        return;
                    }

                    FileInfo fileInfo = new(filePath);
                    int maximumFileLength = 5 * 1024 * 1024;
                    if (fileInfo.Length > maximumFileLength)
                    {
                        await _mainVM.NotificationService.ShowAsync("File Too Large", "The texture file size must be less than 5 MB.", ControlAppearance.Danger);
                        return;
                    }

                    int width = 0;
                    int height = 0;
                    using (FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                        if (decoder.Frames.Count > 0)
                        {
                            BitmapFrame frame = decoder.Frames[0];
                            width = frame.PixelWidth;
                            height = frame.PixelHeight;
                        }
                    }

                    if (width != 256 || height != 256)
                    {
                        Log.Warning($"Invalid texture dimensions {width}x{height}. Must be exactly 256x256.");
                        await _mainVM.NotificationService.ShowAsync("Invalid Dimensions", $"Invalid texture dimensions {width}x{height}. Must be exactly 256x256.", ControlAppearance.Danger);
                        return;
                    }

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string uploadDirRelative = Path.Combine("Textures", "Paints", "Uploaded");
                    string uploadDir = Path.Combine(baseDir, uploadDirRelative);
                    _ = Directory.CreateDirectory(uploadDir);

                    string fileName = Path.GetFileName(filePath);
                    string sanitizedName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
                    string targetFilePath = Path.Combine(uploadDir, sanitizedName);

                    File.Copy(filePath, targetFilePath, true);
                    Log.Information($"Successfully copied uploaded paint from {filePath} to {targetFilePath}");

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
                    if (!paints.Any(p => p.Name.Equals(skinName, StringComparison.OrdinalIgnoreCase) && p.ItemName == "Uploaded"))
                    {
                        paints.Add(new SkinModel
                        {
                            Category = "Paints",
                            ItemName = "Uploaded",
                            Name = skinName,
                            SourceFolder = uploadDirRelative.Replace("\\", "/"),
                            PreviewImage = Path.Combine(uploadDirRelative, sanitizedName).Replace("\\", "/")
                        });

                        string updatedJson = JsonSerializer.Serialize(paints, options);
                        await File.WriteAllTextAsync(jsonPath, updatedJson);
                        Log.Information($"Registered new custom paint '{skinName}' in skins_paints.json");
                    }

                    await _mainVM.LoadSkinsAsync();
                    await _mainVM.NotificationService.ShowAsync("Upload Success", $"Custom paint '{skinName}' was successfully uploaded!", ControlAppearance.Success);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to process custom paint upload from {filePath}");
                    await _mainVM.NotificationService.ShowAsync("Upload Error", $"Failed to process custom paint: {ex.Message}", ControlAppearance.Danger);
                }
            }
        }

        private async Task ExecuteSwap(object? parameter)
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            List<SkinModel> selectedSkins = [.. _mainVM.AllSkins.Where(s => s.IsSelected && s.Category == "Paints")];

            if (selectedSkins.Count == 0 && SelectedSkin == null)
            {
                await _mainVM.NotificationService.ShowAsync("Error", "Please select at least one skin first.", ControlAppearance.Danger);
                return;
            }

            if (SelectedInGamePaint == null)
            {
                await _mainVM.NotificationService.ShowAsync("Error", "Please select an in-game paint first.", ControlAppearance.Danger);
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                _mainVM.IsLoading = true;

                List<SkinModel> skinsToApply = selectedSkins.Count != 0 ? selectedSkins : [SelectedSkin!];
                List<SkinModel> swapCopies = [];

                foreach (SkinModel skin in skinsToApply)
                {
                    swapCopies.Add(new SkinModel
                    {
                        Category = skin.Category,
                        ItemName = skin.ItemName,
                        Name = skin.Name,
                        SourceFolder = skin.SourceFolder,
                        DetailsTarget = SelectedInGamePaint.TargetUrl,
                        PreviewImage = skin.PreviewImage
                    });
                }

                string skinMessage = swapCopies.Count > 1 ? "skins" : "skin";
                string textureMessage = swapCopies.Count > 1 ? "textures" : "texture";

                _mainVM.UpdateStatus = $"Applying {swapCopies.Count} {textureMessage} to cache...";
                Log.Information($"Writing {swapCopies.Count} paints directly to cache disk path: {_mainVM.CachePath}");

                string? error = await Task.Run(() => _mainVM.SwapService.SwapBatch(_mainVM.CachePath, swapCopies, SelectedInGamePaint.Name));
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
                        if (skin.Category == "Paints")
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
                await _mainVM.NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }

        private async Task ExecuteGenerateAiTexture(object? parameter)
        {
            TimeSpan timeSinceLastGen = DateTime.Now - _lastGenerationTime;
            if (timeSinceLastGen < TimeSpan.FromMinutes(1))
            {
                int remainingSeconds = (int)(60 - timeSinceLastGen.TotalSeconds);
                await _mainVM.NotificationService.ShowAsync("Rate Limit", $"Please wait {remainingSeconds} seconds before generating another texture.", ControlAppearance.Caution);
                return;
            }

            if (string.IsNullOrWhiteSpace(_mainVM.Settings.HuggingFaceToken))
            {
                MessageBox messageBox = new()
                {
                    Title = "API Token Required",
                    Content = "To use the AI Texture Generator, please input your free Hugging Face API Token in the app Settings.\n\nWould you like to open Settings now?",
                    PrimaryButtonText = "Open Settings",
                    CloseButtonText = "Cancel",
                    SecondaryButtonText = ""
                };

                MessageBoxResult result = await messageBox.ShowDialogAsync();
                if (result == MessageBoxResult.Primary)
                {
                    _mainVM.OpenSettingsCommand.Execute(null);
                }
                return;
            }

            if (string.IsNullOrWhiteSpace(AiPrompt))
            {
                await _mainVM.NotificationService.ShowAsync("Prompt Required", "Please enter a short description for your texture first.", ControlAppearance.Caution);
                return;
            }

            try
            {
                IsGenerating = true;
                _mainVM.UpdateStatus = "Generating AI texture...";

                byte[]? data = await _mainVM.AiTextureService.GenerateTextureAsync(AiPrompt, _mainVM.Settings.HuggingFaceToken);
                if (data != null && data.Length > 0)
                {
                    _lastGenerationTime = DateTime.Now;
                    _generatedImageBytes = data;
                    GeneratedImageSource = BytesToImage(data);
                    OnPropertyChanged(nameof(HasGeneratedTexture));
                    await _mainVM.NotificationService.ShowAsync("Generation Success", "Your AI texture has been generated! You can now preview and save it.", ControlAppearance.Success);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AI generation failed.");
                await _mainVM.NotificationService.ShowAsync("Generation Error", ex.Message, ControlAppearance.Danger);
            }
            finally
            {
                IsGenerating = false;
                _mainVM.UpdateStatus = string.Empty;
            }
        }

        private async Task ExecuteSaveGeneratedTexture(object? parameter)
        {
            if (_generatedImageBytes == null || _generatedImageBytes.Length == 0)
            {
                return;
            }

            MessageBox messageBox = new()
            {
                Title = "Save Paint Options",
                Content = "How would you like to save this generated paint?\n\n- 'Import to App' will register it directly as a usable paint in your list.\n- 'Save to PC' will let you save the image file anywhere on your computer.",
                PrimaryButtonText = "Import to App",
                SecondaryButtonText = "Save to PC",
                CloseButtonText = "Cancel",
                MaxWidth = 450
            };

            MessageBoxResult result = await messageBox.ShowDialogAsync();
            if (result == MessageBoxResult.None)
            {
                return;
            }

            if (result == MessageBoxResult.Primary)
            {
                string defaultName = string.IsNullOrWhiteSpace(AiPrompt)
                    ? "ai_paint"
                    : string.Join("_", AiPrompt.Split(Path.GetInvalidFileNameChars()));

                if (defaultName.Length > 25)
                {
                    defaultName = defaultName[..25];
                }

                InputDialog inputDialog = new(defaultName)
                {
                    Owner = Application.Current.MainWindow
                };

                if (inputDialog.ShowDialog() != true)
                {
                    return;
                }

                string customName = inputDialog.InputText;

                try
                {
                    _mainVM.IsLoading = true;
                    byte[] resizedBytes = await ResizeImageBytesAsync(_generatedImageBytes, 256, 256);

                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string uploadDirRelative = Path.Combine("Textures", "Paints", "Uploaded");
                    string uploadDir = Path.Combine(baseDir, uploadDirRelative);
                    _ = Directory.CreateDirectory(uploadDir);

                    string sanitizedCustomName = string.Join("_", customName.Split(Path.GetInvalidFileNameChars()));
                    string fileName = $"{sanitizedCustomName}.jpg";
                    string targetFilePath = Path.Combine(uploadDir, fileName);

                    await File.WriteAllBytesAsync(targetFilePath, resizedBytes);

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
                            Log.Error(ex, "Failed to load skins_paints.json during AI auto-save.");
                        }
                    }

                    string skinName = Path.GetFileNameWithoutExtension(fileName);
                    if (!paints.Any(p => p.Name.Equals(skinName, StringComparison.OrdinalIgnoreCase) && p.ItemName == "Uploaded"))
                    {
                        paints.Add(new SkinModel
                        {
                            Category = "Paints",
                            ItemName = "Uploaded",
                            Name = skinName,
                            SourceFolder = uploadDirRelative.Replace("\\", "/"),
                            PreviewImage = Path.Combine(uploadDirRelative, fileName).Replace("\\", "/")
                        });

                        string updatedJson = JsonSerializer.Serialize(paints, options);
                        await File.WriteAllTextAsync(jsonPath, updatedJson);
                    }

                    _generatedImageBytes = null;
                    GeneratedImageSource = null;
                    AiPrompt = string.Empty;
                    OnPropertyChanged(nameof(HasGeneratedTexture));

                    await _mainVM.LoadSkinsAsync();
                    await _mainVM.NotificationService.ShowAsync("Import Success", $"AI custom paint '{skinName}' has been added to your Uploaded list!", ControlAppearance.Success);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to auto-import generated paint.");
                    await _mainVM.NotificationService.ShowAsync("Import Error", $"Failed to import paint: {ex.Message}", ControlAppearance.Danger);
                }
                finally
                {
                    _mainVM.IsLoading = false;
                }
            }
            else if (result == MessageBoxResult.Secondary)
            {
                Microsoft.Win32.SaveFileDialog dialog = new()
                {
                    Title = "Save Generated Paint to PC",
                    Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png",
                    FileName = string.IsNullOrWhiteSpace(AiPrompt)
                        ? "ai_paint.jpg"
                        : string.Join("_", AiPrompt.Split(Path.GetInvalidFileNameChars())) + ".jpg"
                };

                if (dialog.ShowDialog() == true)
                {
                    string targetPath = dialog.FileName;
                    try
                    {
                        _mainVM.IsLoading = true;
                        byte[] resizedBytes = await ResizeImageBytesAsync(_generatedImageBytes, 256, 256);
                        await File.WriteAllBytesAsync(targetPath, resizedBytes);
                        Log.Information("Saved AI paint to PC: {Path}", targetPath);

                        _generatedImageBytes = null;
                        GeneratedImageSource = null;
                        AiPrompt = string.Empty;
                        OnPropertyChanged(nameof(HasGeneratedTexture));

                        await _mainVM.NotificationService.ShowAsync("Save Success", "AI paint saved to your computer successfully!", ControlAppearance.Success);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to save generated paint to PC.");
                        await _mainVM.NotificationService.ShowAsync("Save Error", $"Failed to save to PC: {ex.Message}", ControlAppearance.Danger);
                    }
                    finally
                    {
                        _mainVM.IsLoading = false;
                    }
                }
            }
        }

        private byte[] ResizeImageBytes(byte[] bytes, int width, int height)
        {
            using MemoryStream ms = new(bytes);
            BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            BitmapFrame frame = decoder.Frames[0];

            DrawingVisual targetVisual = new();
            using (DrawingContext dc = targetVisual.RenderOpen())
            {
                dc.DrawImage(frame, new Rect(0, 0, width, height));
            }

            RenderTargetBitmap targetBitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
            targetBitmap.Render(targetVisual);

            JpegBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(targetBitmap));

            using MemoryStream outStream = new();
            encoder.Save(outStream);
            return outStream.ToArray();
        }

        private Task<byte[]> ResizeImageBytesAsync(byte[] bytes, int width, int height)
        {
            TaskCompletionSource<byte[]> tcs = new();
            Thread thread = new(() =>
            {
                try
                {
                    byte[] result = ResizeImageBytes(bytes, width, height);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return tcs.Task;
        }

        private BitmapImage? BytesToImage(byte[]? bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return null;
            }

            try
            {
                BitmapImage image = new();
                using (MemoryStream ms = new(bytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = ms;
                    image.EndInit();
                }
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to convert bytes to ImageSource.");
                return null;
            }
        }

        private void ExecutePreviewGeneratedTexture(object? parameter)
        {
            if (GeneratedImageSource == null)
            {
                return;
            }

            try
            {
                ImagePreviewWindow previewWindow = new(GeneratedImageSource)
                {
                    Owner = Application.Current.MainWindow
                };
                _ = previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open preview window.");
            }
        }
    }
}
