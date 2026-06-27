using Serilog;
using System.IO;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;

namespace TextureSwapper.Services
{
    public class SwapService
    {
        public string DetectCachePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fullPath = Path.Combine(appData, Constants.StandaloneLoaderDir, Constants.LocalStoreDir, Constants.CacheDirName);

            Log.Debug("Detecting cache path. Suggested: {Path}", fullPath);
            return Directory.Exists(fullPath) ? fullPath : string.Empty;
        }

        private void EnsureOriginalsBackup(string cachePath, SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string originalsDir = FileHelper.GetSafePath(baseDir, Path.Combine(Constants.BackupsDir, Constants.OriginalsDir));
            _ = Directory.CreateDirectory(originalsDir);

            string[] targets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget, skin.ModelTarget];
            foreach (string target in targets)
            {
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                string sourceFile = FileHelper.GetSafePath(cachePath, target);
                string backupFile = FileHelper.GetSafePath(originalsDir, target);

                if (File.Exists(sourceFile) && !File.Exists(backupFile))
                {
                    Log.Information("First time seeing {Target}. Saving original version to {BackupDir}", target, originalsDir);
                    File.Copy(sourceFile, backupFile);
                }
            }
        }

        public void SelectiveBackup(string cachePath, SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = FileHelper.GetSafePath(baseDir, Path.Combine(Constants.BackupsDir, $"{skin.Name}_{timestamp}"));

            try
            {
                Log.Information("Creating selective backup for {SkinName} at {BackupDir}", skin.Name, backupDir);
                _ = Directory.CreateDirectory(backupDir);

                string[] targets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget, skin.ModelTarget];
                foreach (string target in targets)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    string sourceFile = FileHelper.GetSafePath(cachePath, target);
                    if (File.Exists(sourceFile))
                    {
                        File.Copy(sourceFile, FileHelper.GetSafePath(backupDir, target), true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create selective backup for {SkinName}.", skin.Name);
            }
        }

        public string? Swap(string cachePath, SkinModel skin, string? inGamePaintName = null)
        {
            Log.Information("Applying skin: {SkinName}", skin.Name);

            if (!Directory.Exists(cachePath))
            {
                Log.Error("Cache directory not found: {CachePath}", cachePath);
                return "Cache directory not found.";
            }

            List<string> missingTargets = [];
            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(skin.DetailsTarget))
                {
                    string path = FileHelper.GetSafePath(cachePath, skin.DetailsTarget);
                    if (!File.Exists(path))
                    {
                        missingTargets.Add(skin.DetailsTarget);
                    }
                }
            }
            else
            {
                string[] requiredTargets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget];
                foreach (string target in requiredTargets)
                {
                    if (!string.IsNullOrEmpty(target))
                    {
                        string path = FileHelper.GetSafePath(cachePath, target);
                        if (!File.Exists(path))
                        {
                            missingTargets.Add(target);
                        }
                    }
                }
                if (!string.IsNullOrEmpty(skin.ModelTarget))
                {
                    string path = FileHelper.GetSafePath(cachePath, skin.ModelTarget);
                    if (!File.Exists(path))
                    {
                        missingTargets.Add(skin.ModelTarget);
                    }
                }
            }

            if (missingTargets.Count != 0)
            {
                Log.Error("Missing target files in cache for {SkinName}", skin.Name);
                if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(inGamePaintName))
                {
                    return $"Paint:{inGamePaintName}";
                }
                if (skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase))
                {
                    return "NotCachedSupplies";
                }
                return "NotCached";
            }

            try
            {
                EnsureOriginalsBackup(cachePath, skin);
                SelectiveBackup(cachePath, skin);

                string texturesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, skin.SourceFolder);

                if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                {
                    string paintFileName = $"{skin.Name}.png";
                    CopyAndRename(texturesDir, paintFileName, cachePath, skin.DetailsTarget);
                }
                else
                {
                    CopyAndRename(texturesDir, "details.png", cachePath, skin.DetailsTarget);
                    CopyAndRename(texturesDir, "lightmap.png", cachePath, skin.LightmapTarget);
                    CopyAndRename(texturesDir, "alpha.png", cachePath, skin.AlphaTarget);
                    if (!string.IsNullOrEmpty(skin.ModelTarget))
                    {
                        CopyAndRename(texturesDir, "object.3ds", cachePath, skin.ModelTarget);
                    }
                }
                Log.Information("Skin {SkinName} applied successfully.", skin.Name);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during skin swap for {SkinName}.", skin.Name);
                return $"Error: {ex.Message}";
            }
        }

        public string? SwapBatch(string cachePath, IEnumerable<SkinModel> skins, string? inGamePaintName = null)
        {
            Log.Information("Starting batch swap for {Count} skins.", skins.Count());

            if (!Directory.Exists(cachePath))
            {
                Log.Error("Cache directory not found: {CachePath}", cachePath);
                return "Cache directory not found.";
            }

            List<string> notCachedSkins = [];
            List<string> notCachedPaints = [];
            List<string> notCachedSupplies = [];
            List<string> otherFailures = [];

            foreach (SkinModel skin in skins)
            {
                string? result = Swap(cachePath, skin, inGamePaintName);
                if (result != null)
                {
                    if (result == "NotCached")
                    {
                        notCachedSkins.Add(skin.Name);
                    }
                    else if (result == "NotCachedSupplies")
                    {
                        notCachedSupplies.Add(skin.ItemName);
                    }
                    else if (result.StartsWith("Paint:"))
                    {
                        string paintName = result[6..];
                        if (!notCachedPaints.Contains(paintName))
                        {
                            notCachedPaints.Add(paintName);
                        }
                    }
                    else
                    {
                        otherFailures.Add($"{skin.Name} ({result})");
                    }
                }
            }

            List<string> errorMessages = [];
            List<string> itemsToEquip = [.. notCachedSkins, .. notCachedPaints.Select(p => $"Paint '{p}'")];

            if (itemsToEquip.Count != 0)
            {
                string list = string.Join("\n", itemsToEquip.Select(item => $"- {item}"));
                errorMessages.Add($"Please open ProTanki and equip the following item(s) first to cache them:\n{list}");
            }

            if (notCachedSupplies.Count != 0)
            {
                string list = string.Join("\n", notCachedSupplies.Select(item => $"- {item}"));
                errorMessages.Add($"Please open ProTanki and load into a game first to cache the following supply item(s):\n{list}");
            }

            if (otherFailures.Count != 0)
            {
                errorMessages.Add("Unexpected failures:\n" + string.Join("\n", otherFailures.Select(f => $"- {f}")));
            }

            if (errorMessages.Count != 0)
            {
                return string.Join("\n\n", errorMessages);
            }

            Log.Information("Batch swap completed.");
            return null;
        }

        public bool RestoreFullCache(string cachePath)
        {
            Log.Information("Restoring original textures from Originals backup...");
            string originalsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir, Constants.OriginalsDir);

            return RestoreFromBackup(cachePath, originalsDir);
        }

        public bool RestoreFromBackup(string cachePath, string backupDir)
        {
            if (!Directory.Exists(backupDir) || Directory.GetFiles(backupDir).Length == 0)
            {
                Log.Warning("Restore skipped: Backup directory {BackupDir} is empty or does not exist.", backupDir);
                return false;
            }

            try
            {
                int restoreCount = 0;
                foreach (string file in Directory.GetFiles(backupDir))
                {
                    string targetName = Path.GetFileName(file);
                    string destFile = FileHelper.GetSafePath(cachePath, targetName);
                    File.Copy(file, destFile, true);
                    restoreCount++;
                }
                Log.Information("Successfully restored {Count} files from {BackupDir}.", restoreCount, backupDir);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore textures from {BackupDir}.", backupDir);
                throw;
            }
        }

        public void PurgeOldBackups(int maxDays)
        {
            string backupsRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir);
            if (!Directory.Exists(backupsRoot))
            {
                return;
            }

            try
            {
                DateTime threshold = DateTime.Now.AddDays(-maxDays);
                int purgeCount = 0;

                foreach (string dir in Directory.GetDirectories(backupsRoot))
                {
                    if (Path.GetFileName(dir).Equals(Constants.OriginalsDir, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    DirectoryInfo di = new(dir);
                    if (di.CreationTime < threshold)
                    {
                        Log.Information("Purging old backup: {DirName} (Created: {Date})", di.Name, di.CreationTime);
                        di.Delete(true);
                        purgeCount++;
                    }
                }

                if (purgeCount > 0)
                {
                    Log.Information("Purged {Count} old backups.", purgeCount);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to purge old backups.");
            }
        }

        public void ClearCache(string cachePath)
        {
            Log.Information("Clearing ProTanki cache at {CachePath}...", cachePath);

            if (!Directory.Exists(cachePath))
            {
                Log.Warning("ClearCache called but directory does not exist: {CachePath}", cachePath);
                return;
            }

            try
            {
                int fileCount = 0;
                foreach (string file in Directory.GetFiles(cachePath))
                {
                    File.Delete(file);
                    fileCount++;
                }
                Log.Information("Successfully deleted {Count} files from cache.", fileCount);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear cache.");
                throw;
            }
        }

        private void CopyAndRename(string sourceDir, string sourceFile, string targetDir, string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
            {
                return;
            }

            string fullSourcePath = FileHelper.GetSafePath(sourceDir, sourceFile);
            string fullTargetPath = FileHelper.GetSafePath(targetDir, targetName);

            Log.Debug("Copying {Source} to {Target}", fullSourcePath, fullTargetPath);

            if (File.Exists(fullSourcePath))
            {
                File.Copy(fullSourcePath, fullTargetPath, true);
            }
            else
            {
                string altSourcePath = Path.ChangeExtension(fullSourcePath, ".jpg");
                if (File.Exists(altSourcePath))
                {
                    Log.Debug("Source PNG not found, using JPG: {AltSource}", altSourcePath);
                    File.Copy(altSourcePath, fullTargetPath, true);
                }
                else
                {
                    Log.Warning("Source file not found: {SourcePath}", fullSourcePath);
                }
            }
        }
    }
}
