using System.Windows;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Views.Windows;
using Wpf.Ui.Controls;

namespace TextureSwapper.Services
{
    public sealed class NotificationService : INotificationService
    {
        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            try
            {
                await await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    INotificationService? targetService = null;

                    Window? activeWindow = Application
                        .Current.Windows.OfType<Window>()
                        .FirstOrDefault(w => w.IsActive && w is INotificationService);

                    if (activeWindow is INotificationService activeService)
                    {
                        targetService = activeService;
                    }
                    else
                    {
                        Window? topmostChildWindow = Application
                            .Current.Windows.OfType<Window>()
                            .LastOrDefault(w =>
                                w.IsVisible
                                && w != Application.Current.MainWindow
                                && w is INotificationService
                            );

                        if (topmostChildWindow is INotificationService childService)
                        {
                            targetService = childService;
                        }
                        else if (Application.Current.MainWindow is MainWindow mainWindow)
                        {
                            targetService = mainWindow;
                        }
                    }

                    if (targetService != null)
                    {
                        await targetService.ShowAsync(title, message, appearance);
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to show notification: {title} - {message}");
            }
        }
    }
}
