using System.Windows;

namespace TextureSwapper.Core.Interfaces
{
    public interface IWindowService
    {
        void ShowSettingsDialog();
        void ShowParachuteEditorDialog(Window? owner = null);
        void ShowPropsEditorDialog(Window? owner = null);
    }
}
