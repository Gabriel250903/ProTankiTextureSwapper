using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public class HammerShotViewModel : ViewModelBase
    {
        private readonly IHammerShotComposer _composer;
        private readonly MainViewModel _mainVM;
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

        public HammerShotViewModel(IHammerShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveHammerEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public HammerShotRenderOptions ToRenderOptions()
        {
            return new HammerShotRenderOptions
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
            HammerShotRenderOptions options = ToRenderOptions();

            _ = Task.Run(() =>
            {
                BitmapSource preview = _composer.ComposePreview(options);
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

        private async Task SaveHammerEffectAsync()
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
                _mainVM.UpdateStatus = "Generating and applying custom Hammer shot effect...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Hammer",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                string acrossFile = Path.Combine(customDir, Constants.HammerShotAcrossTarget);
                string acrossRev2File = Path.Combine(
                    customDir,
                    Constants.HammerShotAcrossRev2Target
                );
                string alongFile = Path.Combine(customDir, Constants.HammerShotAlongTarget);
                string smokeFile = Path.Combine(customDir, Constants.HammerSmokeTarget);

                string pelletImgFile = Path.Combine(
                    customDir,
                    Constants.HammerPelletTrailImageTarget
                );
                string pelletImgRev2File = Path.Combine(
                    customDir,
                    Constants.HammerPelletTrailImageRev2Target
                );
                string sparkleFile = Path.Combine(customDir, Constants.HammerSparkleTarget);
                string sparkleRev2File = Path.Combine(customDir, Constants.HammerSparkleRev2Target);

                HammerShotRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveShotTara(options, acrossFile, HammerShotComponent.ShotAcross);
                    _composer.SaveShotTara(options, acrossRev2File, HammerShotComponent.ShotAcross);
                    _composer.SaveShotTara(options, alongFile, HammerShotComponent.ShotAlong);
                    _composer.SaveShotTara(options, smokeFile, HammerShotComponent.Smoke);

                    _composer.SaveShotImage(
                        options,
                        pelletImgFile,
                        HammerShotComponent.PelletTrail
                    );
                    _composer.SaveShotImage(
                        options,
                        pelletImgRev2File,
                        HammerShotComponent.PelletTrail
                    );
                    _composer.SaveShotImage(options, sparkleFile, HammerShotComponent.Sparkle);
                    _composer.SaveShotImage(options, sparkleRev2File, HammerShotComponent.Sparkle);
                });

                ShotEffectModel customEffect = new()
                {
                    Turret = "Hammer",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Hammer/Custom",
                    PreviewImage = "",
                    Targets =
                    [
                        Constants.HammerShotAcrossTarget,
                        Constants.HammerShotAcrossRev2Target,
                        Constants.HammerShotAlongTarget,
                        Constants.HammerSmokeTarget,
                        Constants.HammerPelletTrailImageTarget,
                        Constants.HammerPelletTrailImageRev2Target,
                        Constants.HammerSparkleTarget,
                        Constants.HammerSparkleRev2Target,
                    ],
                };

                string? result = await Task.Run(() =>
                    _mainVM.SwapService.SwapShotEffect(_mainVM.CachePath, customEffect)
                );
                await Task.Delay(1000);
                if (result == null)
                {
                    notificationTitle = "Success";
                    notificationMessage =
                        "Custom Hammer shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Hammer are not cached by the loader yet. Please launch the game with Hammer equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Hammer effect: {result}";
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
