using Wpf.Ui.Controls;

namespace TextureSwapper.Core.Interfaces
{
    public interface INotificationService
    {
        Task ShowAsync(string title, string message, ControlAppearance appearance);
    }
}
