using TextureSwapper.Services;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
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

            DataContext = _mainViewModel;

            InitializeComponent();
        }

        private void BtnThemeToggle_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ApplicationTheme currentTheme = ApplicationThemeManager.GetAppTheme();
            ApplicationTheme newTheme = currentTheme == ApplicationTheme.Light ? ApplicationTheme.Dark : ApplicationTheme.Light;

            ApplicationThemeManager.Apply(newTheme);
            _mainViewModel.SaveTheme(newTheme);

            btnThemeToggle.Icon = new SymbolIcon(newTheme == ApplicationTheme.Light ? SymbolRegular.WeatherMoon24 : SymbolRegular.WeatherSunny24);
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
