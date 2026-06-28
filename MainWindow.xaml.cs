using System.Windows;
using TextureSwapper.Services;
using TextureSwapper.Services.Interfaces;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper
{
    public partial class MainWindow : FluentWindow, INotificationService
    {
        private readonly MainViewModel _mainViewModel;
        private readonly UpdateService _updateService;

        public MainWindow()
        {
            _updateService = new UpdateService();
            _mainViewModel = new MainViewModel(this, _updateService);
            _mainViewModel.RequestSettings += OnRequestSettings;

            DataContext = _mainViewModel;

            ApplicationThemeManager.Apply(_mainViewModel.Settings.Theme);

            InitializeComponent();
        }

        private void OnRequestSettings()
        {
            SettingsViewModel settingsViewModel = new(_mainViewModel.Settings, _mainViewModel._settingsService, _mainViewModel);
            SettingsWindow settingsWindow = new(settingsViewModel)
            {
                Owner = this
            };
            _ = settingsWindow.ShowDialog();
        }

        private void OnNavigationItemClick(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is NavigationViewItem selectedItem && MainTabControl != null)
            {
                foreach (object? item in RootNavigation.MenuItems)
                {
                    if (item is NavigationViewItem navItem)
                    {
                        navItem.IsActive = navItem == selectedItem;
                    }
                }
                foreach (object? item in RootNavigation.FooterMenuItems)
                {
                    if (item is NavigationViewItem navItem)
                    {
                        navItem.IsActive = navItem == selectedItem;
                    }
                }

                string tag = selectedItem.Tag?.ToString() ?? string.Empty;
                switch (tag)
                {
                    case "Skins":
                        MainTabControl.SelectedIndex = 0;
                        if (_mainViewModel.SelectedCategory == "Paints" && _mainViewModel.SkinsCategories.Any())
                        {
                            _mainViewModel.SelectedCategory = _mainViewModel.SkinsCategories.First();
                        }
                        break;
                    case "Paints":
                        MainTabControl.SelectedIndex = 1;
                        _mainViewModel.SelectedCategory = "Paints";
                        break;
                    case "Backups":
                        MainTabControl.SelectedIndex = 2;
                        break;
                    case "Settings":
                        _mainViewModel.OpenSettingsCommand.Execute(null);
                        selectedItem.IsActive = false;
                        foreach (object? item in RootNavigation.MenuItems)
                        {
                            if (item is NavigationViewItem navItem)
                            {
                                int tabIndex = navItem.Tag?.ToString() switch
                                {
                                    "Skins" => 0,
                                    "Paints" => 1,
                                    "Backups" => 2,
                                    _ => -1
                                };
                                navItem.IsActive = tabIndex == MainTabControl.SelectedIndex;
                            }
                        }
                        break;
                }
            }
        }

        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ToastInfoBar.Title = title;
                ToastInfoBar.Message = message;
                ToastInfoBar.Severity = appearance switch
                {
                    ControlAppearance.Primary => InfoBarSeverity.Informational,
                    ControlAppearance.Success => InfoBarSeverity.Success,
                    ControlAppearance.Danger => InfoBarSeverity.Error,
                    ControlAppearance.Caution => InfoBarSeverity.Warning,
                    ControlAppearance.Info => InfoBarSeverity.Informational,
                    _ => InfoBarSeverity.Informational
                };

                ToastInfoBar.IsOpen = true;
            });

            await Task.Delay(3000);

            Application.Current.Dispatcher.Invoke(() =>
            {
                ToastInfoBar.IsOpen = false;
            });
        }

        private void OnAdminNavItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is NavigationViewItem navItem)
            {
                navItem.IsActive = false;
            }
            OpenAdminPanel();
        }

        private void OpenAdminPanel()
        {
            AdminOverlay.Visibility = Visibility.Visible;
            _mainViewModel.AdminVM.IsAuthenticated = false;
            _mainViewModel.AdminVM.CloseRequested += OnAdminCloseRequested;
            AdminOverlay.ClearPassword();
            AdminOverlay.FocusPasswordBox();
        }

        private void OnLogoMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                OpenAdminPanel();
            }
        }

        private void OnAdminCloseRequested()
        {
            _mainViewModel.AdminVM.CloseRequested -= OnAdminCloseRequested;
            AdminOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
