using Serilog;
using System.Reflection;
using System.Windows.Input;
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
                    Settings.MaxBackupRetentionDays = value;
                    _settingsService.Save(Settings);
                    OnPropertyChanged();
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

        public ICommand CheckForUpdatesCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        public SettingsViewModel(AppSettings settings, SettingsService settingsService, MainViewModel mainViewModel)
        {
            Settings = settings;
            _settingsService = settingsService;
            _mainViewModel = mainViewModel;

            CheckForUpdatesCommand = new AsyncRelayCommand(ExecuteCheckForUpdates, _ => !IsLoading);
            ToggleThemeCommand = new RelayCommand(ExecuteToggleTheme);
        }

        private async Task ExecuteCheckForUpdates(object? parameter)
        {
            try
            {
                IsLoading = true;
                UpdateStatus = "Connecting to GitHub...";
                LastCheckedTime = DateTime.Now.ToString("g");

                await _mainViewModel.PerformUpdateCheckAsync(p => UpdateStatus = p);

                if (UpdateStatus == "Checking for updates...")
                {
                    UpdateStatus = "You are up to date!";
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
            }
        }

        private void ExecuteToggleTheme(object? parameter)
        {
            ApplicationTheme currentTheme = ApplicationThemeManager.GetAppTheme();
            ApplicationTheme newTheme = currentTheme == ApplicationTheme.Light ? ApplicationTheme.Dark : ApplicationTheme.Light;

            ApplicationThemeManager.Apply(newTheme);
            Settings.Theme = newTheme;
            _settingsService.Save(Settings);
        }
    }
}
