using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Serilog;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public class ShotEffectsTabViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private string _selectedShotEffectTurret = string.Empty;
        private ShotEffectModel? _selectedShotEffect;
        private List<ShotEffectModel> _allShotEffects = [];
        public RicochetShotViewModel RicochetShot { get; }
        public FreezeShotViewModel FreezeShot { get; }
        public TwinsShotViewModel TwinsShot { get; }
        public FirebirdShotViewModel FirebirdShot { get; }
        public RailgunShotViewModel RailgunShot { get; }
        public HammerShotViewModel HammerShot { get; }
        public IsidaShotViewModel IsidaShot { get; }
        public ShaftShotViewModel ShaftShot { get; }
        public VulcanShotViewModel VulcanShot { get; }
        public ThunderShotViewModel ThunderShot { get; }
        public DustEffectViewModel DustEffect { get; }
        public SmokyShotViewModel SmokyShot { get; }
        public ObservableCollection<string> ShotEffectTurrets { get; } = [];
        public ObservableCollection<ShotEffectModel> FilteredShotEffects { get; } = [];
        public ObservableCollection<string> BlendModes { get; } =
        ["Normal", "Multiply", "Screen", "Overlay", "Additive"];

        public string SelectedShotEffectTurret
        {
            get => _selectedShotEffectTurret;
            set
            {
                if (SetProperty(ref _selectedShotEffectTurret, value))
                {
                    FilterShotEffects();
                    OnPropertyChanged(nameof(IsCustomRicochetSelected));
                    OnPropertyChanged(nameof(IsCustomFreezeSelected));
                    OnPropertyChanged(nameof(IsCustomTwinsSelected));
                    OnPropertyChanged(nameof(IsCustomFirebirdSelected));
                    OnPropertyChanged(nameof(IsCustomRailgunSelected));
                    OnPropertyChanged(nameof(IsCustomHammerSelected));
                    OnPropertyChanged(nameof(IsCustomIsidaSelected));
                    OnPropertyChanged(nameof(IsCustomShaftSelected));
                    OnPropertyChanged(nameof(IsCustomVulcanSelected));
                    OnPropertyChanged(nameof(IsCustomThunderSelected));
                    OnPropertyChanged(nameof(IsCustomDustSelected));
                    OnPropertyChanged(nameof(IsCustomOtherEffectsSelected));
                    OnPropertyChanged(nameof(IsCustomSmokySelected));
                    OnPropertyChanged(nameof(IsPremiumLockedForCurrentTurret));
                }
            }
        }

        public ShotEffectModel? SelectedShotEffect
        {
            get => _selectedShotEffect;
            set => SetProperty(ref _selectedShotEffect, value);
        }

        public bool IsCustomRicochetSelected =>
            SelectedShotEffectTurret.Equals("Ricochet", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomFreezeSelected =>
            SelectedShotEffectTurret.Equals("Freeze", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomTwinsSelected =>
            SelectedShotEffectTurret.Equals("Twins", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomFirebirdSelected =>
            SelectedShotEffectTurret.Equals("Firebird", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomRailgunSelected =>
            SelectedShotEffectTurret.Equals("Railgun", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomHammerSelected =>
            SelectedShotEffectTurret.Equals("Hammer", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomIsidaSelected =>
            SelectedShotEffectTurret.Equals("Isida", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomShaftSelected =>
            SelectedShotEffectTurret.Equals("Shaft", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomVulcanSelected =>
            SelectedShotEffectTurret.Equals("Vulcan", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomThunderSelected =>
            SelectedShotEffectTurret.Equals("Thunder", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomOtherEffectsSelected =>
            SelectedShotEffectTurret.Equals("Other effects", StringComparison.OrdinalIgnoreCase)
            || SelectedShotEffectTurret.Equals("Dust Effect", StringComparison.OrdinalIgnoreCase);
        public bool IsCustomDustSelected => IsCustomOtherEffectsSelected;
        public bool IsCustomSmokySelected =>
            SelectedShotEffectTurret.Equals("Smoky", StringComparison.OrdinalIgnoreCase);

        public bool IsPremiumUnlocked => _mainVM.Settings.IsPremiumUnlocked;
        public bool IsPremiumLocked => !IsPremiumUnlocked;
        public bool IsPremiumLockedForCurrentTurret =>
            !IsPremiumUnlocked
            && !SelectedShotEffectTurret.Equals("Railgun", StringComparison.OrdinalIgnoreCase);

        public void NotifyPremiumChanged()
        {
            OnPropertyChanged(nameof(IsPremiumUnlocked));
            OnPropertyChanged(nameof(IsPremiumLocked));
            OnPropertyChanged(nameof(IsPremiumLockedForCurrentTurret));
        }

        public ICommand BuyPremiumCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        public ICommand SwapShotEffectCommand { get; }
        public ICommand ToggleShotEffectSelectionCommand { get; }

        public ShotEffectsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;

            BuyPremiumCommand = new RelayCommand(_ =>
            {
                try
                {
                    _ = Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = "https://flytoxp.gumroad.com/l/sebwv",
                            UseShellExecute = true,
                        }
                    );
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to open premium link.");
                }
            });

            OpenSettingsCommand = new RelayCommand(_ =>
            {
                _mainVM.OpenSettingsCommand.Execute(null);
            });

            _mainVM.PremiumService.PremiumStatusChanged += (_, _) =>
            {
                Application.Current?.Dispatcher.Invoke(NotifyPremiumChanged);
            };

            RicochetShot = new RicochetShotViewModel(_mainVM.RicochetShotComposer, _mainVM);
            FreezeShot = new FreezeShotViewModel(_mainVM.FreezeShotComposer, _mainVM);
            TwinsShot = new TwinsShotViewModel(_mainVM.TwinsShotComposer, _mainVM);
            FirebirdShot = new FirebirdShotViewModel(_mainVM.FirebirdShotComposer, _mainVM);
            RailgunShot = new RailgunShotViewModel(_mainVM.RailgunShotComposer, _mainVM);
            HammerShot = new HammerShotViewModel(_mainVM.HammerShotComposer, _mainVM);
            IsidaShot = new IsidaShotViewModel(_mainVM.IsidaShotComposer, _mainVM);
            ShaftShot = new ShaftShotViewModel(_mainVM.ShaftShotComposer, _mainVM);
            VulcanShot = new VulcanShotViewModel(_mainVM.VulcanShotComposer, _mainVM);
            ThunderShot = new ThunderShotViewModel(_mainVM.ThunderShotComposer, _mainVM);
            DustEffect = new DustEffectViewModel(_mainVM.DustEffectComposer, _mainVM);
            SmokyShot = new SmokyShotViewModel(_mainVM.SmokyShotComposer, _mainVM);
            SwapShotEffectCommand = new AsyncRelayCommand(
                ExecuteSwapShotEffect,
                _ => !_mainVM.IsLoading && _allShotEffects.Any(e => e.IsSelected)
            );
            ToggleShotEffectSelectionCommand = new RelayCommand(ExecuteToggleShotEffectSelection);
        }

        public async Task LoadShotEffectsAsync()
        {
            try
            {
                _mainVM.IsLoading = true;
                List<ShotEffectModel> effects =
                    await _mainVM.SkinSyncService.SyncAndLoadShotEffectsAsync();
                _allShotEffects = effects;

                IOrderedEnumerable<string> turrets = _allShotEffects
                    .Select(e => e.Turret)
                    .Concat([
                        "Ricochet",
                        "Freeze",
                        "Twins",
                        "Firebird",
                        "Railgun",
                        "Hammer",
                        "Isida",
                        "Shaft",
                        "Vulcan",
                        "Thunder",
                        "Other effects",
                        "Smoky",
                    ])
                    .Distinct()
                    .OrderBy(t => t);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    ShotEffectTurrets.Clear();
                    foreach (string turret in turrets)
                    {
                        ShotEffectTurrets.Add(turret);
                    }

                    SelectedShotEffectTurret =
                        ShotEffectTurrets.FirstOrDefault(t =>
                            t.Equals("Railgun", StringComparison.OrdinalIgnoreCase)
                        )
                        ?? ShotEffectTurrets.FirstOrDefault()
                        ?? string.Empty;
                    FilterShotEffects();
                });
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
            Application.Current?.Dispatcher.Invoke(() =>
            {
                FilteredShotEffects.Clear();
                IEnumerable<ShotEffectModel> filtered = _allShotEffects.Where(e =>
                    e.Turret.Equals(SelectedShotEffectTurret, StringComparison.OrdinalIgnoreCase)
                );
                foreach (ShotEffectModel effect in filtered)
                {
                    FilteredShotEffects.Add(effect);
                }
            });
        }

        private void ExecuteToggleShotEffectSelection(object? parameter)
        {
            if (parameter is ShotEffectModel effect)
            {
                effect.IsSelected = !effect.IsSelected;
                Log.Information(
                    $"Toggled selection for shot effect: {effect.Name}. New state: {effect.IsSelected}"
                );

                if (effect.IsSelected)
                {
                    foreach (ShotEffectModel otherEffect in _allShotEffects)
                    {
                        if (
                            otherEffect != effect
                            && otherEffect.Turret == effect.Turret
                            && otherEffect.IsSelected
                        )
                        {
                            otherEffect.IsSelected = false;
                            Log.Information(
                                $"Deselected conflicting shot effect {otherEffect.Name} because {effect.Name} was selected."
                            );
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
                await _mainVM.NotificationService.ShowAsync(
                    _mainVM.LocalizationService.GetString("Loc_Msg_Error"),
                    _mainVM.LocalizationService.GetString("Loc_Msg_SelectShotEffectWarning"),
                    ControlAppearance.Danger
                );
                return;
            }

            if (
                !selectedEffect.Turret.Equals("Railgun", StringComparison.OrdinalIgnoreCase)
                && !_mainVM.Settings.IsPremiumUnlocked
            )
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
                _mainVM.UpdateStatus =
                    $"Applying shot effect: {selectedEffect.Turret} {selectedEffect.Name}...";

                string? result = await Task.Run(() =>
                    _mainVM.SwapService.SwapShotEffect(_mainVM.CachePath, selectedEffect)
                );
                await Task.Delay(2000);
                if (result == null)
                {
                    notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Success");
                    notificationMessage = _mainVM.LocalizationService.GetString(
                        "Loc_Msg_ShotEffectAppliedSuccess"
                    );
                    notificationAppearance = ControlAppearance.Success;
                    if (_mainVM.IsHalloweenThemeActive)
                    {
                        _mainVM.HalloweenSoundService.PlayApplySuccess();
                    }
                    _mainVM.BackupsTabVM.LoadBackups();
                }
                else if (result == "NotCached")
                {
                    notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Warning");
                    notificationMessage =
                        $"The shot effect assets for {selectedEffect.Turret} are not cached by the loader yet. Please launch the game with this turret equipped first.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else if (result == "CacheWithExtensions")
                {
                    notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Warning");
                    notificationMessage =
                        "The cache files seem to have extensions added to them (e.g. .png, .jpg). Please rename them back to plain files without extensions so the app can recognize and swap them.";
                    notificationAppearance = ControlAppearance.Caution;
                }
                else
                {
                    notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Error");
                    notificationMessage = $"Failed to apply shot effect: {result}";
                    notificationAppearance = ControlAppearance.Danger;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to swap shot effect.");
                notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Error");
                notificationMessage = $"Swap error: {ex.Message}";
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
    }
}
