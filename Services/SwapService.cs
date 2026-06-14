using Serilog;
using System.IO;
using TextureSwapper.Core;
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
            string originalsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir, Constants.OriginalsDir);
            _ = Directory.CreateDirectory(originalsDir);

            string[] targets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget];
            foreach (string target in targets)
            {
                if (string.IsNullOrEmpty(target))
                {
                    continue;
                }

                string sourceFile = Path.Combine(cachePath, target);
                string backupFile = Path.Combine(originalsDir, target);

                if (File.Exists(sourceFile) && !File.Exists(backupFile))
                {
                    Log.Information("First time seeing {Target}. Saving original version to {BackupDir}", target, originalsDir);
                    File.Copy(sourceFile, backupFile);
                }
            }
        }

        public void SelectiveBackup(string cachePath, SkinModel skin)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir, $"{skin.Name}_{timestamp}");

            try
            {
                Log.Information("Creating selective backup for {SkinName} at {BackupDir}", skin.Name, backupDir);
                _ = Directory.CreateDirectory(backupDir);

                string[] targets = [skin.DetailsTarget, skin.LightmapTarget, skin.AlphaTarget];
                foreach (string target in targets)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    string sourceFile = Path.Combine(cachePath, target);
                    if (File.Exists(sourceFile))
                    {
                        File.Copy(sourceFile, Path.Combine(backupDir, target), true);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create selective backup for {SkinName}.", skin.Name);
            }
        }

        public void Swap(string cachePath, SkinModel skin)
        {
            Log.Information("Applying skin: {SkinName}", skin.Name);

            if (!Directory.Exists(cachePath))
            {
                Log.Error("Cache directory not found: {CachePath}", cachePath);
                throw new DirectoryNotFoundException("Cache directory not found.");
            }

            EnsureOriginalsBackup(cachePath, skin);
            SelectiveBackup(cachePath, skin);

            string texturesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, skin.SourceFolder);

            try
            {
                CopyAndRename(texturesDir, "details.png", cachePath, skin.DetailsTarget);
                CopyAndRename(texturesDir, "lightmap.png", cachePath, skin.LightmapTarget);
                CopyAndRename(texturesDir, "alpha.png", cachePath, skin.AlphaTarget);
                Log.Information("Skin {SkinName} applied successfully.", skin.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occurred during skin swap for {SkinName}.", skin.Name);
                throw;
            }
        }

        public void SwapBatch(string cachePath, IEnumerable<SkinModel> skins)
        {
            Log.Information("Starting batch swap for {Count} skins.", skins.Count());

            if (!Directory.Exists(cachePath))
            {
                Log.Error("Cache directory not found: {CachePath}", cachePath);
                throw new DirectoryNotFoundException("Cache directory not found.");
            }

            foreach (SkinModel skin in skins)
            {
                try
                {
                    Swap(cachePath, skin);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to apply skin {SkinName} during batch process.", skin.Name);
                }
            }
            Log.Information("Batch swap completed.");
        }

        public bool RestoreFullCache(string cachePath)
        {
            Log.Information("Restoring original textures from Originals backup...");
            string originalsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.BackupsDir, Constants.OriginalsDir);

            if (!Directory.Exists(originalsDir) || Directory.GetFiles(originalsDir).Length == 0)
            {
                Log.Warning("Restore skipped: Originals backup directory {BackupDir} is empty or does not exist.", originalsDir);
                return false;
            }

            try
            {
                int restoreCount = 0;
                foreach (string file in Directory.GetFiles(originalsDir))
                {
                    string destFile = Path.Combine(cachePath, Path.GetFileName(file));
                    File.Copy(file, destFile, true);
                    restoreCount++;
                }
                Log.Information("Successfully restored {Count} original files.", restoreCount);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore original textures.");
                throw;
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
            string fullSourcePath = Path.Combine(sourceDir, sourceFile);
            string fullTargetPath = Path.Combine(targetDir, targetName);

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
