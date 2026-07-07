using Serilog;
using System.Windows;
using TextureSwapper.Services.Interfaces;
using Wpf.Ui.Controls;

namespace TextureSwapper.Services
{
    public class NotificationService : INotificationService
    {
        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            try
            {
                await await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (Application.Current.MainWindow is MainWindow mainWindow)
                    {
                        await mainWindow.ShowAsync(title, message, appearance);
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

