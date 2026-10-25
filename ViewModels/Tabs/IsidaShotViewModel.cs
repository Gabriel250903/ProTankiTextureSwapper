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
    public class IsidaShotViewModel : ViewModelBase
    {
        private readonly IIsidaShotComposer _composer;
        private readonly MainViewModel _mainVM;
        private int _previewGeneration;

        public List<string> ComponentOptions { get; } =
        [
            "Damage Beam",
            "Damage Ball",
            "Damage Impact Sparks",
            "Damage Flare",
            "Healing Beam",
            "Healing Ball",
            "Healing Shaft",
        ];

        private string _selectedComponentOption = "Damage Beam";
        public string SelectedComponentOption
        {
            get => _selectedComponentOption;
            set
            {
                if (SetProperty(ref _selectedComponentOption, value))
                {
                    if (
                        value.StartsWith("Damage", StringComparison.OrdinalIgnoreCase)
                        && SelectedTabIndex != 0
                    )
                    {
                        _selectedTabIndex = 0;
                        OnPropertyChanged(nameof(SelectedTabIndex));
                    }
                    else if (
                        value.StartsWith("Healing", StringComparison.OrdinalIgnoreCase)
                        && SelectedTabIndex != 1
                    )
                    {
                        _selectedTabIndex = 1;
                        OnPropertyChanged(nameof(SelectedTabIndex));
                    }

                    UpdatePreview();
                }
            }
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    if (
                        _selectedTabIndex == 0
                        && !_selectedComponentOption.StartsWith(
                            "Damage",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        _selectedComponentOption = "Damage Beam";
                        OnPropertyChanged(nameof(SelectedComponentOption));
                    }
                    else if (
                        _selectedTabIndex == 1
                        && !_selectedComponentOption.StartsWith(
                            "Healing",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        _selectedComponentOption = "Healing Beam";
                        OnPropertyChanged(nameof(SelectedComponentOption));
                    }

                    UpdatePreview();
                }
            }
        }

        private IsidaShotComponent CurrentComponent =>
            _selectedComponentOption switch
            {
                "Damage Ball" => IsidaShotComponent.DamageBall,
                "Damage Impact Sparks" => IsidaShotComponent.DamageImpact,
                "Damage Flare" => IsidaShotComponent.DamageFlare,
                "Healing Beam" => IsidaShotComponent.HealBeam,
                "Healing Ball" => IsidaShotComponent.HealBall,
                "Healing Shaft" => IsidaShotComponent.HealShaft,
                _ => IsidaShotComponent.DamageBeam,
            };

        private bool IsDamageComponent(IsidaShotComponent comp)
        {
            return comp
                is IsidaShotComponent.DamageBeam
                    or IsidaShotComponent.DamageBall
                    or IsidaShotComponent.DamageImpact
                    or IsidaShotComponent.DamageFlare;
        }

        private double _damageHue;
        public double DamageHue
        {
            get => _damageHue;
            set
            {
                if (SetProperty(ref _damageHue, value))
                {
                    if (SyncHealWithDamage)
                    {
                        _healHue = value;
                        OnPropertyChanged(nameof(HealHue));
                    }
                    UpdatePreview();
                }
            }
        }

        private double _damageSaturation = 100.0;
        public double DamageSaturation
        {
            get => _damageSaturation;
            set
            {
                if (SetProperty(ref _damageSaturation, value))
                {
                    if (SyncHealWithDamage)
                    {
                        _healSaturation = value;
                        OnPropertyChanged(nameof(HealSaturation));
                    }
                    UpdatePreview();
                }
            }
        }

        private double _damageBrightness;
        public double DamageBrightness
        {
            get => _damageBrightness;
            set
            {
                if (SetProperty(ref _damageBrightness, value))
                {
                    if (SyncHealWithDamage)
                    {
                        _healBrightness = value;
                        OnPropertyChanged(nameof(HealBrightness));
                    }
                    UpdatePreview();
                }
            }
        }

        private double _damageContrast = 1.0;
        public double DamageContrast
        {
            get => _damageContrast;
            set
            {
                if (SetProperty(ref _damageContrast, value))
                {
                    if (SyncHealWithDamage)
                    {
                        _healContrast = value;
                        OnPropertyChanged(nameof(HealContrast));
                    }
                    UpdatePreview();
                }
            }
        }

        private bool _damageInvert;
        public bool DamageInvert
        {
            get => _damageInvert;
            set
            {
                if (SetProperty(ref _damageInvert, value))
                {
                    if (SyncHealWithDamage)
                    {
                        _healInvert = value;
                        OnPropertyChanged(nameof(HealInvert));
                    }
                    UpdatePreview();
                }
            }
        }

        private bool _syncHealWithDamage = true;
        public bool SyncHealWithDamage
        {
            get => _syncHealWithDamage;
            set
            {
                if (SetProperty(ref _syncHealWithDamage, value))
                {
                    if (value)
                    {
                        _healHue = _damageHue;
                        _healSaturation = _damageSaturation;
                        _healBrightness = _damageBrightness;
                        _healContrast = _damageContrast;
                        _healInvert = _damageInvert;
                        OnPropertyChanged(nameof(HealHue));
                        OnPropertyChanged(nameof(HealSaturation));
                        OnPropertyChanged(nameof(HealBrightness));
                        OnPropertyChanged(nameof(HealContrast));
                        OnPropertyChanged(nameof(HealInvert));
                    }
                    OnPropertyChanged(nameof(CanEditHeal));
                    UpdatePreview();
                }
            }
        }

        public bool CanEditHeal => !SyncHealWithDamage;

        private double _healHue = 120.0;
        public double HealHue
        {
            get => _healHue;
            set
            {
                if (SetProperty(ref _healHue, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _healSaturation = 100.0;
        public double HealSaturation
        {
            get => _healSaturation;
            set
            {
                if (SetProperty(ref _healSaturation, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _healBrightness;
        public double HealBrightness
        {
            get => _healBrightness;
            set
            {
                if (SetProperty(ref _healBrightness, value))
                {
                    UpdatePreview();
                }
            }
        }

        private double _healContrast = 1.0;
        public double HealContrast
        {
            get => _healContrast;
            set
            {
                if (SetProperty(ref _healContrast, value))
                {
                    UpdatePreview();
                }
            }
        }

        private bool _healInvert;
        public bool HealInvert
        {
            get => _healInvert;
            set
            {
                if (SetProperty(ref _healInvert, value))
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

        public IsidaShotViewModel(IIsidaShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveIsidaEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public IsidaShotRenderOptions ToDamageRenderOptions()
        {
            return new IsidaShotRenderOptions
            {
                Hue = DamageHue,
                Saturation = DamageSaturation,
                Brightness = DamageBrightness,
                Contrast = DamageContrast,
                Invert = DamageInvert,
            };
        }

        public IsidaShotRenderOptions ToHealRenderOptions()
        {
            return new IsidaShotRenderOptions
            {
                Hue = SyncHealWithDamage ? DamageHue : HealHue,
                Saturation = SyncHealWithDamage ? DamageSaturation : HealSaturation,
                Brightness = SyncHealWithDamage ? DamageBrightness : HealBrightness,
                Contrast = SyncHealWithDamage ? DamageContrast : HealContrast,
                Invert = SyncHealWithDamage ? DamageInvert : HealInvert,
            };
        }

        private void UpdatePreview()
        {
            int currentGen = Interlocked.Increment(ref _previewGeneration);
            IsidaShotComponent component = CurrentComponent;
            IsidaShotRenderOptions options = IsDamageComponent(component)
                ? ToDamageRenderOptions()
                : ToHealRenderOptions();

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

        private async Task SaveIsidaEffectAsync()
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
                _mainVM.UpdateStatus = "Generating and applying custom Isida shot effect...";

                string templateDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Isida"
                );

                string customDir = Path.Combine(templateDir, "Custom");
                _ = Directory.CreateDirectory(customDir);

                IsidaShotRenderOptions damageOptions = ToDamageRenderOptions();
                IsidaShotRenderOptions healOptions = ToHealRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveTara(
                        damageOptions,
                        Path.Combine(customDir, Constants.IsidaDamageBallTarget),
                        IsidaShotComponent.DamageBall
                    );
                    _composer.SaveImage(
                        damageOptions,
                        Path.Combine(customDir, Constants.IsidaDamageBeamImageTarget),
                        IsidaShotComponent.DamageBeam
                    );
                    _composer.SaveTara(
                        damageOptions,
                        Path.Combine(customDir, Constants.IsidaDamageImpactTarget),
                        IsidaShotComponent.DamageImpact
                    );
                    _composer.SaveTara(
                        damageOptions,
                        Path.Combine(customDir, Constants.IsidaDamageFlareTarget),
                        IsidaShotComponent.DamageFlare
                    );

                    string damageBeamAlphaSrc = Path.Combine(
                        templateDir,
                        "isida_damage_beam_alpha.jpg"
                    );
                    if (File.Exists(damageBeamAlphaSrc))
                    {
                        File.Copy(
                            damageBeamAlphaSrc,
                            Path.Combine(customDir, Constants.IsidaDamageBeamAlphaTarget),
                            true
                        );
                    }

                    _composer.SaveTara(
                        healOptions,
                        Path.Combine(customDir, Constants.IsidaHealBallTarget),
                        IsidaShotComponent.HealBall
                    );
                    _composer.SaveImage(
                        healOptions,
                        Path.Combine(customDir, Constants.IsidaHealBeamImageTarget),
                        IsidaShotComponent.HealBeam
                    );
                    _composer.SaveImage(
                        healOptions,
                        Path.Combine(customDir, Constants.IsidaHealShaftImageTarget),
                        IsidaShotComponent.HealShaft
                    );

                    string healBeamAlphaSrc = Path.Combine(
                        templateDir,
                        "isida_heal_beam_alpha.jpg"
                    );
                    if (File.Exists(healBeamAlphaSrc))
                    {
                        File.Copy(
                            healBeamAlphaSrc,
                            Path.Combine(customDir, Constants.IsidaHealBeamAlphaTarget),
                            true
                        );
                    }

                    string healShaftAlphaSrc = Path.Combine(
                        templateDir,
                        "isida_heal_shaft_alpha.jpg"
                    );
                    if (File.Exists(healShaftAlphaSrc))
                    {
                        File.Copy(
                            healShaftAlphaSrc,
                            Path.Combine(customDir, Constants.IsidaHealShaftAlphaTarget),
                            true
                        );
                    }
                });

                List<string> potentialTargets =
                [
                    Constants.IsidaDamageBallTarget,
                    Constants.IsidaDamageBeamImageTarget,
                    Constants.IsidaDamageBeamAlphaTarget,
                    Constants.IsidaDamageImpactTarget,
                    Constants.IsidaDamageFlareTarget,
                    Constants.IsidaHealBallTarget,
                    Constants.IsidaHealBeamImageTarget,
                    Constants.IsidaHealBeamAlphaTarget,
                    Constants.IsidaHealShaftImageTarget,
                    Constants.IsidaHealShaftAlphaTarget,
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
                    Turret = "Isida",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Isida/Custom",
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
                        "Custom Isida shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Isida are not cached by the loader yet. Please launch the game with Isida equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Isida effect: {result}";
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
