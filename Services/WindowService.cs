using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.ViewModels;
using TextureSwapper.ViewModels.Editors;
using TextureSwapper.Views.Windows;

namespace TextureSwapper.Services
{
    public sealed class WindowService : IWindowService
    {
        public void ShowSettingsDialog()
        {
            SettingsViewModel settingsVM =
                App.ServiceProvider.GetRequiredService<SettingsViewModel>();
            SettingsWindow settingsWindow = new(settingsVM)
            {
                Owner = Application.Current.MainWindow,
            };
            _ = settingsWindow.ShowDialog();
        }

        public void ShowParachuteEditorDialog(Window? owner = null)
        {
            ParachuteEditorViewModel vm =
                App.ServiceProvider.GetRequiredService<ParachuteEditorViewModel>();
            ParachuteEditorWindow window = new(vm)
            {
                Owner = owner ?? Application.Current.MainWindow,
            };
            _ = window.ShowDialog();
        }

        public void ShowPropsEditorDialog(Window? owner = null)
        {
            PropsEditorViewModel vm =
                App.ServiceProvider.GetRequiredService<PropsEditorViewModel>();
            PropsEditorWindow window = new(vm) { Owner = owner ?? Application.Current.MainWindow };
            _ = window.ShowDialog();
        }
    }
}
