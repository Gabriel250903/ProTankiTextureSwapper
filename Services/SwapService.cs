using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class SwapService : ISwapService
    {
        private readonly IPremiumService? _premiumService;

        public SwapService(IPremiumService? premiumService = null)
        {
            _premiumService = premiumService;
        }

        public string DetectCachePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string fullPath = Path.Combine(
                appData,
                Constants.StandaloneLoaderDir,
                Constants.LocalStoreDir,
                Constants.CacheDirName
            );

            Log.Debug($"Detecting cache path. Suggested: {fullPath}");
            return Directory.Exists(fullPath) ? fullPath : string.Empty;
        }

        private static (string? Path, bool IsEncrypted) ResolveAssetSource(string src)
        {
            if (src.EndsWith(".enc", StringComparison.OrdinalIgnoreCase) && File.Exists(src))
            {
                return (src, true);
            }

            if (File.Exists(src))
            {
                return (src, false);
            }

            if (File.Exists(src + ".enc"))
            {
                return (src + ".enc", true);
            }

            string ext = Path.GetExtension(src);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                string alt = Path.ChangeExtension(src, ".jpg");
                if (File.Exists(alt)) return (alt, false);
                if (File.Exists(alt + ".enc")) return (alt + ".enc", true);
            }
            else if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                string alt = Path.ChangeExtension(src, ".png");
                if (File.Exists(alt)) return (alt, false);
                if (File.Exists(alt + ".enc")) return (alt + ".enc", true);
            }

            return (null, false);
        }

        private static async Task DecryptAssetAsync(
            string encryptedFilePath,
            string destTempPath,
            byte[] key,
            CancellationToken ct
        )
        {
            byte[] payload = await File.ReadAllBytesAsync(encryptedFilePath, ct);
            if (payload.Length < 28)
            {
                throw new CryptographicException(
                    $"Encrypted asset '{Path.GetFileName(encryptedFilePath)}' is corrupted or incomplete."
                );
            }

            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            int ciphertextLength = payload.Length - 28;
            byte[] ciphertext = new byte[ciphertextLength];

            Buffer.BlockCopy(payload, 0, nonce, 0, 12);
            Buffer.BlockCopy(payload, 12, tag, 0, 16);
            Buffer.BlockCopy(payload, 28, ciphertext, 0, ciphertextLength);

            byte[] plaintext = new byte[ciphertextLength];
            using (AesGcm aesGcm = new(key, 16))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            string? dir = Path.GetDirectoryName(destTempPath);
            if (!string.IsNullOrEmpty(dir))
            {
                _ = Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(destTempPath, plaintext, ct);
        }

        private static bool IsFileIdenticalToModSource(
            string cacheFile,
            string texturesDir,
            SkinModel skin
        )
        {
            try
            {
                FileInfo cfi = new(cacheFile);
                if (!cfi.Exists)
                {
                    return false;
                }

                string[] candidates = skin.Category.Equals(
                    "Paints",
                    StringComparison.OrdinalIgnoreCase
                )
                    ? [$"{skin.Name}.png", $"{skin.Name}.jpg"]
                    :
                    [
                        "details.jpg",
                        "details.png",
                        "lightmap.jpg",
                        "lightmap.png",
                        "alpha.jpg",
                        "alpha.png",
                        "object.3ds",
                    ];

                if (Directory.Exists(texturesDir) && DirectoryContainsMatch(cfi, texturesDir, candidates, SearchOption.TopDirectoryOnly))
                {
                    return true;
                }

                string? parentDir = Directory.GetParent(texturesDir)?.FullName;
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    if (DirectoryContainsMatch(cfi, parentDir, candidates, SearchOption.AllDirectories))
                    {
                        return true;
                    }

                    string? grandParentDir = Directory.GetParent(parentDir)?.FullName;
                    if (!string.IsNullOrEmpty(grandParentDir) && Directory.Exists(grandParentDir) &&
                        grandParentDir.Contains("Textures", StringComparison.OrdinalIgnoreCase))
                    {
                        if (DirectoryContainsMatch(cfi, grandParentDir, candidates, SearchOption.AllDirectories))
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error checking file hash for original backup safety.");
            }
            return false;
        }

        private static bool DirectoryContainsMatch(
            FileInfo cfi,
            string searchDir,
            string[] candidateNames,
            SearchOption searchOption
        )
        {
            try
            {
                byte[]? cacheHash = null;
                foreach (string candidate in candidateNames)
                {
                    foreach (string file in Directory.EnumerateFiles(searchDir, candidate, searchOption))
                    {
                        FileInfo mfi = new(file);
                        if (cfi.Length == mfi.Length)
                        {
                            cacheHash ??= SHA256.HashData(File.ReadAllBytes(cfi.FullName));
                            byte[] modHash = SHA256.HashData(File.ReadAllBytes(file));
                            if (cacheHash.AsSpan().SequenceEqual(modHash))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error scanning directory {Dir} for matching mod files.", searchDir);
            }
            return false;
        }

        private void EnsureOriginalsBackup(string cachePath, SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string originalsDir = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, Constants.OriginalsDir)
            );
            _ = Directory.CreateDirectory(originalsDir);

            string texturesDir = FileHelper.GetSafePath(
                AppDomain.CurrentDomain.BaseDirectory,
                skin.SourceFolder
            );

            string[] targets =
            [
                skin.DetailsTarget,
                skin.LightmapTarget,
                skin.AlphaTarget,
                skin.ModelTarget,
                skin.TracksTarget,
                skin.ImagesXmlTarget,
            ];
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
                    if (IsFileIdenticalToModSource(sourceFile, texturesDir, skin))
                    {
                        Log.Warning(
                            $"Skipping original backup for {target} because cache file matches a modded texture."
                        );
                        continue;
                    }

                    Log.Information(
                        $"First time seeing {target}. Saving original version to {originalsDir}"
                    );
                    string? destDir = Path.GetDirectoryName(backupFile);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        _ = Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, backupFile);
                }
            }
        }

        public string SelectiveBackup(string cachePath, SkinModel skin)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupZipPath = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, $"{skin.Name}_{timestamp}.zip")
            );

            try
            {
                Log.Information(
                    $"Creating selective compressed backup for {skin.Name} at {backupZipPath}"
                );

                string? dir = Path.GetDirectoryName(backupZipPath);
                if (dir != null)
                {
                    _ = Directory.CreateDirectory(dir);
                }

                string[] targets =
                [
                    skin.DetailsTarget,
                    skin.LightmapTarget,
                    skin.AlphaTarget,
                    skin.ModelTarget,
                    skin.TracksTarget,
                    skin.ImagesXmlTarget,
                ];

                string originalsDir = FileHelper.GetSafePath(
                    baseDir,
                    Path.Combine(Constants.BackupsDir, Constants.OriginalsDir)
                );

                string texturesDir = FileHelper.GetSafePath(
                    baseDir,
                    skin.SourceFolder
                );

                using ZipArchive archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create);
                foreach (string target in targets)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    string originalBackupFile = FileHelper.GetSafePath(originalsDir, target);
                    if (File.Exists(originalBackupFile))
                    {
                        _ = archive.CreateEntryFromFile(originalBackupFile, target);
                        continue;
                    }

                    string sourceFile = FileHelper.GetSafePath(cachePath, target);
                    if (File.Exists(sourceFile))
                    {
                        if (IsFileIdenticalToModSource(sourceFile, texturesDir, skin))
                        {
                            Log.Warning(
                                $"Excluding {target} from backup zip because the cache file is a modded texture and no original backup exists."
                            );
                            continue;
                        }

                        _ = archive.CreateEntryFromFile(sourceFile, target);
                    }
                }

                return backupZipPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Failed to create selective backup for {skin.Name}. Aborting swap.");
                throw;
            }
        }

        public async Task<string?> SwapAsync(
            string cachePath,
            SkinModel skin,
            string? inGamePaintName = null,
            CancellationToken ct = default
        )
        {
            Log.Information($"Applying skin: {skin.Name}");

            if (FileHelper.IsGameRunning())
            {
                return "ProTanki client is currently running. Please close the game before swapping.";
            }

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
                string[] requiredTargets =
                [
                    skin.DetailsTarget,
                    skin.LightmapTarget,
                    skin.AlphaTarget,
                    skin.ModelTarget,
                ];
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
                return skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(inGamePaintName)
                        ? $"Paint:{inGamePaintName}"
                    : skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase)
                        ? "NotCachedSupplies"
                    : "NotCached";
            }

            EnsureOriginalsBackup(cachePath, skin);
            string backupZip = SelectiveBackup(cachePath, skin);

            List<(string Source, string Target)> copyOperations = [];
            string texturesDir = FileHelper.GetSafePath(
                AppDomain.CurrentDomain.BaseDirectory,
                skin.SourceFolder
            );

            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                copyOperations.Add(
                    (
                        FileHelper.GetSafePath(texturesDir, $"{skin.Name}.png"),
                        FileHelper.GetSafePath(cachePath, skin.DetailsTarget)
                    )
                );
            }
            else
            {
                copyOperations.Add(
                    (
                        FileHelper.GetSafePath(texturesDir, "details.jpg"),
                        FileHelper.GetSafePath(cachePath, skin.DetailsTarget)
                    )
                );
                copyOperations.Add(
                    (
                        FileHelper.GetSafePath(texturesDir, "lightmap.jpg"),
                        FileHelper.GetSafePath(cachePath, skin.LightmapTarget)
                    )
                );
                copyOperations.Add(
                    (
                        FileHelper.GetSafePath(texturesDir, "alpha.jpg"),
                        FileHelper.GetSafePath(cachePath, skin.AlphaTarget)
                    )
                );
                if (!string.IsNullOrEmpty(skin.ModelTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "object.3ds"),
                            FileHelper.GetSafePath(cachePath, skin.ModelTarget)
                        )
                    );
                }
                if (!string.IsNullOrEmpty(skin.TracksTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "tracks.jpg"),
                            FileHelper.GetSafePath(cachePath, skin.TracksTarget)
                        )
                    );
                }
                if (!string.IsNullOrEmpty(skin.ImagesXmlTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "images.xml"),
                            FileHelper.GetSafePath(cachePath, skin.ImagesXmlTarget)
                        )
                    );
                }
            }

            foreach ((_, string Target) in copyOperations)
            {
                if (FileHelper.IsFileLocked(Target))
                {
                    return $"Cannot swap textures: target file '{Path.GetFileName(Target)}' is locked by another process.";
                }
            }

            List<(string Source, string Target, bool IsEncrypted)> resolvedOperations = [];
            bool hasEncryptedAssets = false;

            foreach ((string src, string dst) in copyOperations)
            {
                (string? resolvedPath, bool isEncrypted) = ResolveAssetSource(src);
                if (resolvedPath != null)
                {
                    resolvedOperations.Add((resolvedPath, dst, isEncrypted));
                    if (isEncrypted)
                    {
                        hasEncryptedAssets = true;
                    }
                }
            }

            byte[]? assetDecryptionKey = null;
            if (hasEncryptedAssets)
            {
                if (_premiumService == null)
                {
                    return "Premium service is unavailable. Cannot decrypt premium assets.";
                }

                assetDecryptionKey = await _premiumService.GetAssetDecryptionKeyAsync();
                if (assetDecryptionKey == null || assetDecryptionKey.Length != 32)
                {
                    return "An active Premium license is required to decrypt and swap this item.";
                }
            }

            List<string> writtenTempFiles = [];
            try
            {
                foreach ((string? src, string? dst, bool isEncrypted) in resolvedOperations)
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(src) || !File.Exists(src))
                    {
                        continue;
                    }

                    string tempTarget = dst + $".tmp_{Guid.NewGuid():N}";
                    if (isEncrypted && assetDecryptionKey != null)
                    {
                        await DecryptAssetAsync(src, tempTarget, assetDecryptionKey, ct);
                    }
                    else
                    {
                        await FileHelper.CopyWithRetryAsync(
                            src,
                            tempTarget,
                            maxRetries: 3,
                            delayMs: 150,
                            ct: ct
                        );
                    }
                    writtenTempFiles.Add(tempTarget);
                }

                for (int i = 0; i < resolvedOperations.Count; i++)
                {
                    if (i >= writtenTempFiles.Count)
                    {
                        break;
                    }

                    string tempTarget = writtenTempFiles[i];
                    string finalTarget = resolvedOperations[i].Target;

                    File.Move(tempTarget, finalTarget, overwrite: true);
                }

                Log.Information("Skin {SkinName} swapped atomically.", skin.Name);
                return null;
            }
            catch (CryptographicException cryptEx)
            {
                Log.Error(
                    cryptEx,
                    "Cryptographic failure while decrypting asset for {SkinName}. Triggering rollback.",
                    skin.Name
                );

                foreach (string tmp in writtenTempFiles)
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch
                    {
                        Log.Error("Failed to delete temporary file: {TempFile}", tmp);
                    }
                }

                try
                {
                    _ = RestoreFromBackup(cachePath, backupZip);
                }
                catch (Exception rbEx)
                {
                    Log.Error(rbEx, "Rollback failed after decryption error.");
                }

                return $"Failed to decrypt premium asset: {cryptEx.Message}";
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Skin swap failed mid-operation. Triggering emergency rollback from {BackupZip}",
                    backupZip
                );

                foreach (string tmp in writtenTempFiles)
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch { }
                }

                try
                {
                    _ = RestoreFromBackup(cachePath, backupZip);
                    Log.Information(
                        "Rollback completed successfully. Game files restored to pre-swap state."
                    );
                }
                catch (Exception rollbackEx)
                {
                    Log.Fatal(
                        rollbackEx,
                        "CRITICAL: Automated rollback failed after failed skin swap!"
                    );
                }

                return $"Swap failed and was rolled back: {ex.Message}";
            }
        }

        public string? Swap(
            string cachePath,
            SkinModel skin,
            string? inGamePaintName = null,
            CancellationToken ct = default
        )
        {
            return SwapAsync(cachePath, skin, inGamePaintName, ct).GetAwaiter().GetResult();
        }

        public async Task<string?> SwapBatchAsync(
            string cachePath,
            IEnumerable<SkinModel> skins,
            string? inGamePaintName = null,
            CancellationToken ct = default
        )
        {
            List<SkinModel> skinsList = [.. skins];
            string skinsCounterString = skinsList.Count > 1 ? "skins" : "skin";
            Log.Information($"Starting batch swap for {skinsList.Count} {skinsCounterString}.");

            if (FileHelper.IsGameRunning())
            {
                return "ProTanki client is currently running. Please close the game before swapping.";
            }

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
                ct.ThrowIfCancellationRequested();
                string? result = await SwapAsync(cachePath, skin, inGamePaintName, ct);
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
            List<string> itemsToEquip =
            [
                .. notCachedSkins,
                .. notCachedPaints.Select(p => $"Paint '{p}'"),
            ];

            if (anyCacheWithExtensions)
            {
                errorMessages.Add(
                    "The cache files seem to have extensions added to them (e.g. .png, .jpg). Please rename them back to plain files without extensions (e.g. by running 'ren *.png *' inside the cache folder) so the app can recognize and swap them."
                );
            }
            else
            {
                if (itemsToEquip.Count != 0)
                {
                    string list = string.Join("\n", itemsToEquip.Select(item => $"- {item}"));
                    errorMessages.Add(
                        $"Please open ProTanki and equip the following item(s) first to cache them:\n{list}"
                    );
                }

                if (notCachedSupplies.Count != 0)
                {
                    string list = string.Join("\n", notCachedSupplies.Select(item => $"- {item}"));
                    errorMessages.Add(
                        $"Please collect the following box(es) or supply(ies) in-game first to cache them:\n{list}"
                    );
                }

                if (otherFailures.Count != 0)
                {
                    string list = string.Join("\n", otherFailures.Select(f => $"- {f}"));
                    errorMessages.Add($"The following skin(s) failed to swap:\n{list}");
                }
            }

            if (errorMessages.Count != 0)
            {
                return string.Join("\n\n", errorMessages);
            }

            Log.Information("Batch swap completed successfully.");
            return null;
        }

        public string? SwapBatch(
            string cachePath,
            IEnumerable<SkinModel> skins,
            string? inGamePaintName = null,
            CancellationToken ct = default
        )
        {
            return SwapBatchAsync(cachePath, skins, inGamePaintName, ct).GetAwaiter().GetResult();
        }

        public bool RestoreFullCache(string cachePath)
        {
            Log.Information($"Restoring full game cache from originals to {cachePath}...");

            string originalsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.BackupsDir,
                Constants.OriginalsDir
            );

            return RestoreFromBackup(cachePath, originalsDir);
        }

        public bool RestoreFromBackup(string cachePath, string backupPath)
        {
            string safeCachePath = Path.GetFullPath(cachePath);
            if (!safeCachePath.EndsWith(Path.DirectorySeparatorChar))
            {
                safeCachePath += Path.DirectorySeparatorChar;
            }

            if (Directory.Exists(backupPath))
            {
                IEnumerable<string> allFiles = Directory.EnumerateFiles(
                    backupPath,
                    "*",
                    SearchOption.AllDirectories
                );
                if (!allFiles.Any())
                {
                    Log.Warning($"Restore skipped: Backup directory {backupPath} is empty.");
                    return false;
                }

                try
                {
                    int restoreCount = 0;
                    foreach (string file in allFiles)
                    {
                        string relativePath = Path.GetRelativePath(backupPath, file);
                        string destFile = FileHelper.GetSafePath(cachePath, relativePath);
                        string? destDir = Path.GetDirectoryName(destFile);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            _ = Directory.CreateDirectory(destDir);
                        }
                        File.Copy(file, destFile, true);
                        restoreCount++;
                    }

                    Log.Information(
                        $"Successfully restored {restoreCount} {(restoreCount == 1 ? "file" : "files")} from directory {backupPath}."
                    );
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to restore textures from directory {backupPath}.");
                    throw;
                }
            }
            else if (
                File.Exists(backupPath)
                && backupPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            )
            {
                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(backupPath);
                    int restoreCount = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string sanitizedEntry = entry.FullName.Replace('\\', '/');
                        string destFile = Path.GetFullPath(
                            Path.Combine(safeCachePath, sanitizedEntry)
                        );
                        if (!destFile.StartsWith(safeCachePath, StringComparison.OrdinalIgnoreCase))
                        {
                            Log.Warning(
                                "Zip Slip detected in backup archive: {Entry}",
                                entry.FullName
                            );
                            continue;
                        }

                        string? destDir = Path.GetDirectoryName(destFile);
                        if (!string.IsNullOrEmpty(destDir))
                        {
                            _ = Directory.CreateDirectory(destDir);
                        }
                        entry.ExtractToFile(destFile, true);
                        restoreCount++;
                    }
                    Log.Information(
                        $"Successfully restored {restoreCount} {(restoreCount == 1 ? "file" : "files")} from zip backup {backupPath}."
                    );
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
            string backupsRoot = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.BackupsDir
            );

            if (!Directory.Exists(backupsRoot))
            {
                return;
            }

            try
            {
                DateTime threshold = DateTime.Now.AddDays(-maxDays);
                int purgeCount = 0;

                foreach (string dir in Directory.EnumerateDirectories(backupsRoot))
                {
                    if (
                        Path.GetFileName(dir)
                            .Equals(Constants.OriginalsDir, StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }

                    DirectoryInfo di = new(dir);
                    if (di.CreationTime < threshold)
                    {
                        Log.Information(
                            $"Purging old backup directory: {di.Name} (Created: {di.CreationTime})"
                        );
                        di.Delete(true);
                        purgeCount++;
                    }
                }

                foreach (string file in Directory.EnumerateFiles(backupsRoot, "*.zip"))
                {
                    FileInfo fi = new(file);
                    if (fi.CreationTime < threshold)
                    {
                        Log.Information(
                            $"Purging old zip backup: {fi.Name} (Created: {fi.CreationTime})"
                        );
                        fi.Delete();
                        purgeCount++;
                    }
                }

                if (purgeCount > 0)
                {
                    Log.Information(
                        $"Purged {purgeCount} old {(purgeCount == 1 ? "backup" : "backups")}."
                    );
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
                try
                {
                    Directory.Delete(cachePath, true);
                    _ = Directory.CreateDirectory(cachePath);
                    Log.Information("Successfully cleared cache directory.");
                }
                catch (Exception ex)
                {
                    Log.Warning(
                        $"Failed to clear cache via Directory.Delete ({ex.Message}). Falling back to file-by-file deletion."
                    );
                    int fileCount = 0;
                    IEnumerable<string> files = Directory.EnumerateFiles(
                        cachePath,
                        "*",
                        SearchOption.AllDirectories
                    );
                    foreach (string file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            fileCount++;
                        }
                        catch (Exception fileEx)
                        {
                            Log.Warning($"Failed to delete file {file}: {fileEx.Message}");
                        }
                    }
                    Log.Information($"Cleared {fileCount} individual cache files.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to clear cache directory.");
            }
        }

        private static bool IsShotEffectModded(string cacheFile, string texturesDir, string target)
        {
            try
            {
                FileInfo cfi = new(cacheFile);
                if (!cfi.Exists)
                {
                    return false;
                }

                string targetFileName = Path.GetFileName(target);

                if (Directory.Exists(texturesDir))
                {
                    string modSource = FileHelper.GetSafePath(texturesDir, target);
                    if (File.Exists(modSource))
                    {
                        FileInfo mfi = new(modSource);
                        if (cfi.Length == mfi.Length)
                        {
                            byte[] h1 = SHA256.HashData(File.ReadAllBytes(cacheFile));
                            byte[] h2 = SHA256.HashData(File.ReadAllBytes(modSource));
                            if (h1.AsSpan().SequenceEqual(h2))
                            {
                                return true;
                            }
                        }
                    }
                }

                string? parentDir = Directory.GetParent(texturesDir)?.FullName;
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    if (DirectoryContainsMatch(cfi, parentDir, [targetFileName], SearchOption.AllDirectories))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to compare shot effect hashes for backup.");
            }
            return false;
        }

        private void EnsureOriginalsBackup(string cachePath, ShotEffectModel shotEffect)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string originalsDir = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, Constants.OriginalsDir)
            );
            _ = Directory.CreateDirectory(originalsDir);

            string texturesDir = FileHelper.GetSafePath(
                AppDomain.CurrentDomain.BaseDirectory,
                shotEffect.SourceFolder
            );

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
                    if (IsShotEffectModded(sourceFile, texturesDir, target))
                    {
                        Log.Warning(
                            $"Skipping original backup for shot effect {target}: already modded."
                        );
                        continue;
                    }

                    Log.Information(
                        $"First time seeing {target}. Saving original version to {originalsDir}"
                    );
                    string? destDir = Path.GetDirectoryName(backupFile);
                    if (!string.IsNullOrEmpty(destDir))
                    {
                        _ = Directory.CreateDirectory(destDir);
                    }
                    File.Copy(sourceFile, backupFile);
                }
            }
        }

        public string SelectiveBackup(string cachePath, ShotEffectModel shotEffect)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupZipPath = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(
                    Constants.BackupsDir,
                    $"{shotEffect.Turret}_{shotEffect.Name}_{timestamp}.zip"
                )
            );

            try
            {
                Log.Information(
                    $"Creating selective compressed backup for shot effect {shotEffect.Turret} {shotEffect.Name} at {backupZipPath}"
                );

                string? dir = Path.GetDirectoryName(backupZipPath);
                if (dir != null)
                {
                    _ = Directory.CreateDirectory(dir);
                }

                string originalsDir = FileHelper.GetSafePath(
                    baseDir,
                    Path.Combine(Constants.BackupsDir, Constants.OriginalsDir)
                );

                string texturesDir = FileHelper.GetSafePath(
                    baseDir,
                    shotEffect.SourceFolder
                );

                using ZipArchive archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create);
                foreach (string target in shotEffect.Targets)
                {
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }

                    string originalBackupFile = FileHelper.GetSafePath(originalsDir, target);
                    if (File.Exists(originalBackupFile))
                    {
                        _ = archive.CreateEntryFromFile(originalBackupFile, target);
                        continue;
                    }

                    string sourceFile = FileHelper.GetSafePath(cachePath, target);
                    if (File.Exists(sourceFile))
                    {
                        if (IsShotEffectModded(sourceFile, texturesDir, target))
                        {
                            Log.Warning(
                                $"Excluding shot effect {target} from backup zip because the cache file is a modded texture and no original backup exists."
                            );
                            continue;
                        }

                        _ = archive.CreateEntryFromFile(sourceFile, target);
                    }
                }

                return backupZipPath;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    $"Failed to create selective backup for shot effect {shotEffect.Name}. Aborting swap."
                );
                throw;
            }
        }

        public async Task<string?> SwapShotEffectAsync(
            string cachePath,
            ShotEffectModel shotEffect,
            CancellationToken ct = default
        )
        {
            Log.Information($"Applying shot effect: {shotEffect.Turret} {shotEffect.Name}");

            if (FileHelper.IsGameRunning())
            {
                return "ProTanki client is currently running. Please close the game before swapping.";
            }

            if (!Directory.Exists(cachePath))
            {
                Log.Error($"Cache directory not found: {cachePath}");
                return "Cache directory not found.";
            }

            bool hasExtensionFiles = false;
            List<string> missingTargets = [];
            foreach (string target in shotEffect.Targets)
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

            if (missingTargets.Count != 0)
            {
                if (hasExtensionFiles)
                {
                    return "CacheWithExtensions";
                }
                Log.Error(
                    $"Missing target files in cache for shot effect {shotEffect.Turret} {shotEffect.Name}"
                );
                return "NotCached";
            }

            EnsureOriginalsBackup(cachePath, shotEffect);
            string backupZip = SelectiveBackup(cachePath, shotEffect);

            string texturesDir = FileHelper.GetSafePath(
                AppDomain.CurrentDomain.BaseDirectory,
                shotEffect.SourceFolder
            );

            List<(string Source, string Target)> copyOperations = [];
            foreach (string target in shotEffect.Targets)
            {
                string sourceFilePath = FileHelper.GetSafePath(texturesDir, target);
                string destFilePath = FileHelper.GetSafePath(cachePath, target);
                copyOperations.Add((sourceFilePath, destFilePath));
            }

            foreach ((_, string Target) in copyOperations)
            {
                if (FileHelper.IsFileLocked(Target))
                {
                    return $"Cannot swap shot effect: target file '{Path.GetFileName(Target)}' is locked by another process.";
                }
            }

            List<(string Source, string Target, bool IsEncrypted)> resolvedOperations = [];
            bool hasEncryptedAssets = false;

            foreach ((string src, string dst) in copyOperations)
            {
                (string? resolvedPath, bool isEncrypted) = ResolveAssetSource(src);
                if (resolvedPath != null)
                {
                    resolvedOperations.Add((resolvedPath, dst, isEncrypted));
                    if (isEncrypted)
                    {
                        hasEncryptedAssets = true;
                    }
                }
            }

            byte[]? assetDecryptionKey = null;
            if (hasEncryptedAssets)
            {
                if (_premiumService == null)
                {
                    return "Premium service is unavailable. Cannot decrypt premium assets.";
                }

                assetDecryptionKey = await _premiumService.GetAssetDecryptionKeyAsync();
                if (assetDecryptionKey == null || assetDecryptionKey.Length != 32)
                {
                    return "An active Premium license is required to decrypt and swap this shot effect.";
                }
            }

            List<string> writtenTempFiles = [];
            try
            {
                foreach ((string? src, string? dst, bool isEncrypted) in resolvedOperations)
                {
                    ct.ThrowIfCancellationRequested();
                    if (string.IsNullOrEmpty(src) || !File.Exists(src))
                    {
                        Log.Warning($"Source asset file not found locally: {src}");
                        continue;
                    }

                    string tempTarget = dst + $".tmp_{Guid.NewGuid():N}";
                    if (isEncrypted && assetDecryptionKey != null)
                    {
                        await DecryptAssetAsync(src, tempTarget, assetDecryptionKey, ct);
                    }
                    else
                    {
                        await FileHelper.CopyWithRetryAsync(
                            src,
                            tempTarget,
                            maxRetries: 3,
                            delayMs: 150,
                            ct: ct
                        );
                    }
                    writtenTempFiles.Add(tempTarget);
                }

                for (int i = 0; i < resolvedOperations.Count; i++)
                {
                    if (i >= writtenTempFiles.Count)
                    {
                        break;
                    }

                    string tempTarget = writtenTempFiles[i];
                    string finalTarget = resolvedOperations[i].Target;

                    File.Move(tempTarget, finalTarget, overwrite: true);
                }

                Log.Information(
                    $"Shot effect {shotEffect.Turret} {shotEffect.Name} applied successfully."
                );
                return null;
            }
            catch (CryptographicException cryptEx)
            {
                Log.Error(
                    cryptEx,
                    "Cryptographic failure while decrypting shot effect for {ShotEffect}. Triggering rollback.",
                    shotEffect.Name
                );

                foreach (string tmp in writtenTempFiles)
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch { }
                }

                try
                {
                    _ = RestoreFromBackup(cachePath, backupZip);
                }
                catch (Exception rbEx)
                {
                    Log.Error(rbEx, "Rollback failed after decryption error.");
                }

                return $"Failed to decrypt premium shot effect: {cryptEx.Message}";
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "Shot effect swap failed mid-operation. Triggering emergency rollback from {BackupZip}",
                    backupZip
                );

                foreach (string tmp in writtenTempFiles)
                {
                    try
                    {
                        if (File.Exists(tmp))
                        {
                            File.Delete(tmp);
                        }
                    }
                    catch { }
                }

                try
                {
                    _ = RestoreFromBackup(cachePath, backupZip);
                    Log.Information(
                        "Rollback completed successfully. Files restored to pre-swap state."
                    );
                }
                catch (Exception rollbackEx)
                {
                    Log.Fatal(
                        rollbackEx,
                        "CRITICAL: Automated rollback failed after failed shot effect swap!"
                    );
                }

                return $"Swap failed and was rolled back: {ex.Message}";
            }
        }

        public string? SwapShotEffect(
            string cachePath,
            ShotEffectModel shotEffect,
            CancellationToken ct = default
        )
        {
            return SwapShotEffectAsync(cachePath, shotEffect, ct).GetAwaiter().GetResult();
        }
    }
}
