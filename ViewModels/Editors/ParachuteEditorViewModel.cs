using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Services.MaskStrategies;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Editors
{
    public class ParachuteEditorViewModel : TextureEditorViewModelBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly IMaskStrategy _maskStrategy = new ParachuteMaskStrategy();
        private readonly IParachuteComposer _parachuteComposer;
        private double _shadingOpacity = 0.5;
        private ImageSource? _renderedPreview;
        private string _parachuteName = "Custom Parachute";

        public double ShadingOpacity
        {
            get => _shadingOpacity;
            set
            {
                if (SetProperty(ref _shadingOpacity, value))
                {
                    UpdatePreview();
                }
            }
        }

        public ImageSource? RenderedPreview
        {
            get => _renderedPreview;
            set => SetProperty(ref _renderedPreview, value);
        }

        public string ParachuteName
        {
            get => _parachuteName;
            set => SetProperty(ref _parachuteName, value);
        }

        public ICommand ApplyAndSwapCommand { get; }
        public ICommand ExportImageCommand { get; }

        public ParachuteEditorViewModel(
            ITextureComposer composer,
            IParachuteComposer parachuteComposer,
            ISwapService swapService,
            INotificationService notificationService,
            MainViewModel mainVM
        )
            : base(composer, swapService, notificationService, mainVM)
        {
            _parachuteComposer = parachuteComposer;

            ApplyAndSwapCommand = new AsyncRelayCommand(ExecuteApplyAndSwap, _ => !IsApplying);
            ExportImageCommand = new RelayCommand(ExecuteExportImage);

            UpdatePreview();
        }

        public override void UpdatePreview()
        {
            try
            {
                if (!AvailablePaints.Any())
                {
                    LoadPaintsList();
                }

                ParachuteRenderOptions options = GetRenderOptions();
                RenderedPreview = _parachuteComposer.ComposeParachute(options);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating parachute preview.");
            }
        }

        private ParachuteRenderOptions GetRenderOptions()
        {
            return new ParachuteRenderOptions
            {
                SourceImagePath = SourceImagePath,
                Scale = Scale,
                OffsetX = OffsetX,
                OffsetY = OffsetY,
                RotationAngle = RotationAngle,
                TileMode = TileMode,
                ShadingOpacity = ShadingOpacity,
            };
        }

        private async Task ExecuteApplyAndSwap(object? obj)
        {
            if (!IsPremiumUnlocked)
            {
                _ = NotificationService.ShowAsync(
                    "Premium Feature Locked",
                    "The Custom Parachute Editor is a ProTanki Premium feature! Please unlock Premium in Settings.",
                    ControlAppearance.Danger
                );
                return;
            }

            try
            {
                IsApplying = true;

                string cachePath = MainVM.CachePath;
                if (string.IsNullOrEmpty(cachePath))
                {
                    _ = NotificationService.ShowAsync(
                        "Cache Error",
                        "ProTanki cache directory was not found. Please launch ProTanki and load into a game while any supply is dropping.",
                        ControlAppearance.Danger
                    );
                    return;
                }

                if (string.IsNullOrWhiteSpace(ParachuteName))
                {
                    _ = NotificationService.ShowAsync(
                        "Missing Name",
                        "Please enter a name for your custom parachute before swapping.",
                        ControlAppearance.Caution
                    );
                    return;
                }

                string name = ParachuteName.Trim();
                string folderName = string.Concat(name.Split(Path.GetInvalidFileNameChars()))
                    .Replace(" ", "_");
                if (string.IsNullOrWhiteSpace(folderName))
                {
                    folderName = $"Custom_{DateTime.Now:yyyyMMdd_HHmmss}";
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string relativeFolder = $"Textures/Spawners/Parachute/{folderName}";
                string customDir = Path.Combine(
                    baseDir,
                    "Textures",
                    "Spawners",
                    "Parachute",
                    folderName
                );
                string customJpg = Path.Combine(customDir, "details.jpg");

                ParachuteRenderOptions options = GetRenderOptions();
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    _parachuteComposer.SaveParachuteJpg(options, customJpg)
                );

                SkinModel customParaSkin = new()
                {
                    Category = "Spawners",
                    ItemName = "Parachutes",
                    Name = name,
                    SourceFolder = relativeFolder,
                    PreviewImage = $"{relativeFolder}/details.jpg",
                    DetailsTarget =
                        "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8yLzI0Ny8zMjAzMDYyNDU0MDExMS9wYXJhLmpwZw==",
                };

                await SaveSkinToLocalSpawnersJsonAsync(customParaSkin);

                if (MainVM.AllSkins != null)
                {
                    SkinModel? existing = MainVM.AllSkins.FirstOrDefault(s =>
                        s.Category.Equals("Spawners", StringComparison.OrdinalIgnoreCase)
                        && s.ItemName.Equals("Parachutes", StringComparison.OrdinalIgnoreCase)
                        && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
                    );
                    if (existing != null)
                    {
                        _ = MainVM.AllSkins.Remove(existing);
                    }
                    MainVM.AllSkins.Add(customParaSkin);
                }

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MainVM.SkinsTabVM?.OnSkinsLoaded();
                });

                string? result = await Task.Run(() => SwapService.Swap(cachePath, customParaSkin));
                _ =
                    result == null
                        ? NotificationService.ShowAsync(
                            MainVM.LocalizationService.GetString("Loc_Msg_Success"),
                            MainVM.LocalizationService.GetString("Loc_Msg_ParachuteAppliedSuccess"),
                            ControlAppearance.Success
                        )
                        : NotificationService.ShowAsync(
                            MainVM.LocalizationService.GetString("Loc_Msg_Error"),
                            $"Failed to swap parachute: {result}",
                            ControlAppearance.Danger
                        );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply custom parachute texture.");
                _ = NotificationService.ShowAsync(
                    MainVM.LocalizationService.GetString("Loc_Msg_Error"),
                    $"Error applying parachute: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
            finally
            {
                IsApplying = false;
            }
        }

        private void ExecuteExportImage(object? obj)
        {
            SaveFileDialog dlg = new()
            {
                Filter = "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png",
                FileName = "para.jpg",
                Title = "Export Parachute Texture",
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    ParachuteRenderOptions options = GetRenderOptions();
                    _parachuteComposer.SaveParachuteJpg(options, dlg.FileName);
                    _ = NotificationService.ShowAsync(
                        MainVM.LocalizationService.GetString("Loc_Msg_ExportSuccess"),
                        MainVM.LocalizationService.GetString("Loc_Msg_ExportSuccessDesc"),
                        ControlAppearance.Success
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to export parachute image.");
                    _ = NotificationService.ShowAsync(
                        MainVM.LocalizationService.GetString("Loc_Msg_ExportError"),
                        $"Failed to export image: {ex.Message}",
                        ControlAppearance.Danger
                    );
                }
            }
        }

        protected override void ExecuteResetTransform(object? obj)
        {
            _shadingOpacity = 0.5;
            _parachuteName = "Custom Parachute";
            OnPropertyChanged(nameof(ShadingOpacity));
            OnPropertyChanged(nameof(ParachuteName));
            base.ExecuteResetTransform(obj);
        }

        private static async Task SaveSkinToLocalSpawnersJsonAsync(SkinModel skin)
        {
            try
            {
                string jsonPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Constants.SpawnersSkinsJson
                );
                List<SkinModel> skins = [];
                if (File.Exists(jsonPath))
                {
                    string existingJson = await File.ReadAllTextAsync(jsonPath);
                    if (!string.IsNullOrWhiteSpace(existingJson))
                    {
                        skins = JsonSerializer.Deserialize<List<SkinModel>>(existingJson) ?? [];
                    }
                }

                int existingIdx = skins.FindIndex(s =>
                    s.Name.Equals(skin.Name, StringComparison.OrdinalIgnoreCase)
                );
                if (existingIdx >= 0)
                {
                    skins[existingIdx] = skin;
                }
                else
                {
                    skins.Add(skin);
                }

                string updatedJson = JsonSerializer.Serialize(skins, JsonOptions);
                await File.WriteAllTextAsync(jsonPath, updatedJson);
                Log.Information($"Saved custom parachute '{skin.Name}' to skins_spawners.json");
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    $"Failed to save custom parachute '{skin.Name}' to skins_spawners.json"
                );
            }
        }
    }
}
