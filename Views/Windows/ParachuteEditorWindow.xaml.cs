using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels.Editors;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Windows
{
    public partial class ParachuteEditorWindow : FluentWindow, INotificationService
    {
        private readonly NotificationState _notificationState = new();

        public ParachuteEditorWindow(ParachuteEditorViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
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
