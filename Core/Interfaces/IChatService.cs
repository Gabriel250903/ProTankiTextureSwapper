using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface IChatService : IDisposable
    {
        bool IsConnected { get; }
        bool HasAccess { get; }
        bool IsSpecialThemeAllowed { get; }

        event EventHandler<ChatMessage>? MessageReceived;
        event EventHandler<IReadOnlyList<ChatMessage>>? HistoryReceived;
        event EventHandler<(int Snowflakes, int Streak, string Badge)>? StatsUpdated;
        event EventHandler<IReadOnlyList<string>>? UnlockedBadgesUpdated;
        event EventHandler<string>? GrandPrizeReceived;
        event EventHandler? SkinVoucherWon;
        event EventHandler<bool>? ConnectionStateChanged;

        Task<bool> CheckAccessAsync();
        Task ConnectAsync(string nickname);
        Task DisconnectAsync();
        Task SendAsync(string text);
        Task<(bool Success, string? UnlockedSkinId, string? Error)> ClaimSkinVoucherAsync(
            string skinId,
            string skinName
        );
        Task<ChatAdminStatsResult> GetAdminStatsAsync(string adminSecret);
        Task<(
            bool Success,
            string? WinnerNickname,
            int WinnerSnowflakes,
            string? Key,
            string? Error
        )> DrawRaffleWinnerAsync(string adminSecret);
        Task<(bool Success, string? Message)> ResetChatEventAsync(string adminSecret);
        Task<(bool Success, int NewSnowflakes, string? Error)> AdjustSnowflakesAsync(
            string adminSecret,
            string hwid,
            int delta
        );
        Task<(bool Success, string? Message)> GrantAdminRewardAsync(
            string adminSecret,
            string hwid,
            string rewardType,
            string value,
            string? nickname = null
        );
    }
}
