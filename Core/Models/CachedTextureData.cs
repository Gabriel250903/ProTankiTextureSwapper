namespace TextureSwapper.Core.Models
{
    public class CachedTextureData
    {
        public int Width { get; init; }
        public int Height { get; init; }
        public byte[] Pixels { get; init; } = [];
    }
}
