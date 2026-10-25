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
    public class TwinsShotViewModel : ViewModelBase
    {
        private readonly ITwinsShotComposer _composer;
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

        public TwinsShotViewModel(ITwinsShotComposer composer, MainViewModel mainVM)
        {
            _composer = composer;
            _mainVM = mainVM;

            SaveCommand = new AsyncRelayCommand(
                _ => SaveTwinsEffectAsync(),
                _ => !_mainVM.IsLoading
            );
            RestoreCommand = new AsyncRelayCommand(
                _ => RestoreOriginalsAsync(),
                _ => !_mainVM.IsLoading
            );

            UpdatePreview();
        }

        public TwinsShotRenderOptions ToRenderOptions()
        {
            return new TwinsShotRenderOptions
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
            TwinsShotRenderOptions options = ToRenderOptions();

            _ = Task.Run(() =>
            {
                BitmapSource preview = _composer.ComposeBallPreview(options, true);
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

        private async Task SaveTwinsEffectAsync()
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
                _mainVM.UpdateStatus = "Generating and applying custom Twins shot effect...";

                string customDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Textures",
                    "ShotEffects",
                    "Turrets",
                    "Twins",
                    "Custom"
                );
                _ = Directory.CreateDirectory(customDir);

                string mainFile = Path.Combine(customDir, Constants.TwinsMainBallTarget);
                string endFile = Path.Combine(customDir, Constants.TwinsBounceBallTarget);

                TwinsShotRenderOptions options = ToRenderOptions();

                await Task.Run(() =>
                {
                    _composer.SaveBallTara(options, mainFile, true);
                    _composer.SaveBallTara(options, endFile, false);
                });

                ShotEffectModel customEffect = new()
                {
                    Turret = "Twins",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Twins/Custom",
                    PreviewImage = "",
                    Targets = [Constants.TwinsMainBallTarget, Constants.TwinsBounceBallTarget],
                };

                string? result = await Task.Run(() =>
                    _mainVM.SwapService.SwapShotEffect(_mainVM.CachePath, customEffect)
                );
                await Task.Delay(1000);
                if (result == null)
                {
                    notificationTitle = "Success";
                    notificationMessage =
                        "Custom Twins shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage =
                        "The shot effect assets for Twins are not cached by the loader yet. Please launch the game with Twins equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply custom Twins effect: {result}";
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
