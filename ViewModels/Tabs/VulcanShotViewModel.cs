using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public class VulcanShotViewModel : ViewModelBase
    {
        private readonly IVulcanShotComposer _composer;
        private readonly MainViewModel _mainVM;

        public List<string> ComponentOptions { get; } =
        [
            "Bullet Tracers",
            "Muzzle Burst",
            "Muzzle Flame",
            "Smoke Puff",
            "Smoke Cloud",
            "Overheat Sparks",
        ];

        private string _selectedComponentOption = "Bullet Tracers";
        public string SelectedComponentOption
        {
            get => _selectedComponentOption;
            set
            {
                if (SetProperty(ref _selectedComponentOption, value))
                {
                    UpdatePreview();
                }
            }
        }

        private VulcanShotComponent CurrentComponent =>
            _selectedComponentOption switch
            {
                "Muzzle Burst" => VulcanShotComponent.MuzzleBurst,
                "Muzzle Flame" => VulcanShotComponent.MuzzleFlame,
                "Smoke Puff" => VulcanShotComponent.SmokePuff,
                "Smoke Cloud" => VulcanShotComponent.SmokeCloud,
                "Overheat Sparks" => VulcanShotComponent.Overheat,
                _ => VulcanShotComponent.Tracers,
            };

        private int _previewGeneration;
        private double _hue;
        public double Hue
        {
            get => _hue;
            set
            {
                if (SetProperty(ref _hue, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _saturation = 100.0;
        public double Saturation
        {
            get => _saturation;
            set
            {
                if (SetProperty(ref _saturation, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _brightness = 0.0;
        public double Brightness
        {
            get => _brightness;
            set
            {
                if (SetProperty(ref _brightness, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _contrast = 1.0;
        public double Contrast
        {
            get => _contrast;
            set
            {
                if (SetProperty(ref _contrast, value))
                {
                    UpdatePreview();
                }
            }
        }

        private bool _invert;
        public bool Invert
        {
            get => _invert;
            set
            {
                if (SetProperty(ref _invert, value))
                {
                    UpdatePreview();
                }
            }
        }

        private BitmapSource? _previewImage;
        public BitmapSource? PreviewImage
        {
            get => _previewImage;
            set => SetProperty(ref _previewImage, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand RestoreCommand { get; }

        public VulcanShotViewModel(IVulcanShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveVulcanEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public VulcanShotRenderOptions ToRenderOptions()
        {
            return new VulcanShotRenderOptions
            {
                Hue = Hue,
                Saturation = Saturation,
                Brightness = Brightness,
                Contrast = Contrast,
                Invert = Invert,
            };
        }

        private void UpdatePreview()
        {
            int currentGen = Interlocked.Increment(ref _previewGeneration);
            VulcanShotComponent component = CurrentComponent;
            VulcanShotRenderOptions options = ToRenderOptions();

            _ = Task.Run(() =>
            {
                BitmapSource preview = _composer.ComposePreview(options, component);
                if (currentGen == _previewGeneration)
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (currentGen == _previewGeneration)
                        {
                            PreviewImage = preview;
                        }
                    });
                }
            });
        }

        private async Task SaveVulcanEffectAsync()
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            if (!_mainVM.Settings.IsPremiumUnlocked)
            {
                await _mainVM.NotificationService.ShowAsync(
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequired"),
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequiredDesc"),
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
                _mainVM.UpdateStatus = "Generating and applying custom Vulcan shot effect...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Vulcan",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                VulcanShotRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.VulcanTracersTarget),
                        VulcanShotComponent.Tracers
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.VulcanMuzzleBurstTarget),
                        VulcanShotComponent.MuzzleBurst
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.VulcanMuzzleFlameTarget),
                        VulcanShotComponent.MuzzleFlame
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.VulcanSmokePuffTarget),
                        VulcanShotComponent.SmokePuff
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.VulcanSmokeCloudTarget),
                        VulcanShotComponent.SmokeCloud
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.VulcanOverheatTarget),
                        VulcanShotComponent.Overheat
                    );
                });

                List<string> potentialTargets =
                [
                    Constants.VulcanTracersTarget,
                    Constants.VulcanMuzzleBurstTarget,
                    Constants.VulcanMuzzleFlameTarget,
                    Constants.VulcanSmokePuffTarget,
                    Constants.VulcanSmokeCloudTarget,
                    Constants.VulcanOverheatTarget,
                ];

                List<string> activeTargets = [];
                foreach (string target in potentialTargets)
                {
                    string cachePath = FileHelper.GetSafePath(_mainVM.CachePath, target);
                    if (File.Exists(cachePath))
                    {
                        activeTargets.Add(target);
                    }
                }

                if (activeTargets.Count == 0)
                {
                    activeTargets = potentialTargets;
                }

                ShotEffectModel customEffect = new()
                {
                    Turret = "Vulcan",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Vulcan/Custom",
                    PreviewImage = "",
                    Targets = activeTargets,
                };

                string? result = await Task.Run(() =>
                    _mainVM.SwapService.SwapShotEffect(_mainVM.CachePath, customEffect)
                );
                await Task.Delay(1000);
                if (result == null)
                {
                    notificationTitle = "Success";
                    notificationMessage =
                        "Custom Vulcan shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Vulcan are not cached by the loader yet. Please launch the game with Vulcan equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Vulcan effect: {result}";
                    notificationAppearance = ControlAppearance.Danger;
                }
            }
            catch (Exception ex)
            {
                notificationTitle = "Error";
                notificationMessage = $"Error: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                _mainVM.IsLoading = false;
                _mainVM.UpdateStatus = string.Empty;
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

        private async Task RestoreOriginalsAsync()
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            try
            {
                await _mainVM.NotificationService.ShowAsync(
                    "Backups",
                    "Please use the 'Backups' tab to restore vanilla textures.",
                    ControlAppearance.Info
                );
            }
            catch (Exception ex)
            {
                await _mainVM.NotificationService.ShowAsync(
                    "Error",
                    $"Error restoring: {ex.Message}",
                    ControlAppearance.Danger
                );
            }
        }
    }
}
