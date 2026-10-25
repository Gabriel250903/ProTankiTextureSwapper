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
    public class SmokyShotViewModel : ViewModelBase
    {
        private readonly ISmokyShotComposer _composer;
        private readonly MainViewModel _mainVM;

        public List<string> ComponentOptions { get; } =
        [
            "Muzzle Flash",
            "Critical Hit 1 (Fireball)",
            "Critical Hit 2 (Blast Wave)",
            "Critical Hit 3 (Fiery Burst)",
            "Bullet Tracer",
            "Muzzle Glow",
        ];

        private string _selectedComponentOption = "Critical Hit 1 (Fireball)";
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

        private SmokyShotComponent CurrentComponent =>
            _selectedComponentOption switch
            {
                "Muzzle Flash" => SmokyShotComponent.MuzzleFlash,
                "Critical Hit 2 (Blast Wave)" => SmokyShotComponent.CriticalHit2,
                "Critical Hit 3 (Fiery Burst)" => SmokyShotComponent.CriticalHit3,
                "Bullet Tracer" => SmokyShotComponent.Bullet,
                "Muzzle Glow" => SmokyShotComponent.Glow,
                _ => SmokyShotComponent.CriticalHit1,
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

        public SmokyShotViewModel(ISmokyShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveSmokyEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public SmokyShotRenderOptions ToRenderOptions()
        {
            return new SmokyShotRenderOptions
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
            SmokyShotComponent component = CurrentComponent;
            SmokyShotRenderOptions options = ToRenderOptions();

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

        private async Task SaveSmokyEffectAsync()
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
                _mainVM.UpdateStatus = "Generating and applying custom Smoky shot effect...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Smoky",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                string templateDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Smoky"
                );
                if (!Directory.Exists(templateDir))
                {
                    string alt = Path.Combine(
                        AppContext.BaseDirectory,
                        "Textures",
                        "ShotEffects",
                        "Turrets",
                        "Smoky"
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
                            "Smoky"
                        );
                        if (Directory.Exists(cwd))
                        {
                            templateDir = cwd;
                        }
                    }
                }

                SmokyShotRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.SmokyMuzzleFlashTarget),
                        SmokyShotComponent.MuzzleFlash
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.SmokyCriticalHit1Target),
                        SmokyShotComponent.CriticalHit1
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.SmokyCriticalHit2Target),
                        SmokyShotComponent.CriticalHit2
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.SmokyCriticalHit3Target),
                        SmokyShotComponent.CriticalHit3
                    );

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.SmokyBulletImageTarget),
                        SmokyShotComponent.Bullet
                    );
                    string bulletAlphaSource = Path.Combine(templateDir, "smoky_bullet_alpha.jpg");
                    if (File.Exists(bulletAlphaSource))
                    {
                        File.Copy(
                            bulletAlphaSource,
                            Path.Combine(customDir, Constants.SmokyBulletAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.SmokyGlowImageTarget),
                        SmokyShotComponent.Glow
                    );
                    string glowAlphaSource = Path.Combine(templateDir, "smoky_glow_alpha.jpg");
                    if (File.Exists(glowAlphaSource))
                    {
                        File.Copy(
                            glowAlphaSource,
                            Path.Combine(customDir, Constants.SmokyGlowAlphaTarget),
                            true
                        );
                    }
                });

                List<string> potentialTargets =
                [
                    Constants.SmokyMuzzleFlashTarget,
                    Constants.SmokyCriticalHit1Target,
                    Constants.SmokyCriticalHit2Target,
                    Constants.SmokyCriticalHit3Target,
                    Constants.SmokyBulletImageTarget,
                    Constants.SmokyBulletAlphaTarget,
                    Constants.SmokyGlowImageTarget,
                    Constants.SmokyGlowAlphaTarget,
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
                    Turret = "Smoky",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Smoky/Custom",
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
                        "Custom Smoky shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Smoky are not cached by the loader yet. Please launch the game with Smoky equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Smoky effect: {result}";
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
