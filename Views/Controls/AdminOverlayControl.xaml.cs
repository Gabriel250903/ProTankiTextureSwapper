using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels;
using Wpf.Ui.Controls;

namespace TextureSwapper.Views.Controls
{
    public partial class AdminOverlayControl : UserControl
    {
        public AdminOverlayControl()
        {
            InitializeComponent();
        }

        private void AdminPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            {
                if (vm.AdminVM.LoginCommand.CanExecute(AdminPasswordBox))
                {
                    vm.AdminVM.LoginCommand.Execute(AdminPasswordBox);
                }
            }
        }

        private void OnCancelAdminLogin(object sender, RoutedEventArgs e)
        {
            ClearPassword();
            if (DataContext is MainViewModel vm)
            {
                vm.AdminVM.IsAuthenticated = false;
                vm.AdminVM.CancelCommand.Execute(null);
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        private void OnCloseAdminDashboard(object sender, RoutedEventArgs e)
        {
            ClearPassword();
            if (DataContext is MainViewModel vm)
            {
                vm.AdminVM.IsAuthenticated = false;
                vm.AdminVM.CancelCommand.Execute(null);
            }
            else
            {
                Visibility = Visibility.Collapsed;
            }
        }

        public void FocusPasswordBox()
        {
            _ = AdminPasswordBox.Focus();
        }

        public void ClearPassword()
        {
            AdminPasswordBox.Password = string.Empty;
        }

        private void OnActionsButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.ContextMenu != null)
            {
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.IsOpen = true;
            }
        }

        private readonly NotificationState _notificationState = new();

        public Task ShowToastAsync(string title, string message, ControlAppearance appearance)
        {
            return NotificationHelper.ShowToastAsync(
                AdminToastInfoBar,
                _notificationState,
                title,
                message,
                appearance
            );
        }
    }
}
