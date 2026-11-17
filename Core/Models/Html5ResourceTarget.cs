namespace TextureSwapper.Core.Models
{
    public sealed class Html5ResourceTarget
    {
        public string Base64Target { get; init; } = string.Empty;
        public string OriginalUrl { get; init; } = string.Empty;
        public long ResourceId { get; init; }
        public long Version { get; init; }
        public string IndexedDbKey => $"{ResourceId}:default";
        public string EntryFileName { get; init; } = string.Empty;
    }
}
