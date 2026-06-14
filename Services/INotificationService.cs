using Wpf.Ui.Controls;

namespace TextureSwapper.Services
{
    public interface INotificationService
    {
        Task ShowAsync(string title, string message, ControlAppearance appearance);
    }
}
