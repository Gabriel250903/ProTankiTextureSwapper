using System.Windows.Media;
using System.Windows.Threading;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper
{
    public partial class SettingsWindow : FluentWindow
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();

            ApplicationThemeManager.Apply(this);

            ApplicationThemeManager.Changed += OnAppThemeChanged;
            Closed += (_, _) => ApplicationThemeManager.Changed -= OnAppThemeChanged;
        }

        private void OnAppThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                ApplicationThemeManager.Apply(this);

                WindowBackdropType savedBackdrop = WindowBackdropType;
                WindowBackdropType = WindowBackdropType.None;
                WindowBackdropType = savedBackdrop;
            }, DispatcherPriority.Background);
        }
    }
}
