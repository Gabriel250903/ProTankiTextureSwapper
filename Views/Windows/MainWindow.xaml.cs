using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using TextBlock = Wpf.Ui.Controls.TextBlock;

namespace TextureSwapper.Views.Windows
{
    public partial class MainWindow : FluentWindow, INotificationService
    {
        private readonly MainViewModel _mainViewModel;
        private readonly IWindowService _windowService;
        private readonly NotificationState _notificationState = new();

        public MainWindow(MainViewModel mainViewModel, IWindowService windowService)
        {
            _mainViewModel = mainViewModel;
            _windowService = windowService;
            DataContext = _mainViewModel;

            InitializeComponent();

            if (_mainViewModel.IsSeasonalThemeActive)
            {
                _mainViewModel.SeasonalThemeService.CheckAndApply(notify: false, force: true);
            }
            else
            {
                ApplicationThemeManager.Apply(_mainViewModel.Settings.Theme);
            }
            ApplicationThemeManager.Changed += OnAppThemeChanged;

            Loaded += (_, _) =>
            {
                if (_mainViewModel.IsSeasonalThemeActive)
                {
                    _mainViewModel.SeasonalAudioService.PlayLaunchGreeting();
                    _mainViewModel.SeasonalAudioService.SetAmbientDrone(true);
                }
            };

            PreviewMouseLeftButtonDown += OnGlobalPreviewMouseLeftButtonDown;

            _mainViewModel.ChatTabVM.NavigateToGiftedSkinRequested += (_, skinName) =>
            {
                Dispatcher.Invoke(() => NavigateToGiftedSkin(skinName));
            };

            Closed += (_, _) =>
            {
                try
                {
                    _mainViewModel.DeviceBanService.StopPeriodicVerification();
                }
                catch { }
                PreviewMouseLeftButtonDown -= OnGlobalPreviewMouseLeftButtonDown;
                ApplicationThemeManager.Changed -= OnAppThemeChanged;
                _mainViewModel.AdminVM.CloseRequested -= OnAdminCloseRequested;
                _mainViewModel.AdminVM.AdminNotificationRequested -= AdminOverlay.ShowToastAsync;
            };
        }

        private void OnGlobalPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DependencyObject? current = e.OriginalSource as DependencyObject;
                while (current != null && current != this)
                {
                    if (
                        current is ButtonBase
                        || current is NavigationViewItem
                        || current is TabItem
                        || current is ComboBox
                        || current is Slider
                        || current is ListBoxItem
                        || current is ToggleSwitch
                        || (current is FrameworkElement fe && fe.Cursor == Cursors.Hand)
                    )
                    {
                        _mainViewModel.SeasonalAudioService.PlayClick();
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            catch
            {
                Log.Warning("Error processing global mouse click for seasonal sound effect.");
            }
        }

        private void OnAppThemeChanged(ApplicationTheme currentTheme, Color systemAccent)
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    try
                    {
                        WindowBackdropType savedBackdrop = WindowBackdropType;
                        WindowBackdropType = WindowBackdropType.None;
                        WindowBackdropType = savedBackdrop;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(
                            ex,
                            "Error refreshing window backdrop on theme change."
                        );
                    }
                },
                DispatcherPriority.Background
            );
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
                        if (
                            _mainViewModel.SkinsTabVM.SelectedCategory == "Paints"
                            && _mainViewModel.SkinsTabVM.SkinsCategories.Any()
                        )
                        {
                            _mainViewModel.SkinsTabVM.SelectedCategory =
                                _mainViewModel.SkinsTabVM.SkinsCategories.First();
                        }
                        break;
                    case "Paints":
                        MainTabControl.SelectedIndex = 1;
                        _mainViewModel.SkinsTabVM.SelectedCategory = "Paints";
                        break;
                    case "Backups":
                        MainTabControl.SelectedIndex = 2;
                        break;
                    case "ShotEffects":
                        MainTabControl.SelectedIndex = 3;
                        break;
                    case "ChristmasChat":
                        MainTabControl.SelectedIndex = 4;
                        if (_mainViewModel.IsChristmasChatVisible)
                        {
                            _ = _mainViewModel.ChatTabVM.EnsureConnectedAsync();
                        }
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
                                    "ChristmasChat" => 4,
                                    _ => -1,
                                };
                                navItem.IsActive = tabIndex == MainTabControl.SelectedIndex;
                            }
                        }
                        break;
                }
            }
        }

        private void NavigateToGiftedSkin(string skinName)
        {
            SkinModel? match = _mainViewModel.AllSkins.FirstOrDefault(s =>
                s.Name.Equals(skinName, StringComparison.OrdinalIgnoreCase)
            );
            bool isPaint =
                match != null
                && match.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase);

            if (isPaint)
            {
                MainTabControl.SelectedIndex = 1;
                _mainViewModel.SkinsTabVM.SelectedCategory = "Paints";
                _mainViewModel.PaintsTabVM.CustomPaintSearchQuery = skinName;
            }
            else
            {
                MainTabControl.SelectedIndex = 0;
                _mainViewModel.SkinsTabVM.ShowOnlyGiftedSkins = true;
                _mainViewModel.SkinsTabVM.SearchQuery = skinName;
            }

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
                        "ChristmasChat" => 4,
                        _ => -1,
                    };
                    navItem.IsActive = tabIndex == MainTabControl.SelectedIndex;
                }
            }
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

        private void OnAdminNavItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is NavigationViewItem navItem)
            {
                navItem.IsActive = false;
            }
            _ = OpenAdminPanelAsync();
        }

        private async Task OpenAdminPanelAsync()
        {
            try
            {
                AdminOverlay.Visibility = Visibility.Visible;
                _mainViewModel.AdminVM.CloseRequested -= OnAdminCloseRequested;
                _mainViewModel.AdminVM.CloseRequested += OnAdminCloseRequested;
                _mainViewModel.AdminVM.AdminNotificationRequested -= AdminOverlay.ShowToastAsync;
                _mainViewModel.AdminVM.AdminNotificationRequested += AdminOverlay.ShowToastAsync;

                if (_mainViewModel.AdminVM.IsAuthenticated)
                {
                    return;
                }

                bool autoLoggedIn = await _mainViewModel.AdminVM.TryAutoLoginAsync();
                if (!autoLoggedIn)
                {
                    AdminOverlay.ClearPassword();
                    AdminOverlay.FocusPasswordBox();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open admin panel.");
            }
        }

        private void OnLogoMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (e.ClickCount == 1 && _mainViewModel.IsSeasonalThemeActive)
            {
                _mainViewModel.SeasonalAudioService.PlaySpecialCue(force: true);
                _ = HandleThemeInteractionAsync();
            }
        }

        private async Task HandleThemeInteractionAsync()
        {
            try
            {
                SeasonalEventResult eventResult =
                    await _mainViewModel.PremiumService.ProcessSeasonalEventAsync(
                        _mainViewModel.Settings.UserNickname
                    );

                if (
                    !string.IsNullOrWhiteSpace(eventResult.DialogTitle)
                    && !string.IsNullOrWhiteSpace(eventResult.DialogBody)
                )
                {
                    MessageBox messageBox = new()
                    {
                        Title = eventResult.DialogTitle,
                        Content = new TextBlock
                        {
                            Text = eventResult.DialogBody,
                            FontSize = 13,
                            TextWrapping = TextWrapping.Wrap,
                            LineHeight = 20,
                        },
                        CloseButtonText = eventResult.CloseButtonText ?? "OK",
                        MaxWidth = 520,
                    };
                    _ = await messageBox.ShowDialogAsync();
                }
                else if (!string.IsNullOrWhiteSpace(eventResult.NotificationMessage))
                {
                    ControlAppearance appearance = ControlAppearance.Caution;
                    if (
                        string.Equals(
                            eventResult.Appearance,
                            "Info",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        appearance = ControlAppearance.Info;
                    }
                    else if (
                        string.Equals(
                            eventResult.Appearance,
                            "Success",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        appearance = ControlAppearance.Success;
                    }

                    _ = ShowAsync(
                        eventResult.NotificationTitle ?? "Halloween",
                        eventResult.NotificationMessage,
                        appearance
                    );
                }
                else
                {
                    _ = ShowAsync(
                        "Halloween",
                        "Mwahahaha! Beware of what lurks in the mist...",
                        ControlAppearance.Caution
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to handle theme interaction.");
                _ = ShowAsync(
                    "Halloween",
                    "Mwahahaha! Beware of what lurks in the mist...",
                    ControlAppearance.Caution
                );
            }
        }

        private void OnParachuteEditorNavItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is NavigationViewItem navItem)
            {
                navItem.IsActive = false;
            }
            _windowService.ShowParachuteEditorDialog(this);
        }

        private void OnPropsEditorNavItemClick(object sender, RoutedEventArgs e)
        {
            if (sender is NavigationViewItem navItem)
            {
                navItem.IsActive = false;
            }
            _windowService.ShowPropsEditorDialog(this);
        }

        private void OnAdminCloseRequested()
        {
            _mainViewModel.AdminVM.CloseRequested -= OnAdminCloseRequested;
            _mainViewModel.AdminVM.AdminNotificationRequested -= AdminOverlay.ShowToastAsync;
            AdminOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
