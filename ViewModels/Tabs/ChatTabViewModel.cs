using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Commands;
using TextureSwapper.ViewModels.Base;
using Wpf.Ui.Controls;

namespace TextureSwapper.ViewModels.Tabs
{
    public sealed class ChatTabViewModel : ViewModelBase
    {
        private readonly IChatService _chatService;
        private readonly IPremiumService _premiumService;
        private readonly INotificationService _notificationService;
        private readonly ISettingsService _settingsService;

        private string _inputText = string.Empty;
        private int _snowflakes;
        private int _streak;
        private string _badge = string.Empty;
        private bool _isConnected;
        private string _statusText = "Disconnected";
        private bool _hasPendingSkinVoucher;
        private string _selectedBadge = "None";
        private bool _isUpdatingBadge;
        private bool _isAuroraThemeEnabled = true;

        public ObservableCollection<ChatMessage> Messages { get; } = [];
        public ObservableCollection<string> UnlockedBadges { get; } = ["None"];

        public string InputText
        {
            get => _inputText;
            set => _ = SetProperty(ref _inputText, value);
        }

        public int Snowflakes
        {
            get => _snowflakes;
            private set => _ = SetProperty(ref _snowflakes, value);
        }

        public int Streak
        {
            get => _streak;
            private set => _ = SetProperty(ref _streak, value);
        }

        public string Badge
        {
            get => _badge;
            private set => _ = SetProperty(ref _badge, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set => _ = SetProperty(ref _isConnected, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => _ = SetProperty(ref _statusText, value);
        }

        public bool HasPendingSkinVoucher
        {
            get => _hasPendingSkinVoucher;
            private set => _ = SetProperty(ref _hasPendingSkinVoucher, value);
        }

        public string SelectedBadge
        {
            get => _selectedBadge;
            set
            {
                if (
                    SetProperty(ref _selectedBadge, value)
                    && !_isUpdatingBadge
                    && !string.IsNullOrWhiteSpace(value)
                )
                {
                    string cmd = string.Equals(value, "None", StringComparison.OrdinalIgnoreCase)
                        ? "/badge none"
                        : $"/badge {value}";
                    _ = _chatService.SendAsync(cmd);
                }
            }
        }

        public bool IsEventUnlocked => _chatService.HasAccess;

        public bool IsSpecialThemeAllowed => _chatService.IsSpecialThemeAllowed;

        public bool IsAuroraThemeEnabled
        {
            get => _isAuroraThemeEnabled;
            set
            {
                if (SetProperty(ref _isAuroraThemeEnabled, value))
                {
                    OnPropertyChanged(nameof(IsSpecialThemeActive));
                }
            }
        }

        public bool IsSpecialThemeActive => IsSpecialThemeAllowed && IsAuroraThemeEnabled;

        public ObservableCollection<string> GiftedSkinNames { get; } = [];

        public bool HasGiftedSkins => GiftedSkinNames.Count > 0;

        public event EventHandler<string>? NavigateToGiftedSkinRequested;

        public void RefreshGiftedSkins()
        {
            GiftedSkinNames.Clear();
            if (!_premiumService.IsPremiumUnlocked)
            {
                foreach (string skinId in _premiumService.UnlockedChristmasSkinIds)
                {
                    if (!string.IsNullOrWhiteSpace(skinId))
                    {
                        GiftedSkinNames.Add(skinId);
                    }
                }
            }
            OnPropertyChanged(nameof(HasGiftedSkins));
        }

        public void RefreshSpecialThemeAccess()
        {
            OnPropertyChanged(nameof(IsEventUnlocked));
            OnPropertyChanged(nameof(IsSpecialThemeAllowed));
            OnPropertyChanged(nameof(IsSpecialThemeActive));
            RefreshGiftedSkins();
        }

        public ICommand SendCommand { get; }
        public ICommand QuickCommand { get; }
        public ICommand ReconnectCommand { get; }
        public ICommand ToggleAuroraThemeCommand { get; }
        public ICommand NavigateToGiftedSkinCommand { get; }

        public ChatTabViewModel(
            IChatService chatService,
            IPremiumService premiumService,
            INotificationService notificationService,
            ISettingsService settingsService
        )
        {
            _chatService = chatService;
            _premiumService = premiumService;
            _notificationService = notificationService;
            _settingsService = settingsService;

            SendCommand = new AsyncRelayCommand(async _ => await SendCurrentMessageAsync());
            QuickCommand = new AsyncRelayCommand(async param =>
            {
                if (param is string cmd && !string.IsNullOrWhiteSpace(cmd))
                {
                    await EnsureConnectedAsync();
                    await _chatService.SendAsync(cmd);
                }
            });
            ReconnectCommand = new AsyncRelayCommand(async _ => await EnsureConnectedAsync());
            ToggleAuroraThemeCommand = new RelayCommand(_ =>
                IsAuroraThemeEnabled = !IsAuroraThemeEnabled
            );
            NavigateToGiftedSkinCommand = new RelayCommand(param =>
            {
                if (param is string skinName && !string.IsNullOrWhiteSpace(skinName))
                {
                    NavigateToGiftedSkinRequested?.Invoke(this, skinName);
                }
            });

            _chatService.HistoryReceived += OnHistoryReceived;
            _chatService.MessageReceived += OnMessageReceived;
            _chatService.StatsUpdated += OnStatsUpdated;
            _chatService.UnlockedBadgesUpdated += OnUnlockedBadgesUpdated;
            _chatService.ConnectionStateChanged += OnConnectionStateChanged;
            _chatService.GrandPrizeReceived += OnGrandPrizeReceived;
            _chatService.SkinVoucherWon += OnSkinVoucherWon;
        }

        public async Task EnsureConnectedAsync()
        {
            OnPropertyChanged(nameof(IsEventUnlocked));
            OnPropertyChanged(nameof(IsSpecialThemeAllowed));
            OnPropertyChanged(nameof(IsSpecialThemeActive));
            if (!_chatService.HasAccess || _chatService.IsConnected)
            {
                return;
            }

            try
            {
                StatusText = "Connecting to North Pole...";
                AppSettings settings = _settingsService.Load();
                string nickname = string.IsNullOrWhiteSpace(settings.UserNickname)
                    ? Environment.UserName
                    : settings.UserNickname;
                await _chatService.ConnectAsync(nickname);
            }
            catch (Exception ex)
            {
                StatusText = $"Connection failed: {ex.Message}";
            }
        }

        private async Task SendCurrentMessageAsync()
        {
            string text = InputText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            InputText = string.Empty;
            await EnsureConnectedAsync();
            await _chatService.SendAsync(text);
        }

        private void OnHistoryReceived(object? sender, IReadOnlyList<ChatMessage> history)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Messages.Clear();
                foreach (ChatMessage msg in history)
                {
                    Messages.Add(msg);
                }
            });
        }

        private void OnMessageReceived(object? sender, ChatMessage message)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                Messages.Add(message);
                if (Messages.Count > 200)
                {
                    Messages.RemoveAt(0);
                }

                if (message.IsPrivateReply)
                {
                    _ = AutoRemoveEphemeralMessageAsync(message);
                }
            });
        }

        private async Task AutoRemoveEphemeralMessageAsync(ChatMessage message)
        {
            int delaySeconds = message.Text.Contains('\n') ? 18 : 10;
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _ = Messages.Remove(message);
            });
        }

        private void OnStatsUpdated(
            object? sender,
            (int Snowflakes, int Streak, string Badge) stats
        )
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                if (stats.Snowflakes >= 0)
                {
                    Snowflakes = stats.Snowflakes;
                }
                if (stats.Streak >= 0)
                {
                    Streak = stats.Streak;
                }

                Badge = stats.Badge ?? string.Empty;
                _isUpdatingBadge = true;
                try
                {
                    SelectedBadge = string.IsNullOrWhiteSpace(Badge) ? "None" : Badge;
                }
                finally
                {
                    _isUpdatingBadge = false;
                }
            });
        }

        private void OnUnlockedBadgesUpdated(object? sender, IReadOnlyList<string> badges)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                _isUpdatingBadge = true;
                try
                {
                    string current = SelectedBadge;
                    UnlockedBadges.Clear();
                    UnlockedBadges.Add("None");
                    foreach (string b in badges)
                    {
                        if (!UnlockedBadges.Contains(b))
                        {
                            UnlockedBadges.Add(b);
                        }
                    }

                    SelectedBadge = UnlockedBadges.Contains(current) ? current : "None";
                }
                finally
                {
                    _isUpdatingBadge = false;
                }
            });
        }

        private void OnConnectionStateChanged(object? sender, bool connected)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                IsConnected = connected;
                StatusText = connected ? "Connected — North Pole Terminal" : "Disconnected";
            });
        }

        private void OnSkinVoucherWon(object? sender, EventArgs e)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                HasPendingSkinVoucher = true;
                await _notificationService.ShowAsync(
                    "🌟 Mythic Gift Unwrapped!",
                    "You won a 1-Skin Premium Voucher! Go to the Skins tab and swap any 1 Premium skin to claim it.",
                    ControlAppearance.Success
                );
            });
        }

        private void OnGrandPrizeReceived(object? sender, string lifetimeKey)
        {
            _ = Application.Current?.Dispatcher.InvokeAsync(async () =>
            {
                AppSettings settings = _settingsService.Load();
                string nickname = string.IsNullOrWhiteSpace(settings.UserNickname)
                    ? Environment.UserName
                    : settings.UserNickname;

                _ = await _premiumService.ActivatePremiumKeyAsync(lifetimeKey, nickname);
                await _notificationService.ShowAsync(
                    "🎄 Christmas 2026 Raffle Winner!",
                    $"CONGRATULATIONS! You won the 1-of-1 Lifetime Premium License Key!\n\nKey: {lifetimeKey}\n\nYour Lifetime Premium features have been automatically unlocked!",
                    ControlAppearance.Success
                );
            });
        }
    }
}
