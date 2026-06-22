using TextureSwapper.Services;
using TextureSwapper.ViewModels;
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

            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(_mainViewModel.Settings.Theme);

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
            if (sender is Wpf.Ui.Controls.NavigationViewItem selectedItem && MainTabControl != null)
            {
                // Deactivate other items
                foreach (var item in RootNavigation.MenuItems)
                {
                    if (item is Wpf.Ui.Controls.NavigationViewItem navItem)
                    {
                        navItem.IsActive = (navItem == selectedItem);
                    }
                }
                foreach (var item in RootNavigation.FooterMenuItems)
                {
                    if (item is Wpf.Ui.Controls.NavigationViewItem navItem)
                    {
                        navItem.IsActive = (navItem == selectedItem);
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
                        foreach (var item in RootNavigation.MenuItems)
                        {
                            if (item is Wpf.Ui.Controls.NavigationViewItem navItem)
                            {
                                int tabIndex = navItem.Tag?.ToString() switch
                                {
                                    "Skins" => 0,
                                    "Paints" => 1,
                                    "Backups" => 2,
                                    _ => -1
                                };
                                navItem.IsActive = (tabIndex == MainTabControl.SelectedIndex);
                            }
                        }
                        break;
                }
            }
        }

        public async Task ShowAsync(string title, string message, ControlAppearance appearance)
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
            await Task.Delay(3000);
            ToastInfoBar.IsOpen = false;
        }
    }
}
