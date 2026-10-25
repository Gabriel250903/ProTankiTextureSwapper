using System.Windows.Media;
using System.Windows.Threading;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Windows
{
    public partial class SettingsWindow : FluentWindow, INotificationService
    {
        private readonly NotificationState _notificationState = new();

        public SettingsWindow(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();

            ApplicationThemeManager.Apply(this);

            Loaded += async (_, _) =>
            {
                await viewModel.InitializeOnLoadedAsync();
            };

            ApplicationThemeManager.Changed += OnAppThemeChanged;
            Closed += (_, _) =>
            {
                ApplicationThemeManager.Changed -= OnAppThemeChanged;
                if (DataContext is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            };
        }

        private void OnAppThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    try
                    {
                        WindowBackdropType savedBackdrop = WindowBackdropType;
                        WindowBackdropType = WindowBackdropType.None;
                        WindowBackdropType = savedBackdrop;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(
                            ex,
                            "Error refreshing Settings window backdrop on theme change."
                        );
                    }
                },
                DispatcherPriority.Background
            );
        }

        public Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            return NotificationHelper.ShowToastAsync(
                ToastInfoBar,
                _notificationState,
                title,
                message,
                appearance
            );
        }
    }
}
