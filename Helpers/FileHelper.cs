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

            if (!rootPath.EndsWith(Path.DirectorySeparatorChar))
            {
                rootPath += Path.DirectorySeparatorChar;
            }

            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) &&
                !fullPath.Equals(rootPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                Log.Error("Path traversal attempt blocked! Base: {Base}, Attempted: {Attempted}", rootPath, fullPath);
                throw new UnauthorizedAccessException("Path traversal attack detected!");
            }

            return fullPath;
        }

        public static bool IsGameRunning()
        {
            string[] processNames = [Constants.GameProcessName, "ProTanki Standalone"];
            foreach (string name in processNames)
            {
                Process[] processes = Process.GetProcessesByName(name);
                try
                {
                    if (processes.Length != 0)
                    {
                        Log.Warning("Detected running process: {ProcessName}", name);
                        return true;
                    }
                }
                finally
                {
                    foreach (Process p in processes)
                    {
                        p.Dispose();
                    }
                }
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
                string[] files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
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

        public static bool HasFileWithExtension(string directory, string filenameWithoutExtension)
        {
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(filenameWithoutExtension) || !Directory.Exists(directory))
            {
                return false;
            }

            try
            {
                string[] files = Directory.GetFiles(directory, filenameWithoutExtension + ".*");
                return files.Any(f => !Path.GetFileName(f).Equals(filenameWithoutExtension, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, $"Failed to check for files with extension for target: {filenameWithoutExtension}");
                return false;
            }
        }
    }
}
