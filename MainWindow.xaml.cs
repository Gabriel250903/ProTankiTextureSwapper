using Serilog;
using TextureSwapper.Services;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper
{
    public partial class MainWindow : FluentWindow, INotificationService
    {
        private readonly MainViewModel _viewModel;
        public MainWindow()
        {
            Log.Information("MainWindow initializing with MVVM...");

            _viewModel = new MainViewModel(this);

            DataContext = _viewModel;
            InitializeComponent();

            ApplicationThemeManager.Apply(_viewModel.Settings.Theme);
            UpdateThemeIcon(_viewModel.Settings.Theme);
        }

        private void BtnThemeToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplicationTheme currentTheme = ApplicationThemeManager.GetAppTheme();
            ApplicationTheme newTheme = currentTheme == ApplicationTheme.Light ? ApplicationTheme.Dark : ApplicationTheme.Light;

            Log.Information("Toggling theme to {Theme}", newTheme);
            ApplicationThemeManager.Apply(newTheme);
            _viewModel.SaveTheme(newTheme);

            UpdateThemeIcon(newTheme);
        }

        private void UpdateThemeIcon(ApplicationTheme theme)
        {
            btnThemeToggle.Icon = theme == ApplicationTheme.Light
                ? new SymbolIcon { Symbol = SymbolRegular.WeatherMoon24 }
                : new SymbolIcon { Symbol = SymbolRegular.WeatherSunny24 };
        }


        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            ToastInfoBar.Title = title;
            ToastInfoBar.Message = message;

            ToastInfoBar.Severity = appearance switch
            {
                ControlAppearance.Success => InfoBarSeverity.Success,
                ControlAppearance.Danger => InfoBarSeverity.Error,
                ControlAppearance.Info => InfoBarSeverity.Informational,
                _ => InfoBarSeverity.Informational
            };

            ToastInfoBar.IsOpen = true;
            await Task.Delay(3000);
            ToastInfoBar.IsOpen = false;
        }
    }
}
