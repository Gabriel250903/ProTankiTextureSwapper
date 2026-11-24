namespace TextureSwapper.Core.Models
{
    public sealed class ChatParticipantItem
    {
        public string Hwid { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public int Snowflakes { get; set; }
        public int Streak { get; set; }
        public string Badge { get; set; } = string.Empty;
        public string BadgeTier { get; set; } = string.Empty;
        public string LastCheckinDate { get; set; } = string.Empty;

        public string ShortHwid => Hwid.Length > 12 ? $"{Hwid[..12]}..." : Hwid;
        public string DisplayName =>
            string.IsNullOrWhiteSpace(Badge) ? Nickname : $"[{Badge}] {Nickname}";
    }

    public sealed class ChatAdminStatsResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<ChatParticipantItem> Participants { get; set; } = [];
        public int TotalTickets { get; set; }
        public int MessageCount { get; set; }
        public bool EventEnded { get; set; }
        public string WinnerNickname { get; set; } = string.Empty;
        public string WinnerKey { get; set; } = string.Empty;
    }
}
