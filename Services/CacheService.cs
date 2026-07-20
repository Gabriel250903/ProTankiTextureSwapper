using TextureSwapper.Helpers;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public sealed class CacheService : ICacheService
    {
        public bool IsGameRunning()
        {
            return FileHelper.IsGameRunning();
        }

        public bool IsCacheFileLocked(string cachePath)
        {
            return FileHelper.IsCacheFileLocked(cachePath);
        }
    }
}
