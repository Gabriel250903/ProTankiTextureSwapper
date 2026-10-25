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
    public class ThunderShotViewModel : ViewModelBase
    {
        private readonly IThunderShotComposer _composer;
        private readonly MainViewModel _mainVM;

        public List<string> ComponentOptions { get; } =
        [
            "Explosion (Fireball)",
            "Explosion Smoke (Crater)",
            "Explosion Ring",
            "Muzzle Flash",
            "Projectile Shaft",
            "Projectile Head",
            "Shockwave Ring",
        ];

        private string _selectedComponentOption = "Explosion (Fireball)";
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

        private ThunderShotComponent CurrentComponent =>
            _selectedComponentOption switch
            {
                "Explosion Smoke (Crater)" => ThunderShotComponent.ExplosionSmoke,
                "Explosion Ring" => ThunderShotComponent.ExplosionRing,
                "Muzzle Flash" => ThunderShotComponent.MuzzleFlash,
                "Projectile Shaft" => ThunderShotComponent.ProjectileShaft,
                "Projectile Head" => ThunderShotComponent.ProjectileHead,
                "Shockwave Ring" => ThunderShotComponent.Shockwave,
                _ => ThunderShotComponent.Explosion,
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

        public ThunderShotViewModel(IThunderShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveThunderEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public ThunderShotRenderOptions ToRenderOptions()
        {
            return new ThunderShotRenderOptions
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
            ThunderShotComponent component = CurrentComponent;
            ThunderShotRenderOptions options = ToRenderOptions();

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

        private async Task SaveThunderEffectAsync()
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
                _mainVM.UpdateStatus = "Generating and applying custom Thunder shot effect...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Thunder",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                string templateDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Thunder"
                );
                if (!Directory.Exists(templateDir))
                {
                    string alt = Path.Combine(
                        AppContext.BaseDirectory,
                        "Textures",
                        "ShotEffects",
                        "Turrets",
                        "Thunder"
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
                            "Thunder"
                        );
                        if (Directory.Exists(cwd))
                        {
                            templateDir = cwd;
                        }
                    }
                }

                ThunderShotRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionMainTarget),
                        ThunderShotComponent.Explosion
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionSmokeTarget),
                        ThunderShotComponent.ExplosionSmoke
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionRingTarget),
                        ThunderShotComponent.ExplosionRing
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar1Target),
                        ThunderShotComponent.ExplosionVar1
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar2Target),
                        ThunderShotComponent.ExplosionVar2
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar3Target),
                        ThunderShotComponent.ExplosionVar3
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar4Target),
                        ThunderShotComponent.ExplosionVar4
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar5Target),
                        ThunderShotComponent.ExplosionVar1
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar6Target),
                        ThunderShotComponent.ExplosionVar2
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar7Target),
                        ThunderShotComponent.ExplosionVar3
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.ThunderExplosionVar8Target),
                        ThunderShotComponent.ExplosionVar4
                    );

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ThunderProjectileShaftImageTarget),
                        ThunderShotComponent.ProjectileShaft
                    );
                    string shaftAlphaSrc = Path.Combine(
                        templateDir,
                        "thunder_projectile_shaft_alpha.jpg"
                    );
                    if (File.Exists(shaftAlphaSrc))
                    {
                        File.Copy(
                            shaftAlphaSrc,
                            Path.Combine(customDir, Constants.ThunderProjectileShaftAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ThunderProjectileHeadImageTarget),
                        ThunderShotComponent.ProjectileHead
                    );
                    string headAlphaSrc = Path.Combine(
                        templateDir,
                        "thunder_projectile_head_alpha.jpg"
                    );
                    if (File.Exists(headAlphaSrc))
                    {
                        File.Copy(
                            headAlphaSrc,
                            Path.Combine(customDir, Constants.ThunderProjectileHeadAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ThunderShockwaveImageTarget),
                        ThunderShotComponent.Shockwave
                    );
                    string shockAlphaSrc = Path.Combine(templateDir, "thunder_shockwave_alpha.jpg");
                    if (File.Exists(shockAlphaSrc))
                    {
                        File.Copy(
                            shockAlphaSrc,
                            Path.Combine(customDir, Constants.ThunderShockwaveAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ThunderShockwave2ImageTarget),
                        ThunderShotComponent.Shockwave
                    );
                    if (File.Exists(shockAlphaSrc))
                    {
                        File.Copy(
                            shockAlphaSrc,
                            Path.Combine(customDir, Constants.ThunderShockwave2AlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ThunderMuzzleFlashImageTarget),
                        ThunderShotComponent.MuzzleFlash
                    );
                    string flashAlphaSrc = Path.Combine(templateDir, "thunder_flash_alpha.jpg");
                    if (File.Exists(flashAlphaSrc))
                    {
                        File.Copy(
                            flashAlphaSrc,
                            Path.Combine(customDir, Constants.ThunderMuzzleFlashAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveImage(
                        options,
                        Path.Combine(customDir, Constants.ThunderMuzzleFlashVarImageTarget),
                        ThunderShotComponent.MuzzleFlash
                    );
                    if (File.Exists(flashAlphaSrc))
                    {
                        File.Copy(
                            flashAlphaSrc,
                            Path.Combine(customDir, Constants.ThunderMuzzleFlashVarAlphaTarget),
                            true
                        );
                    }
                });

                List<string> potentialTargets =
                [
                    Constants.ThunderExplosionMainTarget,
                    Constants.ThunderExplosionSmokeTarget,
                    Constants.ThunderExplosionRingTarget,
                    Constants.ThunderExplosionVar1Target,
                    Constants.ThunderExplosionVar2Target,
                    Constants.ThunderExplosionVar3Target,
                    Constants.ThunderExplosionVar4Target,
                    Constants.ThunderExplosionVar5Target,
                    Constants.ThunderExplosionVar6Target,
                    Constants.ThunderExplosionVar7Target,
                    Constants.ThunderExplosionVar8Target,
                    Constants.ThunderProjectileShaftImageTarget,
                    Constants.ThunderProjectileShaftAlphaTarget,
                    Constants.ThunderProjectileHeadImageTarget,
                    Constants.ThunderProjectileHeadAlphaTarget,
                    Constants.ThunderShockwaveImageTarget,
                    Constants.ThunderShockwaveAlphaTarget,
                    Constants.ThunderShockwave2ImageTarget,
                    Constants.ThunderShockwave2AlphaTarget,
                    Constants.ThunderMuzzleFlashImageTarget,
                    Constants.ThunderMuzzleFlashAlphaTarget,
                    Constants.ThunderMuzzleFlashVarImageTarget,
                    Constants.ThunderMuzzleFlashVarAlphaTarget,
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
                    Turret = "Thunder",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Thunder/Custom",
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
                        "Custom Thunder shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Thunder are not cached by the loader yet. Please launch the game with Thunder equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Thunder effect: {result}";
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
