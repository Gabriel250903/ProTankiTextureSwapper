using TextureSwapper.Core.Models;

namespace TextureSwapper.Core.Interfaces
{
    public interface ICacheService
    {
        bool IsGameRunning();
        bool IsCacheFileLocked(string cachePath, IEnumerable<string>? targetFiles = null);
        bool IsClientDetected(ClientType clientType);
    }
}
