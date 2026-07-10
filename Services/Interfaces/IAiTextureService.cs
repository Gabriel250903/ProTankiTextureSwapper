namespace TextureSwapper.Services.Interfaces
{
    public interface IAiTextureService
    {
        Task<byte[]?> GenerateTextureAsync(string prompt, string token);
    }
}
