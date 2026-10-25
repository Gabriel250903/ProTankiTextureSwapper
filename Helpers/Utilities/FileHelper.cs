using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Serilog;
using TextureSwapper.Core;

namespace TextureSwapper.Helpers.Utilities
{
    public static class FileHelper
    {
        public static string GetSafePath(string basePath, string relativePath)
        {
            string cleanRelative = relativePath.Replace("\\", "/").TrimStart('/');
            string fullPath = Path.GetFullPath(Path.Combine(basePath, cleanRelative));
            string rootPath = Path.GetFullPath(basePath);

            if (!rootPath.EndsWith(Path.DirectorySeparatorChar))
            {
                rootPath += Path.DirectorySeparatorChar;
            }

            if (
                !fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase)
                && !fullPath.Equals(
                    rootPath.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                Log.Error(
                    $"Path traversal attempt blocked! Base: {rootPath}, Attempted: {fullPath}"
                );
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
                        Log.Warning($"Detected running process: {name}");
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

        public static bool IsFileLocked(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            try
            {
                using FileStream stream = File.Open(
                    filePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                return false;
            }
            catch (IOException)
            {
                Log.Warning($"Target cache file is locked by another process: {filePath}");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsCacheFileLocked(
            string cachePath,
            IEnumerable<string>? targetFiles = null
        )
        {
            if (string.IsNullOrEmpty(cachePath) || !Directory.Exists(cachePath))
            {
                return false;
            }

            try
            {
                if (targetFiles != null && targetFiles.Any())
                {
                    foreach (string target in targetFiles)
                    {
                        if (string.IsNullOrWhiteSpace(target))
                        {
                            continue;
                        }

                        string targetPath = GetSafePath(cachePath, target);
                        if (IsFileLocked(targetPath))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                if (IsGameRunning())
                {
                    return true;
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
            if (
                string.IsNullOrEmpty(directory)
                || string.IsNullOrEmpty(filenameWithoutExtension)
                || !Directory.Exists(directory)
            )
            {
                return false;
            }

            try
            {
                string[] files = Directory.GetFiles(directory, filenameWithoutExtension + ".*");
                return files.Any(f =>
                    !Path.GetFileName(f)
                        .Equals(filenameWithoutExtension, StringComparison.OrdinalIgnoreCase)
                );
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    $"Failed to check for files with extension for target: {filenameWithoutExtension}"
                );
                return false;
            }
        }

        public static string GetHardwareId()
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(
                    RegistryHive.LocalMachine,
                    RegistryView.Registry64
                );
                using RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");

                if (key != null)
                {
                    object? val = key.GetValue("MachineGuid");
                    if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                    {
                        return val.ToString()!;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Failed to retrieve MachineGuid from Registry. Falling back to MachineName."
                );
            }
            return Environment.MachineName;
        }

        public static async Task CopyWithRetryAsync(
            string source,
            string destination,
            int maxRetries = 3,
            int delayMs = 150,
            CancellationToken ct = default
        )
        {
            for (int i = 0; i < maxRetries; i++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await using FileStream inStream = new(
                        source,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        65536,
                        true
                    );
                    await using FileStream outStream = new(
                        destination,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        65536,
                        true
                    );
                    await inStream.CopyToAsync(outStream, ct);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs * (i + 1), ct);
                }
            }
        }
    }
}
