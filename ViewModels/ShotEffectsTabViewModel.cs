using Serilog;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class ShotEffectsTabViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private string _selectedShotEffectTurret = string.Empty;
        private ShotEffectModel? _selectedShotEffect;
        private List<ShotEffectModel> _allShotEffects = [];

        private byte[]? _templatePixels;
        private int _templateWidth;
        private int _templateHeight;

        public IsidaBeamViewModel DamageBeam { get; }
        public IsidaBeamViewModel HealingBeam { get; }

        public ObservableCollection<string> ShotEffectTurrets { get; } = [];
        public ObservableCollection<ShotEffectModel> FilteredShotEffects { get; } = [];
        public ObservableCollection<string> BlendModes { get; } = ["Normal", "Multiply", "Screen", "Overlay", "Additive"];

        public string SelectedShotEffectTurret
        {
            get => _selectedShotEffectTurret;
            set
            {
                if (SetProperty(ref _selectedShotEffectTurret, value))
                {
                    FilterShotEffects();
                    OnPropertyChanged(nameof(IsCustomIsidaSelected));
                }
            }
        }

        public ShotEffectModel? SelectedShotEffect
        {
            get => _selectedShotEffect;
            set => SetProperty(ref _selectedShotEffect, value);
        }

        public bool IsCustomIsidaSelected => SelectedShotEffectTurret.Equals("Isida", StringComparison.OrdinalIgnoreCase);

        public ICommand SwapShotEffectCommand { get; }
        public ICommand ToggleShotEffectSelectionCommand { get; }
        public ICommand ApplyCustomIsidaEffectCommand { get; }

        public ShotEffectsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;

            DamageBeam = new IsidaBeamViewModel("Damage", 0.0, UpdatePreviews);
            HealingBeam = new IsidaBeamViewModel("Healing", 120.0, UpdatePreviews);

            SwapShotEffectCommand = new AsyncRelayCommand(ExecuteSwapShotEffect, _ => !_mainVM.IsLoading && _allShotEffects.Any(e => e.IsSelected));
            ToggleShotEffectSelectionCommand = new RelayCommand(ExecuteToggleShotEffectSelection);
            ApplyCustomIsidaEffectCommand = new AsyncRelayCommand(ExecuteApplyCustomIsidaEffect, _ => !_mainVM.IsLoading);
        }

        public async Task LoadShotEffectsAsync()
        {
            try
            {
                _mainVM.IsLoading = true;
                List<ShotEffectModel> effects = await _mainVM.SkinSyncService.SyncAndLoadShotEffectsAsync();
                _allShotEffects = effects;

                IOrderedEnumerable<string> turrets = _allShotEffects.Select(e => e.Turret)
                    .Concat(["Isida"])
                    .Distinct()
                    .OrderBy(t => t);

                ShotEffectTurrets.Clear();
                foreach (string turret in turrets)
                {
                    ShotEffectTurrets.Add(turret);
                }

                SelectedShotEffectTurret = ShotEffectTurrets.FirstOrDefault(t => t.Equals("Railgun", StringComparison.OrdinalIgnoreCase)) ?? ShotEffectTurrets.FirstOrDefault() ?? string.Empty;
                FilterShotEffects();

                LoadTemplateImage();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load shot effects.");
            }
            finally
            {
                _mainVM.IsLoading = false;
            }
        }

        private void LoadTemplateImage()
        {
            try
            {
                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures/ShotEffects/Turrets/Isida/isida_gray.png");
                if (File.Exists(templatePath))
                {
                    Log.Information($"Loading Isida gray template from {templatePath}");
                    Uri uri = new(templatePath);
                    BitmapImage bitmap = new();
                    bitmap.BeginInit();
                    bitmap.UriSource = uri;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    FormatConvertedBitmap formattedBitmap = new(bitmap, PixelFormats.Bgr32, null, 0);
                    _templateWidth = formattedBitmap.PixelWidth;
                    _templateHeight = formattedBitmap.PixelHeight;
                    int stride = _templateWidth * 4;
                    _templatePixels = new byte[_templateHeight * stride];
                    formattedBitmap.CopyPixels(_templatePixels, stride, 0);

                    UpdatePreviews();
                }
                else
                {
                    Log.Warning($"Isida gray template image not found at: {templatePath}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load Isida gray template image.");
            }
        }

        private int _renderSessionId = 0;

        private async void UpdatePreviews()
        {
            if (_templatePixels == null)
            {
                return;
            }

            int sessionId = Interlocked.Increment(ref _renderSessionId);

            try
            {
                Task<BitmapSource> damageTask = Task.Run(() => DamageBeam.Render(_templatePixels, _templateWidth, _templateHeight));
                Task<BitmapSource> healingTask = Task.Run(() => HealingBeam.Render(_templatePixels, _templateWidth, _templateHeight));

                _ = await Task.WhenAll(damageTask, healingTask);

                if (sessionId == _renderSessionId)
                {
                    DamageBeam.Preview = damageTask.Result;
                    HealingBeam.Preview = healingTask.Result;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to render Isida beam previews.");
            }
        }

        private void FilterShotEffects()
        {
            FilteredShotEffects.Clear();
            IEnumerable<ShotEffectModel> filtered = _allShotEffects.Where(e => e.Turret.Equals(SelectedShotEffectTurret, StringComparison.OrdinalIgnoreCase));
            foreach (ShotEffectModel effect in filtered)
            {
                FilteredShotEffects.Add(effect);
            }
        }

        private void ExecuteToggleShotEffectSelection(object? parameter)
        {
            if (parameter is ShotEffectModel effect)
            {
                effect.IsSelected = !effect.IsSelected;
                Log.Information($"Toggled selection for shot effect: {effect.Name}. New state: {effect.IsSelected}");

                if (effect.IsSelected)
                {
                    foreach (ShotEffectModel otherEffect in _allShotEffects)
                    {
                        if (otherEffect != effect && otherEffect.Turret == effect.Turret && otherEffect.IsSelected)
                        {
                            otherEffect.IsSelected = false;
                            Log.Information($"Deselected conflicting shot effect {otherEffect.Name} because {effect.Name} was selected.");
                        }
                    }
                }

                CommandManager.InvalidateRequerySuggested();
            }
        }

        private async Task ExecuteSwapShotEffect(object? parameter)
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            ShotEffectModel? selectedEffect = _allShotEffects.FirstOrDefault(e => e.IsSelected);
            if (selectedEffect == null)
            {
                await _mainVM.NotificationService.ShowAsync("Error", "Please select a shot effect first.", ControlAppearance.Danger);
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                _mainVM.IsLoading = true;
                _mainVM.UpdateStatus = $"Applying shot effect: {selectedEffect.Turret} {selectedEffect.Name}...";

                string? result = await Task.Run(() => _mainVM.SwapService.SwapShotEffect(_mainVM.CachePath, selectedEffect));
                await Task.Delay(2000);
                if (result == null)
                {
                    notificationTitle = "Success";
                    notificationMessage = $"{selectedEffect.Turret} {selectedEffect.Name} shot effect applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage = $"The shot effect assets for {selectedEffect.Turret} are not cached by the loader yet. Please launch the game with this turret equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else if (result == "CacheWithExtensions")
                {
                    notificationTitle = "Notice";
                    notificationMessage = "The cache files seem to have extensions added to them (e.g. .png, .jpg). Please rename them back to plain files without extensions so the app can recognize and swap them.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = "Error";
                    notificationMessage = $"Failed to apply shot effect: {result}";
                    notificationAppearance = ControlAppearance.Danger;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to swap shot effect.");
                notificationTitle = "Error";
                notificationMessage = $"Swap error: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                _mainVM.IsLoading = false;
                _mainVM.UpdateStatus = string.Empty;
                await _mainVM.NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }

        private async Task ExecuteApplyCustomIsidaEffect(object? parameter)
        {
            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            if (_templatePixels == null)
            {
                await _mainVM.NotificationService.ShowAsync("Error", "Isida gray template image is not loaded.", ControlAppearance.Danger);
                return;
            }

            if (DamageBeam.Preview == null || HealingBeam.Preview == null)
            {
                await _mainVM.NotificationService.ShowAsync("Error", "Previews are not ready.", ControlAppearance.Danger);
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                _mainVM.IsLoading = true;
                _mainVM.UpdateStatus = "Generating and applying custom Isida shot effect...";

                string customDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Textures/ShotEffects/Turrets/Isida/Custom");
                _ = Directory.CreateDirectory(customDir);

                string damageFile = Path.Combine(customDir, "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3Ny8zMTczMTU1MTIwNDAyNy9pbWFnZS5qcGc=");
                string healingFile = Path.Combine(customDir, "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMwMi8zMTczMTU1MTIwNDEyNy9pbWFnZS5qcGc=");

                await Task.Run(() =>
                {
                    SaveBitmapToJpeg(DamageBeam.Preview, damageFile);
                    SaveBitmapToJpeg(HealingBeam.Preview, healingFile);
                });

                ShotEffectModel customEffect = new()
                {
                    Turret = "Isida",
                    Name = "Custom",
                    SourceFolder = "Textures/ShotEffects/Turrets/Isida/Custom",
                    PreviewImage = "",
                    Targets =
                    [
                        "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzI3Ny8zMTczMTU1MTIwNDAyNy9pbWFnZS5qcGc=",
                        "aHR0cDovLzE0Ni41OS4xMTAuMTAzLzAvMC8zLzMwMi8zMTczMTU1MTIwNDEyNy9pbWFnZS5qcGc="
                    ]
                };

                string? result = await Task.Run(() => _mainVM.SwapService.SwapShotEffect(_mainVM.CachePath, customEffect));
                await Task.Delay(1000);
                if (result == null)
                {
                    notificationTitle = "Success";
                    notificationMessage = "Custom Isida shot effect generated and applied successfully!";
                    notificationAppearance = ControlAppearance.Success;
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = "Notice";
                    notificationMessage = "The shot effect assets for Isida are not cached by the loader yet. Please launch the game with Isida equipped first.";
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
                Log.Error(ex, "Failed to generate custom Isida effect.");
                notificationTitle = "Error";
                notificationMessage = $"Error: {ex.Message}";
                notificationAppearance = ControlAppearance.Danger;
            }
            finally
            {
                _mainVM.IsLoading = false;
                _mainVM.UpdateStatus = string.Empty;
                await _mainVM.NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }

        private static void SaveBitmapToJpeg(BitmapSource source, string path)
        {
            JpegBitmapEncoder encoder = new() { QualityLevel = 95 };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using FileStream stream = new(path, FileMode.Create);
            encoder.Save(stream);
        }
    }
}
