using Serilog;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
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
            RestoreBackupCommand = new AsyncRelayCommand(ExecuteRestoreBackup, _ => !_mainVM.IsLoading && SelectedBackup != null);
        }

        public void LoadBackups()
        {
            SelectedBackup = null;
            SnapshotBackups.Clear();
            try
            {
                List<BackupModel> backups = _mainVM.SkinSyncService.LoadBackups();
                foreach (BackupModel backup in backups)
                {
                    SnapshotBackups.Add(backup);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load backups.");
            }
        }

        private async Task ExecuteRestoreBackup(object? parameter)
        {
            if (SelectedBackup == null)
            {
                return;
            }

            if (!await _mainVM.EnsureSafeToOperate())
            {
                return;
            }

            string notificationTitle = string.Empty;
            string notificationMessage = string.Empty;
            ControlAppearance notificationAppearance = ControlAppearance.Info;

            try
            {
                _mainVM.IsLoading = true;
                _mainVM.UpdateStatus = $"Restoring from {SelectedBackup.DisplayName}...";
                Log.Information($"Restoring cache snapshot from backup: {SelectedBackup.DisplayName} (Path: {SelectedBackup.FullPath})");
                bool success = await Task.Run(() => _mainVM.SwapService.RestoreFromBackup(_mainVM.CachePath, SelectedBackup.FullPath));
                if (success)
                {
                    notificationTitle = "Success";
                    notificationMessage = $"Restored from {SelectedBackup.DisplayName}";
                    notificationAppearance = ControlAppearance.Success;

                    _mainVM.UpdateStatus = $"Restoring from backup: {SelectedBackup.DisplayName}";
                    await Task.Delay(2500);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore from snapshot.");
                notificationTitle = "Error";
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
                await _mainVM.NotificationService.ShowAsync(notificationTitle, notificationMessage, notificationAppearance);
            }
        }
    }
}
