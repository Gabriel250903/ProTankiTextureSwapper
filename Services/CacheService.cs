using System.IO;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public class CacheService : ICacheService
    {
        public bool IsGameRunning()
        {
            return FileHelper.IsGameRunning();
        }

        public bool IsCacheFileLocked(string cachePath, IEnumerable<string>? targetFiles = null)
        {
            return FileHelper.IsCacheFileLocked(cachePath, targetFiles);
        }

        public bool IsClientDetected(ClientType clientType)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (clientType == ClientType.Html5)
            {
                string roamingDir = Path.Combine(appData, Constants.Html5AppDataDir);
                string localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );
                string installDir = Path.Combine(localAppData, Constants.Html5LocalInstallSubPath);
                return Directory.Exists(roamingDir) || Directory.Exists(installDir);
            }
            else
            {
                string flashCache = Path.Combine(
                    appData,
                    Constants.StandaloneLoaderDir,
                    Constants.LocalStoreDir,
                    Constants.CacheDirName
                );
                return Directory.Exists(flashCache);
            }
        }
    }
}
