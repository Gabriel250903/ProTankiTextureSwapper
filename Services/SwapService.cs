using Serilog;
using System.IO;
using System.IO.Compression;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public class SwapService : ISwapService
    {
        public string DetectCachePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fullPath = Path.Combine(appData, Constants.StandaloneLoaderDir, Constants.LocalStoreDir, Constants.CacheDirName);

            Log.Debug($"Detecting cache path. Suggested: {fullPath}");
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
                    Log.Information($"First time seeing {target}. Saving original version to {originalsDir}");
                    string? destDir = Path.GetDirectoryName(backupFile);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        _ = Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, backupFile);
                }
            }
        }

        public void SelectiveBackup(string cachePath, SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupZipPath = FileHelper.GetSafePath(baseDir, Path.Combine(Constants.BackupsDir, $"{skin.Name}_{timestamp}.zip"));

            try
            {
                Log.Information($"Creating selective compressed backup for {skin.Name} at {backupZipPath}");

                string? dir = Path.GetDirectoryName(backupZipPath);
                if (dir != null)
                {
                    _ = Directory.CreateDirectory(dir);
                }

                string[] targets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget, skin.ModelTarget];

                using ZipArchive archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create);
                foreach (string target in targets)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    string sourceFile = FileHelper.GetSafePath(cachePath, target);
                    if (File.Exists(sourceFile))
                    {
                        _ = archive.CreateEntryFromFile(sourceFile, target);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to create selective backup for {skin.Name}. Aborting swap.");
                throw;
            }
        }

        public string? Swap(string cachePath, SkinModel skin, string? inGamePaintName = null)
        {
            Log.Information($"Applying skin: {skin.Name}");

            if (!Directory.Exists(cachePath))
            {
                Log.Error($"Cache directory not found: {cachePath}");
                return "Cache directory not found.";
            }

            bool hasExtensionFiles = false;
            List<string> missingTargets = [];
            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(skin.DetailsTarget))
                {
                    string path = FileHelper.GetSafePath(cachePath, skin.DetailsTarget);
                    if (!File.Exists(path))
                    {
                        missingTargets.Add(skin.DetailsTarget);
                        if (FileHelper.HasFileWithExtension(cachePath, skin.DetailsTarget))
                        {
                            hasExtensionFiles = true;
                        }
                    }
                }
            }
            else
            {
                string[] requiredTargets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget, skin.ModelTarget];
                foreach (string target in requiredTargets)
                {
                    if (!string.IsNullOrEmpty(target))
                    {
                        string path = FileHelper.GetSafePath(cachePath, target);
                        if (!File.Exists(path))
                        {
                            missingTargets.Add(target);
                            if (FileHelper.HasFileWithExtension(cachePath, target))
                            {
                                hasExtensionFiles = true;
                            }
                        }
                    }
                }
            }

            if (missingTargets.Count != 0)
            {
                if (hasExtensionFiles)
                {
                    return "CacheWithExtensions";
                }
                Log.Error($"Missing target files in cache for {skin.Name}");
                return skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(inGamePaintName)
                    ? $"Paint:{inGamePaintName}"
                    : skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase) ? "NotCachedSupplies" : "NotCached";
            }

            try
            {
                EnsureOriginalsBackup(cachePath, skin);
                SelectiveBackup(cachePath, skin);

                string texturesDir = FileHelper.GetSafePath(AppDomain.CurrentDomain.BaseDirectory, skin.SourceFolder);

                if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                {
                    string paintFileName = $"{skin.Name}.png";
                    CopyAndRename(texturesDir, paintFileName, cachePath, skin.DetailsTarget);
                }
                else
                {
                    CopyAndRename(texturesDir, "details.jpg", cachePath, skin.DetailsTarget);
                    CopyAndRename(texturesDir, "lightmap.jpg", cachePath, skin.LightmapTarget);
                    CopyAndRename(texturesDir, "alpha.jpg", cachePath, skin.AlphaTarget);
                    if (!string.IsNullOrEmpty(skin.ModelTarget))
                    {
                        CopyAndRename(texturesDir, "object.3ds", cachePath, skin.ModelTarget);
                    }
                }
                Log.Information($"Skin {skin.Name} applied successfully.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred during skin swap for {skin.Name}.");
                return $"Error: {ex.Message}";
            }
        }

        public string? SwapBatch(string cachePath, IEnumerable<SkinModel> skins, string? inGamePaintName = null)
        {
            List<SkinModel> skinsList = [.. skins];
            string skinsCounterString = skinsList.Count > 1 ? "skins" : "skin";
            Log.Information($"Starting batch swap for {skinsList.Count} {skinsCounterString}.");

            if (!Directory.Exists(cachePath))
            {
                Log.Error($"Cache directory not found: {cachePath}");
                return "Cache directory not found.";
            }

            List<string> notCachedSkins = [];
            List<string> notCachedPaints = [];
            List<string> notCachedSupplies = [];
            List<string> otherFailures = [];

            bool anyCacheWithExtensions = false;
            foreach (SkinModel skin in skinsList)
            {
                string? result = Swap(cachePath, skin, inGamePaintName);
                if (result != null)
                {
                    if (result == "CacheWithExtensions")
                    {
                        anyCacheWithExtensions = true;
                    }
                    else if (result == "NotCached")
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

            if (anyCacheWithExtensions)
            {
                errorMessages.Add("The cache files seem to have extensions added to them (e.g. .png, .jpg). Please rename them back to plain files without extensions (e.g. by running 'ren *.png *' inside the cache folder) so the app can recognize and swap them.");
            }
            else
            {
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

        public bool RestoreFromBackup(string cachePath, string backupPath)
        {
            if (Directory.Exists(backupPath))
            {
                if (Directory.GetFiles(backupPath).Length == 0)
                {
                    Log.Warning($"Restore skipped: Backup directory {backupPath} is empty.");
                    return false;
                }

                try
                {
                    int restoreCount = 0;
                    foreach (string file in Directory.GetFiles(backupPath))
                    {
                        string targetName = Path.GetFileName(file);
                        string destFile = FileHelper.GetSafePath(cachePath, targetName);
                        File.Copy(file, destFile, true);
                        restoreCount++;
                    }

                    Log.Information($"Successfully restored {restoreCount} {(restoreCount == 1 ? "file" : "files")} from directory {backupPath}.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to restore textures from directory {backupPath}.");
                    throw;
                }
            }
            else if (File.Exists(backupPath) && backupPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(backupPath);
                    int restoreCount = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string destFile = FileHelper.GetSafePath(cachePath, entry.FullName);
                        entry.ExtractToFile(destFile, true);
                        restoreCount++;
                    }
                    Log.Information($"Successfully restored {restoreCount} {(restoreCount == 1 ? "file" : "files")} from zip backup {backupPath}.");
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to restore textures from zip backup {backupPath}.");
                    throw;
                }
            }

            Log.Warning($"Restore skipped: Backup path {backupPath} does not exist.");
            return false;
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
                        Log.Information($"Purging old backup directory: {di.Name} (Created: {di.CreationTime})");
                        di.Delete(true);
                        purgeCount++;
                    }
                }

                foreach (string file in Directory.GetFiles(backupsRoot, "*.zip"))
                {
                    FileInfo fi = new(file);
                    if (fi.CreationTime < threshold)
                    {
                        Log.Information($"Purging old zip backup: {fi.Name} (Created: {fi.CreationTime})");
                        fi.Delete();
                        purgeCount++;
                    }
                }

                if (purgeCount > 0)
                {
                    Log.Information($"Purged {purgeCount} old {(purgeCount == 1 ? "backup" : "backups")}.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to purge old backups.");
            }
        }

        public void ClearCache(string cachePath)
        {
            Log.Information($"Clearing ProTanki cache at {cachePath}...");

            if (!Directory.Exists(cachePath))
            {
                Log.Warning($"ClearCache called but directory does not exist: {cachePath}");
                return;
            }

            try
            {
                int fileCount = 0;
                string[] files = Directory.GetFiles(cachePath, "*", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    File.Delete(file);
                    fileCount++;
                }
                Log.Information($"Successfully deleted {fileCount} {(fileCount == 1 ? "file" : "files")} from cache.");
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

            Log.Debug($"Copying {fullSourcePath} to {fullTargetPath}");

            if (File.Exists(fullSourcePath))
            {
                File.Copy(fullSourcePath, fullTargetPath, true);
            }
            else
            {
                string currentExt = Path.GetExtension(fullSourcePath).ToLower();
                string altExt = currentExt == ".png" ? ".jpg" : ".png";
                string altSourcePath = Path.ChangeExtension(fullSourcePath, altExt);

                if (File.Exists(altSourcePath))
                {
                    Log.Debug($"Source {currentExt.ToUpper()} not found, using {altExt.ToUpper()}: {altSourcePath}");
                    File.Copy(altSourcePath, fullTargetPath, true);
                }
                else
                {
                    Log.Warning($"Source file not found: {fullSourcePath}");
                }
            }
        }

        private void EnsureOriginalsBackup(string cachePath, ShotEffectModel shotEffect)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string originalsDir = FileHelper.GetSafePath(baseDir, Path.Combine(Constants.BackupsDir, Constants.OriginalsDir));
            _ = Directory.CreateDirectory(originalsDir);

            foreach (string target in shotEffect.Targets)
            {
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                string sourceFile = FileHelper.GetSafePath(cachePath, target);
                string backupFile = FileHelper.GetSafePath(originalsDir, target);

                if (File.Exists(sourceFile) && !File.Exists(backupFile))
                {
                    Log.Information($"First time seeing {target}. Saving original version to {originalsDir}");
                    string? destDir = Path.GetDirectoryName(backupFile);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        _ = Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, backupFile);
                }
            }
        }

        public void SelectiveBackup(string cachePath, ShotEffectModel shotEffect)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupZipPath = FileHelper.GetSafePath(baseDir, Path.Combine(Constants.BackupsDir, $"{shotEffect.Turret}_{shotEffect.Name}_{timestamp}.zip"));

            try
            {
                Log.Information($"Creating selective compressed backup for shot effect {shotEffect.Turret} {shotEffect.Name} at {backupZipPath}");

                string? dir = Path.GetDirectoryName(backupZipPath);
                if (dir != null)
                {
                    _ = Directory.CreateDirectory(dir);
                }

                using ZipArchive archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create);
                foreach (string target in shotEffect.Targets)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    string sourceFile = FileHelper.GetSafePath(cachePath, target);
                    if (File.Exists(sourceFile))
                    {
                        _ = archive.CreateEntryFromFile(sourceFile, target);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to create selective backup for shot effect {shotEffect.Name}. Aborting swap.");
                throw;
            }
        }

        public string? SwapShotEffect(string cachePath, ShotEffectModel shotEffect)
        {
            Log.Information($"Applying shot effect: {shotEffect.Turret} {shotEffect.Name}");

            if (!Directory.Exists(cachePath))
            {
                Log.Error($"Cache directory not found: {cachePath}");
                return "Cache directory not found.";
            }

            bool hasExtensionFiles = false;
            List<string> missingTargets = [];
            foreach (string target in shotEffect.Targets)
            {
                if (!string.IsNullOrEmpty(target))
                {
                    string path = FileHelper.GetSafePath(cachePath, target);
                    if (!File.Exists(path))
                    {
                        missingTargets.Add(target);
                        if (FileHelper.HasFileWithExtension(cachePath, target))
                        {
                            hasExtensionFiles = true;
                        }
                    }
                }
            }

            if (missingTargets.Count != 0)
            {
                if (hasExtensionFiles)
                {
                    return "CacheWithExtensions";
                }
                Log.Error($"Missing target files in cache for shot effect {shotEffect.Turret} {shotEffect.Name}");
                return "NotCached";
            }

            try
            {
                EnsureOriginalsBackup(cachePath, shotEffect);
                SelectiveBackup(cachePath, shotEffect);

                string texturesDir = FileHelper.GetSafePath(AppDomain.CurrentDomain.BaseDirectory, shotEffect.SourceFolder);

                foreach (string target in shotEffect.Targets)
                {
                    string sourceFilePath = FileHelper.GetSafePath(texturesDir, target);
                    if (File.Exists(sourceFilePath))
                    {
                        string destFilePath = FileHelper.GetSafePath(cachePath, target);
                        File.Copy(sourceFilePath, destFilePath, true);
                    }
                    else
                    {
                        Log.Warning($"Source asset file not found locally: {sourceFilePath}");
                    }
                }

                Log.Information($"Shot effect {shotEffect.Turret} {shotEffect.Name} applied successfully.");
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error occurred during shot effect swap for {shotEffect.Name}.");
                return $"Error: {ex.Message}";
            }
        }
    }
}
