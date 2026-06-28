namespace TextureSwapper.Services.Interfaces
{
    public interface ICacheService
    {
        bool IsGameRunning();
        bool IsCacheFileLocked(string cachePath);
    }
}
