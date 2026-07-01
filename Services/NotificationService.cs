using System.Windows;
using TextureSwapper.Services.Interfaces;
using Wpf.Ui.Controls;

namespace TextureSwapper.Services
{
    public class NotificationService : INotificationService
    {
        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            _ = await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (Application.Current.MainWindow is INotificationService notificationService)
                {
                    await notificationService.ShowAsync(title, message, appearance);
                }
            });
        }
    }
}
