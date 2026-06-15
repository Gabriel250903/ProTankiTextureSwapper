using TextureSwapper.Services;
using TextureSwapper.ViewModels;
using Wpf.Ui.Controls;

namespace TextureSwapper
{
    public partial class MainWindow : FluentWindow, INotificationService
    {
        private readonly MainViewModel _mainViewModel;
        private readonly UpdateService _updateService;

        public MainWindow()
        {
            _updateService = new UpdateService();
            _mainViewModel = new MainViewModel(this, _updateService);
            _mainViewModel.RequestSettings += OnRequestSettings;

            DataContext = _mainViewModel;

            InitializeComponent();
        }

        private void OnRequestSettings()
        {
            SettingsViewModel settingsViewModel = new(_mainViewModel.Settings, _mainViewModel._settingsService, _mainViewModel);
            SettingsWindow settingsWindow = new(settingsViewModel)
            {
                Owner = this
            };
            _ = settingsWindow.ShowDialog();
        }

        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            ToastInfoBar.Title = title;
            ToastInfoBar.Message = message;
            ToastInfoBar.Severity = appearance switch
            {
                ControlAppearance.Primary => InfoBarSeverity.Informational,
                ControlAppearance.Success => InfoBarSeverity.Success,
                ControlAppearance.Danger => InfoBarSeverity.Error,
                ControlAppearance.Caution => InfoBarSeverity.Warning,
                ControlAppearance.Info => InfoBarSeverity.Informational,
                _ => InfoBarSeverity.Informational
            };

            ToastInfoBar.IsOpen = true;
            await Task.Delay(3000);
            ToastInfoBar.IsOpen = false;
        }
    }
}
