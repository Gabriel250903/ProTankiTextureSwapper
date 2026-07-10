using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Input;
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

        public ObservableCollection<string> ShotEffectTurrets { get; } = [];
        public ObservableCollection<ShotEffectModel> FilteredShotEffects { get; } = [];

        public string SelectedShotEffectTurret
        {
            get => _selectedShotEffectTurret;
            set
            {
                if (SetProperty(ref _selectedShotEffectTurret, value))
                {
                    FilterShotEffects();
                }
            }
        }

        public ShotEffectModel? SelectedShotEffect
        {
            get => _selectedShotEffect;
            set => SetProperty(ref _selectedShotEffect, value);
        }

        public ICommand SwapShotEffectCommand { get; }
        public ICommand ToggleShotEffectSelectionCommand { get; }

        public ShotEffectsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            SwapShotEffectCommand = new AsyncRelayCommand(ExecuteSwapShotEffect, _ => !_mainVM.IsLoading && _allShotEffects.Any(e => e.IsSelected));
            ToggleShotEffectSelectionCommand = new RelayCommand(ExecuteToggleShotEffectSelection);
        }

        public async Task LoadShotEffectsAsync()
        {
            try
            {
                _mainVM.IsLoading = true;
                List<ShotEffectModel> effects = await _mainVM.SkinSyncService.SyncAndLoadShotEffectsAsync();
                _allShotEffects = effects;

                IOrderedEnumerable<string> turrets = _allShotEffects.Select(e => e.Turret).Distinct().OrderBy(t => t);
                ShotEffectTurrets.Clear();
                foreach (string turret in turrets)
                {
                    ShotEffectTurrets.Add(turret);
                }

                SelectedShotEffectTurret = ShotEffectTurrets.FirstOrDefault(t => t.Equals("Railgun", StringComparison.OrdinalIgnoreCase)) ?? ShotEffectTurrets.FirstOrDefault() ?? string.Empty;
                FilterShotEffects();
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
    }
}
