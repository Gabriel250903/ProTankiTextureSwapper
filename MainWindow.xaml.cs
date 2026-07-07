using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using TextureSwapper.Services.Interfaces;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace TextureSwapper
{
    public partial class MainWindow : FluentWindow, INotificationService
    {
        private MainViewModel _mainViewModel;
        private int _currentNotificationId = 0;

        public MainWindow(MainViewModel mainViewModel)
        {
            _mainViewModel = mainViewModel;
            DataContext = _mainViewModel;

            InitializeComponent();

            ApplicationThemeManager.Apply(_mainViewModel.Settings.Theme);
            ApplicationThemeManager.Changed += OnAppThemeChanged;
            Closed += (_, _) => ApplicationThemeManager.Changed -= OnAppThemeChanged;
        }

        private void OnAppThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
        {
            _ = Dispatcher.InvokeAsync(() =>
            {
                ApplicationThemeManager.Apply(this);
                ApplicationThemeManager.Apply(RootNavigation);

                WindowBackdropType savedBackdrop = WindowBackdropType;
                WindowBackdropType = WindowBackdropType.None;
                WindowBackdropType = savedBackdrop;
            }, DispatcherPriority.Background);
        }

        private void OnNavigationItemClick(object sender, RoutedEventArgs e)
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
                    case "ShotEffects":
                        MainTabControl.SelectedIndex = 3;
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
                                    "ShotEffects" => 3,
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
            int notificationId = 0;
            Application.Current.Dispatcher.Invoke(() =>
            {
                _currentNotificationId++;
                notificationId = _currentNotificationId;

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
                if (_currentNotificationId == notificationId)
                {
                    ToastInfoBar.IsOpen = false;
                }
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
            _mainViewModel.AdminVM.CloseRequested -= OnAdminCloseRequested;
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
