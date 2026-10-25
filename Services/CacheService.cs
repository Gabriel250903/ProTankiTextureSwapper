using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class CacheService : ICacheService
    {
        public bool IsGameRunning()
        {
            return FileHelper.IsGameRunning();
        }

        public bool IsCacheFileLocked(string cachePath, IEnumerable<string>? targetFiles = null)
        {
            return FileHelper.IsCacheFileLocked(cachePath, targetFiles);
        }
    }
}
