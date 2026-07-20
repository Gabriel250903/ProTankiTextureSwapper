namespace TextureSwapper.Models
{
    public class CustomTextureInfo
    {
        public byte[] Pixels { get; set; } = [];
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
    }
}
