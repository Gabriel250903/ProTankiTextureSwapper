using Serilog;
using System.Diagnostics;
using System.IO;
using TextureSwapper.Core;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public class CacheService : ICacheService
    {
        public bool IsGameRunning()
        {
            if (Process.GetProcessesByName(Constants.GameProcessName).Length != 0)
            {
                Log.Warning($"Detected running process: {Constants.GameProcessName}");
                return true;
            }
            return false;
        }

        public bool IsCacheFileLocked(string cachePath)
        {
            if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath))
            {
                return false;
            }

            try
            {
                string[] files = Directory.GetFiles(cachePath);
                foreach (string file in files)
                {
                    try
                    {
                        using FileStream stream = File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (IOException)
                    {
                        Log.Warning($"Cache file is locked by another process: {file}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while checking for cache locks.");
            }

            return false;
        }
    }
}
