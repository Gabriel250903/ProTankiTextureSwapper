using Serilog;
using System.Diagnostics;
using System.IO;
using TextureSwapper.Core;

namespace TextureSwapper.Helpers
{
    public static class FileHelper
    {
        public static string GetSafePath(string basePath, string relativePath)
        {
            string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath.Replace("\\", "/")));
            string rootPath = Path.GetFullPath(basePath);

            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("Path traversal attempt blocked! Base: {Base}, Attempted: {Attempted}", rootPath, fullPath);
                throw new UnauthorizedAccessException("Path traversal attack detected!");
            }

            return fullPath;
        }

        public static bool IsGameRunning()
        {
            if (Process.GetProcessesByName(Constants.GameProcessName).Length != 0)
            {
                Log.Warning("Detected running process: {ProcessName}", Constants.GameProcessName);
                return true;
            }

            return false;
        }

        public static bool IsCacheFileLocked(string cachePath)
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
                        Log.Warning("Cache file is locked by another process: {FilePath}", file);
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
