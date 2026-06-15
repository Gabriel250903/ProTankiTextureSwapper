using TextureSwapper.ViewModels;
using Wpf.Ui.Controls;

namespace TextureSwapper
{
    public partial class SettingsWindow : FluentWindow
    {
        public SettingsWindow(SettingsViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}
