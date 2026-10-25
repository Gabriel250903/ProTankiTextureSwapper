using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public class DustEffectViewModel : ViewModelBase
    {
        private readonly IDustEffectComposer _composer;
        private readonly MainViewModel _mainVM;

        public List<string> ComponentOptions { get; } =
        ["Ground Mound", "Rising Dust", "Dust Trail 1", "Dust Trail 2"];

        private string _selectedComponentOption = "Ground Mound";
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

        private DustEffectComponent CurrentComponent =>
            _selectedComponentOption switch
            {
                "Rising Dust" => DustEffectComponent.Smoke,
                "Dust Trail 1" => DustEffectComponent.Tracer,
                "Dust Trail 2" => DustEffectComponent.Trail,
                _ => DustEffectComponent.Mound,
            };

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
            private set => SetProperty(ref _previewImage, value);
        }

        public ICommand SwapEffectCommand { get; }
        public ICommand RestoreOriginalsCommand { get; }
        public ICommand SaveCommand => SwapEffectCommand;
        public ICommand RestoreCommand => RestoreOriginalsCommand;
        public ICommand ApplyPresetCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        public DustEffectViewModel(IDustEffectComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SwapEffectCommand = new AsyncRelayCommand(
                _ => ExecuteSwapEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreOriginalsCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );
            ApplyPresetCommand = new RelayCommand(ExecuteApplyPreset);
            ResetSettingsCommand = new RelayCommand(_ => ResetSettings());

            UpdatePreview();
        }

        private void ResetSettings()
        {
            _hue = 0.0;
            _saturation = 100.0;
            _brightness = 0.0;
            _contrast = 1.0;
            _invert = false;

            OnPropertyChanged(nameof(Hue));
            OnPropertyChanged(nameof(Saturation));
            OnPropertyChanged(nameof(Brightness));
            OnPropertyChanged(nameof(Contrast));
            OnPropertyChanged(nameof(Invert));

            UpdatePreview();
        }

        public DustEffectRenderOptions ToRenderOptions()
        {
            return new DustEffectRenderOptions
            {
                Hue = _hue,
                Saturation = _saturation,
                Brightness = _brightness,
                Contrast = _contrast,
                Invert = _invert,
            };
        }

        public void UpdatePreview()
        {
            DustEffectComponent component = CurrentComponent;
            DustEffectRenderOptions options = ToRenderOptions();

            Application.Current?.Dispatcher.Invoke(() =>
            {
                try
                {
                    PreviewImage = _composer.ComposePreview(options, component);
                }
                catch
                {
                    Log.Error(
                        $"Failed to generate preview for component {component} with options: Hue={options.Hue}, Saturation={options.Saturation}, Brightness={options.Brightness}, Contrast={options.Contrast}, Invert={options.Invert}"
                    );
                }
            });
        }

        private void ExecuteApplyPreset(object? parameter)
        {
            if (parameter is not string presetName)
            {
                return;
            }

            switch (presetName.ToLowerInvariant())
            {
                case "red":
                    Hue = 0;
                    Saturation = 100;
                    Brightness = 0;
                    Contrast = 1.2;
                    Invert = false;
                    break;
                case "green":
                    Hue = 120;
                    Saturation = 100;
                    Brightness = 0;
                    Contrast = 1.2;
                    Invert = false;
                    break;
                case "blue":
                    Hue = 220;
                    Saturation = 100;
                    Brightness = 0;
                    Contrast = 1.2;
                    Invert = false;
                    break;
                case "purple":
                    Hue = 280;
                    Saturation = 100;
                    Brightness = 10;
                    Contrast = 1.2;
                    Invert = false;
                    break;
                case "orange":
                    Hue = 30;
                    Saturation = 100;
                    Brightness = 0;
                    Contrast = 1.2;
                    Invert = false;
                    break;
                case "cyan":
                    Hue = 180;
                    Saturation = 100;
                    Brightness = 10;
                    Contrast = 1.1;
                    Invert = false;
                    break;
                case "white":
                    Hue = 0;
                    Saturation = 0;
                    Brightness = 30;
                    Contrast = 1.5;
                    Invert = false;
                    break;
                case "dark":
                    Hue = 0;
                    Saturation = 0;
                    Brightness = -40;
                    Contrast = 1.8;
                    Invert = true;
                    break;
                case "golden":
                    Hue = 45;
                    Saturation = 100;
                    Brightness = 15;
                    Contrast = 1.3;
                    Invert = false;
                    break;
            }
        }

        private async Task ExecuteSwapEffectAsync()
        {
            if (!_mainVM.Settings.IsPremiumUnlocked)
            {
                await _mainVM.NotificationService.ShowAsync(
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequired"),
                    _mainVM.LocalizationService.GetString("Loc_Msg_PremiumRequiredDesc"),
                    ControlAppearance.Danger
                );
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            string notificationTitle;
            string notificationMessage;
            ControlAppearance notificationAppearance;

            try
            {
                _mainVM.IsLoading = true;
                _mainVM.UpdateStatus = "Applying custom effects...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Dust",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                DustEffectRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.DustMoundTarget),
                        DustEffectComponent.Mound
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.DustSmokeTarget),
                        DustEffectComponent.Smoke
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.DustTracerTarget),
                        DustEffectComponent.Tracer
                    );
                    _composer.SaveTara(
                        options,
                        Path.Combine(customDir, Constants.DustTrailTarget),
                        DustEffectComponent.Trail
                    );
                });

                List<string> potentialTargets =
                [
                    Constants.DustMoundTarget,
                    Constants.DustSmokeTarget,
                    Constants.DustTracerTarget,
                    Constants.DustTrailTarget,
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
                    Turret = "Dust",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Dust/Custom",
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
                    notificationMessage = "Custom effects applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "Track dust textures are not in cache yet. Please drive around in a battle to cache dust textures first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom effects: {result}";
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
