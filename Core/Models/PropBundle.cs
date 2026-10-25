namespace TextureSwapper.Core.Models
{
    public sealed class PropBundle(List<TaraEntry> entries)
    {
        public List<TaraEntry> Entries { get; } = entries;

        public TaraEntry? FindTextureEntry(string? preferredName = null)
        {
            if (!string.IsNullOrEmpty(preferredName))
            {
                TaraEntry? match = Entries.FirstOrDefault(e =>
                    e.FileName.Equals(preferredName, StringComparison.OrdinalIgnoreCase)
                );
                if (match != null)
                {
                    return match;
                }
            }

            return Entries.FirstOrDefault(e =>
                e.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || e.FileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            );
        }

        public TaraEntry? FindMeshEntry(string? preferredName = null)
        {
            if (!string.IsNullOrEmpty(preferredName))
            {
                TaraEntry? match = Entries.FirstOrDefault(e =>
                    e.FileName.Equals(preferredName, StringComparison.OrdinalIgnoreCase)
                );
                if (match != null)
                {
                    return match;
                }
            }

            return Entries.FirstOrDefault(e =>
                e.FileName.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase)
            );
        }
    }
}
