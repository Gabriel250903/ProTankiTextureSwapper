using Serilog;
using System.IO;
using System.Reflection;
using System.Windows.Input;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly MainViewModel _mainViewModel;

        public AppSettings Settings { get; }

        public string AppVersion => $"v{Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0"}";

        public string LastCheckedTime
        {
            get => Settings.LastCheckedTime;
            set
            {
                Settings.LastCheckedTime = value;
                OnPropertyChanged();
            }
        }

        public int BackupRetentionDays
        {
            get => Settings.MaxBackupRetentionDays;
            set
            {
                if (Settings.MaxBackupRetentionDays != value)
                {
                    int oldVal = Settings.MaxBackupRetentionDays;
                    Settings.MaxBackupRetentionDays = value;
                    _settingsService.Save(Settings);
                    OnPropertyChanged();
                    Log.Information("Settings changed: Max Backup Retention updated from {OldDays} to {NewDays} days.", oldVal, value);
                    ExecuteRefreshLogs(null);
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string UpdateStatus
        {
            get => Settings.LastUpdateStatus;
            set
            {
                Settings.LastUpdateStatus = value;
                OnPropertyChanged();
            }
        }

        private string _logContent = string.Empty;
        public string LogContent
        {
            get => _logContent;
            set => SetProperty(ref _logContent, value);
        }

        public ICommand CheckForUpdatesCommand { get; }
        public ICommand ToggleThemeCommand { get; }
        public ICommand RefreshLogsCommand { get; }
        public ICommand OpenLogsFolderCommand { get; }

        public SettingsViewModel(AppSettings settings, SettingsService settingsService, MainViewModel mainViewModel)
        {
            Settings = settings;
            _settingsService = settingsService;
            _mainViewModel = mainViewModel;

            CheckForUpdatesCommand = new AsyncRelayCommand(ExecuteCheckForUpdates, _ => !IsLoading);
            ToggleThemeCommand = new RelayCommand(ExecuteToggleTheme);
            RefreshLogsCommand = new RelayCommand(ExecuteRefreshLogs);
            OpenLogsFolderCommand = new RelayCommand(ExecuteOpenLogsFolder);

            ExecuteRefreshLogs(null);
        }

        private void ExecuteRefreshLogs(object? parameter)
        {
            try
            {
                string logFile = App.CurrentLogFilePath;
                if (!string.IsNullOrEmpty(logFile) && File.Exists(logFile))
                {
                    using FileStream stream = new(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using StreamReader reader = new(stream);
                    LogContent = reader.ReadToEnd();
                    return;
                }
                LogContent = "No log file found.";
            }
            catch (Exception ex)
            {
                LogContent = $"Error reading log file: {ex.Message}";
            }
        }

        private void ExecuteOpenLogsFolder(object? parameter)
        {
            try
            {
                string logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.LogsDir);
                if (Directory.Exists(logFolder))
                {
                    _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = logFolder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open logs directory.");
            }
        }

        private async Task ExecuteCheckForUpdates(object? parameter)
        {
            try
            {
                IsLoading = true;
                UpdateStatus = "Connecting to GitHub...";
                LastCheckedTime = DateTime.Now.ToString("g");
                Log.Information("User initiated manual app update check. Querying API...");

                await _mainViewModel.PerformUpdateCheckAsync(p => UpdateStatus = p);

                if (UpdateStatus == "Checking for updates...")
                {
                    UpdateStatus = "You are up to date!";
                    Log.Information("Manual app update check complete. App is already up to date.");
                }
                else
                {
                    Log.Information("Manual app update check complete. Status: {Status}", UpdateStatus);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Update check failed.");
                UpdateStatus = "Error: Check connection.";
                await _mainViewModel._notificationService.ShowAsync("Update Error", "Failed to reach GitHub API. Please check your internet connection.", ControlAppearance.Danger);
            }
            finally
            {
                IsLoading = false;
                _settingsService.Save(Settings);
                ExecuteRefreshLogs(null);
            }
        }

        private void ExecuteToggleTheme(object? parameter)
        {
            ApplicationTheme currentTheme = ApplicationThemeManager.GetAppTheme();
            ApplicationTheme newTheme = currentTheme == ApplicationTheme.Light ? ApplicationTheme.Dark : ApplicationTheme.Light;

            ApplicationThemeManager.Apply(newTheme);
            Settings.Theme = newTheme;
            _settingsService.Save(Settings);
            Log.Information("User toggled application theme from {OldTheme} to {NewTheme}.", currentTheme, newTheme);
            ExecuteRefreshLogs(null);
        }
    }
}
