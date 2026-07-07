using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TextureSwapper.ViewModels;

namespace TextureSwapper.Views
{
    public partial class AdminOverlayControl : UserControl
    {
        public AdminOverlayControl()
        {
            InitializeComponent();
        }

        private void AdminPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.AdminVM.PasswordInput = AdminPasswordBox.Password;
            }
        }

        private void AdminPasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainViewModel vm)
            {
                if (vm.AdminVM.LoginCommand.CanExecute(null))
                {
                    vm.AdminVM.LoginCommand.Execute(null);
                }
            }
        }

        private void OnCancelAdminLogin(object sender, RoutedEventArgs e)
        {
            AdminPasswordBox.Password = string.Empty;
            if (DataContext is MainViewModel vm)
            {
                vm.AdminVM.IsAuthenticated = false;
            }
            Visibility = Visibility.Collapsed;
        }

        private void OnCloseAdminDashboard(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.AdminVM.IsAuthenticated = false;
            }
            Visibility = Visibility.Collapsed;
        }

        public void FocusPasswordBox()
        {
            _ = AdminPasswordBox.Focus();
        }

        public void ClearPassword()
        {
            AdminPasswordBox.Password = string.Empty;
        }
    }
}
