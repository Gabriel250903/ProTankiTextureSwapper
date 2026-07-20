using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using TextureSwapper.Services.Interfaces;
using TextureSwapper.ViewModels;

namespace TextureSwapper.Services
{
    public sealed class WindowService : IWindowService
    {
        public void ShowSettingsDialog()
        {
            SettingsViewModel settingsVM = App.ServiceProvider.GetRequiredService<SettingsViewModel>();
            SettingsWindow settingsWindow = new(settingsVM)
            {
                Owner = Application.Current.MainWindow
            };
            _ = settingsWindow.ShowDialog();
        }
    }
}
