namespace TextureSwapper.Core.Models
{
    public sealed class CacheBackupManifest
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<string, CacheBackupEntry> Entries { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class CacheBackupEntry
    {
        public string FileName { get; set; } = string.Empty;
        public string CacheIdentityHash { get; set; } = string.Empty;
        public string OriginalSha256 { get; set; } = string.Empty;
        public int OriginalSize { get; set; }
        public string BuildFingerprint { get; set; } = string.Empty;
        public List<string> PatchSet { get; set; } = [];
        public DateTime TimestampUtc { get; set; }
    }

    public sealed class BundlePatchDiagnostic
    {
        public string FileName { get; set; } = string.Empty;
        public string BundleKind { get; set; } = string.Empty;
        public string Feature { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string BuildFingerprint { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public List<string> MissingSignatures { get; set; } = [];
    }

    public sealed class PatchRunReport
    {
        public DateTime TimestampUtc { get; set; }
        public string CacheIdentityHash { get; set; } = string.Empty;
        public List<BundlePatchDiagnostic> Entries { get; set; } = [];
    }
}
