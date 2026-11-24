namespace TextureSwapper.Core.Models
{
    public sealed class ChatMessage
    {
        public long Id { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public string Badge { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsPrivateReply { get; set; }
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;

        public string DisplayAuthor =>
            string.IsNullOrWhiteSpace(Badge) ? Nickname : $"[{Badge}] {Nickname}";

        public string TimeText => Timestamp.ToLocalTime().ToString("HH:mm");
    }
}
