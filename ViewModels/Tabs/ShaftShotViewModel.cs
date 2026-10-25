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
    public class ShaftShotViewModel : ViewModelBase
    {
        private readonly IShaftShotComposer _composer;
        private readonly MainViewModel _mainVM;

        public List<string> ComponentOptions { get; } =
        [
            "Bullet Trail",
            "Laser Dot",
            "Muzzle Flash",
            "Muzzle Burst",
            "Impact Burst",
            "Muzzle Smoke",
        ];

        private string _selectedComponentOption = "Bullet Trail";
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

        private ShaftShotComponent CurrentComponent =>
            _selectedComponentOption switch
            {
                "Laser Dot" => ShaftShotComponent.LaserDot,
                "Muzzle Flash" => ShaftShotComponent.MuzzleFlash,
                "Muzzle Burst" => ShaftShotComponent.Burst,
                "Impact Burst" => ShaftShotComponent.Impact,
                "Muzzle Smoke" => ShaftShotComponent.MuzzleSmoke,
                _ => ShaftShotComponent.Trail,
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

        public ShaftShotViewModel(IShaftShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveShaftEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public ShaftShotRenderOptions ToRenderOptions()
        {
            return new ShaftShotRenderOptions
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
            ShaftShotComponent component = CurrentComponent;
            ShaftShotRenderOptions options = ToRenderOptions();

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

        private async Task SaveShaftEffectAsync()
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
                _mainVM.UpdateStatus = "Generating and applying custom Shaft shot effect...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Shaft",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                string templateDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Shaft"
                );
                if (!Directory.Exists(templateDir))
                {
                    string alt = Path.Combine(
                        AppContext.BaseDirectory,
                        "Textures",
                        "ShotEffects",
                        "Turrets",
                        "Shaft"
                    );
                    if (Directory.Exists(alt))
                    {
                        templateDir = alt;
                    }
                    else
                    {
                        string cwd = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "Textures",
                            "ShotEffects",
                            "Turrets",
                            "Shaft"
                        );
                        if (Directory.Exists(cwd))
                        {
                            templateDir = cwd;
                        }
                    }
                }

                ShaftShotRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ShaftTrailImageTarget),
                        ShaftShotComponent.Trail
                    );
                    string trailAlphaSrc = Path.Combine(templateDir, "shaft_trail_alpha.jpg");
                    if (File.Exists(trailAlphaSrc))
                    {
                        File.Copy(
                            trailAlphaSrc,
                            Path.Combine(customDir, Constants.ShaftTrailAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ShaftLaserDotImageTarget),
                        ShaftShotComponent.LaserDot
                    );
                    string laserAlphaSrc = Path.Combine(templateDir, "shaft_laser_alpha.jpg");
                    if (File.Exists(laserAlphaSrc))
                    {
                        File.Copy(
                            laserAlphaSrc,
                            Path.Combine(customDir, Constants.ShaftLaserDotAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ShaftMuzzleSmokeImageTarget),
                        ShaftShotComponent.MuzzleSmoke
                    );
                    string smokeAlphaSrc = Path.Combine(
                        templateDir,
                        "shaft_muzzle_smoke_alpha.jpg"
                    );
                    if (File.Exists(smokeAlphaSrc))
                    {
                        File.Copy(
                            smokeAlphaSrc,
                            Path.Combine(customDir, Constants.ShaftMuzzleSmokeAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ShaftMuzzleFlashTarget),
                        ShaftShotComponent.MuzzleFlash
                    );

                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ShaftBurstTarget),
                        ShaftShotComponent.Burst
                    );

                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ShaftImpactTarget),
                        ShaftShotComponent.Impact
                    );
                });

                List<string> potentialTargets =
                [
                    Constants.ShaftBurstTarget,
                    Constants.ShaftTrailImageTarget,
                    Constants.ShaftTrailAlphaTarget,
                    Constants.ShaftMuzzleSmokeImageTarget,
                    Constants.ShaftMuzzleSmokeAlphaTarget,
                    Constants.ShaftLaserDotImageTarget,
                    Constants.ShaftLaserDotAlphaTarget,
                    Constants.ShaftMuzzleFlashTarget,
                    Constants.ShaftImpactTarget,
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
                    Turret = "Shaft",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Shaft/Custom",
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
                        "Custom Shaft shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Shaft are not cached by the loader yet. Please launch the game with Shaft equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Shaft effect: {result}";
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
