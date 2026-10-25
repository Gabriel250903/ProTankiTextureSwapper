using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;
using TextureSwapper.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using MessageBoxResult = Wpf.Ui.Controls.MessageBoxResult;
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

            if (_mainViewModel.IsHalloweenThemeActive)
            {
                _mainViewModel.HalloweenThemeService.CheckAndApply(notify: false, force: true);
            }
            else
            {
                ApplicationThemeManager.Apply(_mainViewModel.Settings.Theme);
            }
            ApplicationThemeManager.Changed += OnAppThemeChanged;

            Loaded += async (_, _) =>
            {
                if (_mainViewModel.IsHalloweenThemeActive)
                {
                    _mainViewModel.HalloweenSoundService.PlayLaunchGreeting();
                    _mainViewModel.HalloweenSoundService.SetAmbientDrone(true);
                }

                await CheckAndShowDiscordInviteAsync();
            };

            PreviewMouseLeftButtonDown += OnGlobalPreviewMouseLeftButtonDown;

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
            if (!_mainViewModel.IsHalloweenThemeActive)
            {
                return;
            }

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
                        _mainViewModel.HalloweenSoundService.PlayHollowClick();
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            catch
            {
                Log.Warning("Error processing global mouse click for Halloween sound effect.");
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
                                    _ => -1,
                                };
                                navItem.IsActive = tabIndex == MainTabControl.SelectedIndex;
                            }
                        }
                        break;
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
            if (e.ClickCount == 1 && _mainViewModel.IsHalloweenThemeActive)
            {
                _mainViewModel.HalloweenSoundService.PlayEvilLaugh(force: true);
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

        private async Task CheckAndShowDiscordInviteAsync()
        {
            try
            {
                if (_mainViewModel.Settings.HasSeenDiscordInvite)
                {
                    return;
                }

                string title = _mainViewModel.LocalizationService.GetString("Loc_DiscordInvite_Title")
                    ?? "Join the Discord Server";
                string header = _mainViewModel.LocalizationService.GetString("Loc_DiscordInvite_Header")
                    ?? "Welcome to Texture Swapper V2!";
                string body = _mainViewModel.LocalizationService.GetString("Loc_DiscordInvite_Body")
                    ?? "Join the official Discord server to follow the latest news, announcements, texture updates, and sneak peeks.\n\nConnect with other swappers, get support, and share your suggestions!";
                string joinText = _mainViewModel.LocalizationService.GetString("Loc_DiscordInvite_Join")
                    ?? "Join Discord Server";
                string laterText = _mainViewModel.LocalizationService.GetString("Loc_DiscordInvite_Later")
                    ?? "Maybe Later";

                StackPanel panel = new()
                {
                    Margin = new Thickness(0, 10, 0, 10),
                    MaxWidth = 500
                };

                TextBlock headerBlock = new()
                {
                    Text = header,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 12),
                    TextWrapping = TextWrapping.Wrap
                };

                TextBlock bodyBlock = new()
                {
                    Text = body,
                    FontSize = 13.5,
                    Opacity = 0.85,
                    LineHeight = 20,
                    TextWrapping = TextWrapping.Wrap
                };

                panel.Children.Add(headerBlock);
                panel.Children.Add(bodyBlock);

                MessageBox messageBox = new()
                {
                    Title = title,
                    Content = panel,
                    PrimaryButtonText = joinText,
                    SecondaryButtonText = laterText,
                    MaxWidth = 540,
                };

                MessageBoxResult result = await messageBox.ShowDialogAsync();
                if (result == MessageBoxResult.Primary)
                {
                    try
                    {
                        _ = Process.Start(new ProcessStartInfo
                        {
                            FileName = Constants.DiscordInviteUrl,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to launch Discord invite URL.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to display Discord invite notice.");
            }
            finally
            {
                _mainViewModel.Settings.HasSeenDiscordInvite = true;
                _mainViewModel.SaveSettings();
            }
        }
    }
}
