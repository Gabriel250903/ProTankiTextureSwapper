using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels.Editors;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Windows
{
    public partial class PropsEditorWindow : FluentWindow, INotificationService
    {
        private readonly NotificationState _notificationState = new();

        public PropsEditorWindow(PropsEditorViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
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
