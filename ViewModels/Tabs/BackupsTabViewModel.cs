using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Serilog;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public class BackupsTabViewModel : ViewModelBase
    {
        private readonly MainViewModel _mainVM;
        private BackupModel? _selectedBackup;

        public ObservableCollection<BackupModel> SnapshotBackups { get; } = [];

        public BackupModel? SelectedBackup
        {
            get => _selectedBackup;
            set => SetProperty(ref _selectedBackup, value);
        }

        public ICommand RestoreBackupCommand { get; }

        public BackupsTabViewModel(MainViewModel mainVM)
        {
            _mainVM = mainVM;
            RestoreBackupCommand = new AsyncRelayCommand(
                ExecuteRestoreBackup,
                param => !_mainVM.IsLoading && (param is BackupModel || SelectedBackup != null)
            );
        }

        public async Task LoadBackupsAsync()
        {
            try
            {
                List<BackupModel> backups = await Task.Run(() =>
                    _mainVM.SkinSyncService.LoadBackups()
                );
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    SelectedBackup = null;
                    SnapshotBackups.Clear();
                    foreach (BackupModel backup in backups)
                    {
                        SnapshotBackups.Add(backup);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load backups.");
            }
        }

        public void LoadBackups()
        {
            _ = LoadBackupsAsync();
        }

        private async Task ExecuteRestoreBackup(object? parameter)
        {
            BackupModel? backupToRestore = parameter as BackupModel ?? SelectedBackup;
            if (backupToRestore == null)
            {
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            if (
                !_mainVM.Settings.IsPremiumUnlocked
                && (
                    backupToRestore.DisplayName.Contains(
                        "Isida",
                        StringComparison.OrdinalIgnoreCase
                    )
                    || backupToRestore.DisplayName.Contains(
                        "Parachute",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
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
                _mainVM.UpdateStatus = $"Restoring from {backupToRestore.DisplayName}...";
                Log.Information(
                    $"Restoring cache snapshot from backup: {backupToRestore.DisplayName} (Path: {backupToRestore.FullPath})"
                );
                bool success = await Task.Run(() =>
                    _mainVM.SwapService.RestoreFromBackup(
                        _mainVM.CachePath,
                        backupToRestore.FullPath
                    )
                );
                if (success)
                {
                    notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Success");
                    notificationMessage = _mainVM.LocalizationService.GetString(
                        "Loc_Msg_SnapshotRestoredSuccess"
                    );
                    notificationAppearance = ControlAppearance.Success;

                    _mainVM.UpdateStatus = $"Restoring from backup: {backupToRestore.DisplayName}";
                    await Task.Delay(2500);
                }
                else
                {
                    notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Error");
                    notificationMessage =
                        $"Failed to restore from backup: {backupToRestore.DisplayName}";
                    notificationAppearance = ControlAppearance.Danger;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore from snapshot.");
                notificationTitle = _mainVM.LocalizationService.GetString("Loc_Msg_Error");
                notificationMessage = $"Restore failed: {ex.Message}";
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
