using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;

namespace TextureSwapper.Views.Controls
{
    public partial class PremiumLockOverlayControl : UserControl
    {
        public static readonly DependencyProperty FeatureTitleProperty =
            DependencyProperty.Register(
                nameof(FeatureTitle),
                typeof(string),
                typeof(PremiumLockOverlayControl),
                new PropertyMetadata("Premium Feature Locked")
            );

        public static readonly DependencyProperty IsLockedProperty = DependencyProperty.Register(
            nameof(IsLocked),
            typeof(bool),
            typeof(PremiumLockOverlayControl),
            new PropertyMetadata(true)
        );

        public static readonly DependencyProperty BuyPremiumCommandProperty =
            DependencyProperty.Register(
                nameof(BuyPremiumCommand),
                typeof(ICommand),
                typeof(PremiumLockOverlayControl),
                new PropertyMetadata(null)
            );

        public static readonly DependencyProperty OpenSettingsCommandProperty =
            DependencyProperty.Register(
                nameof(OpenSettingsCommand),
                typeof(ICommand),
                typeof(PremiumLockOverlayControl),
                new PropertyMetadata(null)
            );

        public static readonly DependencyProperty CanStartTrialProperty =
            DependencyProperty.Register(
                nameof(CanStartTrial),
                typeof(bool),
                typeof(PremiumLockOverlayControl),
                new PropertyMetadata(false)
            );

        public static readonly DependencyProperty HasClaimedTrialProperty =
            DependencyProperty.Register(
                nameof(HasClaimedTrial),
                typeof(bool),
                typeof(PremiumLockOverlayControl),
                new PropertyMetadata(false)
            );

        public static readonly DependencyProperty IsTrialStartingProperty =
            DependencyProperty.Register(
                nameof(IsTrialStarting),
                typeof(bool),
                typeof(PremiumLockOverlayControl),
                new PropertyMetadata(false)
            );

        public string FeatureTitle
        {
            get => (string)GetValue(FeatureTitleProperty);
            set => SetValue(FeatureTitleProperty, value);
        }

        public bool IsLocked
        {
            get => (bool)GetValue(IsLockedProperty);
            set => SetValue(IsLockedProperty, value);
        }

        public bool CanStartTrial
        {
            get => (bool)GetValue(CanStartTrialProperty);
            set => SetValue(CanStartTrialProperty, value);
        }

        public bool HasClaimedTrial
        {
            get => (bool)GetValue(HasClaimedTrialProperty);
            set => SetValue(HasClaimedTrialProperty, value);
        }

        public bool IsTrialStarting
        {
            get => (bool)GetValue(IsTrialStartingProperty);
            set => SetValue(IsTrialStartingProperty, value);
        }

        public ICommand? BuyPremiumCommand
        {
            get => (ICommand?)GetValue(BuyPremiumCommandProperty);
            set => SetValue(BuyPremiumCommandProperty, value);
        }

        public ICommand? OpenSettingsCommand
        {
            get => (ICommand?)GetValue(OpenSettingsCommandProperty);
            set => SetValue(OpenSettingsCommandProperty, value);
        }

        public ICommand BuyCommand { get; }
        public ICommand EnterKeyCommand { get; }
        public ICommand StartTrialCommand { get; }

        private const string GumroadSubscriptionUrl = Constants.GumroadBuyUrl;

        public PremiumLockOverlayControl()
        {
            InitializeComponent();

            BuyCommand = new RelayCommand(_ => ExecuteBuy());
            EnterKeyCommand = new RelayCommand(_ => ExecuteEnterKey());
            StartTrialCommand = new AsyncRelayCommand(
                ExecuteStartTrialAsync,
                _ => !IsTrialStarting && CanStartTrial
            );

            Loaded += (_, _) => RefreshTrialState();

            try
            {
                IPremiumService? premiumService =
                    App.ServiceProvider?.GetService<IPremiumService>();
                if (premiumService != null)
                {
                    premiumService.PremiumStatusChanged += (_, _) =>
                    {
                        Dispatcher.Invoke(RefreshTrialState);
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "Failed to hook PremiumStatusChanged in PremiumLockOverlayControl."
                );
            }

            RefreshTrialState();
        }

        public void RefreshTrialState()
        {
            try
            {
                ISettingsService? settingsService =
                    App.ServiceProvider?.GetService<ISettingsService>();
                IPremiumService? premiumService =
                    App.ServiceProvider?.GetService<IPremiumService>();
                AppSettings? settings = settingsService?.Load();

                bool hasClaimed = settings?.HasClaimedTrial ?? false;
                bool isUnlocked = premiumService?.IsPremiumUnlocked ?? false;

                HasClaimedTrial = hasClaimed && !isUnlocked;
                CanStartTrial = !hasClaimed && !isUnlocked;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to refresh trial state in PremiumLockOverlayControl.");
            }
        }

        private async Task ExecuteStartTrialAsync(object? parameter)
        {
            if (IsTrialStarting || !CanStartTrial)
            {
                return;
            }

            try
            {
                IsTrialStarting = true;
                IPremiumService? premiumService =
                    App.ServiceProvider?.GetService<IPremiumService>();
                ISettingsService? settingsService =
                    App.ServiceProvider?.GetService<ISettingsService>();
                INotificationService? notificationService =
                    App.ServiceProvider?.GetService<INotificationService>();
                ILocalizationService? loc = App.ServiceProvider?.GetService<ILocalizationService>();

                if (premiumService == null)
                {
                    return;
                }

                AppSettings? settings = settingsService?.Load();
                string nickname = string.IsNullOrWhiteSpace(settings?.UserNickname)
                    ? Environment.UserName
                    : settings.UserNickname;

                PremiumActivationResult result = await premiumService.StartFreeTrialAsync(nickname);
                if (result.Success)
                {
                    IsLocked = false;
                    RefreshTrialState();
                    if (notificationService != null)
                    {
                        await notificationService.ShowAsync(
                            loc?.GetString("Loc_Msg_TrialActivatedTitle")
                                ?? "Free Trial Activated!",
                            loc?.GetString("Loc_Msg_TrialActivated")
                                ?? "Enjoy 3 days of unrestricted access to all premium features!",
                            Wpf.Ui.Controls.ControlAppearance.Success
                        );
                    }
                }
                else
                {
                    RefreshTrialState();
                    if (notificationService != null)
                    {
                        await notificationService.ShowAsync(
                            "Trial Activation Failed",
                            result.ErrorMessage ?? "Could not activate free trial.",
                            Wpf.Ui.Controls.ControlAppearance.Danger
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error activating trial from PremiumLockOverlayControl.");
            }
            finally
            {
                IsTrialStarting = false;
            }
        }

        private void OnStartTrialClicked(object sender, RoutedEventArgs e)
        {
            if (StartTrialCommand.CanExecute(null))
            {
                StartTrialCommand.Execute(null);
            }
        }

        private void OnBuyClicked(object sender, RoutedEventArgs e)
        {
            ExecuteBuy();
        }

        private void OnEnterKeyClicked(object sender, RoutedEventArgs e)
        {
            ExecuteEnterKey();
        }

        private void ExecuteBuy()
        {
            if (BuyPremiumCommand != null && BuyPremiumCommand.CanExecute(null))
            {
                BuyPremiumCommand.Execute(null);
                return;
            }

            _ = TextureSwapper.Helpers.Utilities.PremiumPurchaseHelper.PromptPurchaseOptionsAsync(Window.GetWindow(this));
        }

        private void ExecuteEnterKey()
        {
            if (OpenSettingsCommand != null && OpenSettingsCommand.CanExecute(null))
            {
                OpenSettingsCommand.Execute(null);
                return;
            }

            try
            {
                IWindowService? windowService = App.ServiceProvider?.GetService<IWindowService>();
                windowService?.ShowSettingsDialog();
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Failed to open Settings dialog from Premium lock overlay. Please open the Settings window manually."
                );
            }
        }
    }
}
