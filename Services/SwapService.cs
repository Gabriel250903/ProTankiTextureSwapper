using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class SwapService(
        IPremiumService? premiumService = null,
        IHtml5CacheService? html5CacheService = null,
        IBntArchiveService? bntArchiveService = null,
        ISettingsService? settingsService = null
    ) : ISwapService
    {
        private readonly IPremiumService? _premiumService = premiumService;
        private readonly IBntArchiveService _bntArchiveService =
            bntArchiveService ?? new BntArchiveService();
        private readonly IHtml5CacheService _html5CacheService =
            html5CacheService
            ?? new Html5CacheService(bntArchiveService ?? new BntArchiveService());
        private readonly ISettingsService? _settingsService = settingsService;
        private ClientType? _cachedActiveClient;
        private DateTime _cachedClientCheckedUtc;
        private static readonly HttpClient _assetDownloadHttpClient = new(
            new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectTimeout = TimeSpan.FromSeconds(10),
            }
        )
        {
            Timeout = TimeSpan.FromSeconds(15),
        };

        static SwapService()
        {
            _assetDownloadHttpClient.DefaultRequestHeaders.Add("User-Agent", "TextureSwapper/2.0");
        }

        public string DetectCachePath()
        {
            ClientType clientType = _settingsService?.Load().ActiveClient ?? ClientType.Flash;
            return DetectCachePath(clientType);
        }

        public string DetectCachePath(ClientType clientType)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (clientType == ClientType.Html5)
            {
                string html5Path = Path.Combine(appData, Constants.Html5AppDataDir);
                Log.Debug($"Detecting HTML5 cache path. Suggested: {html5Path}");
                return Directory.Exists(html5Path) ? html5Path : string.Empty;
            }

            string fullPath = Path.Combine(
                appData,
                Constants.StandaloneLoaderDir,
                Constants.LocalStoreDir,
                Constants.CacheDirName
            );

            Log.Debug($"Detecting Flash cache path. Suggested: {fullPath}");
            return Directory.Exists(fullPath) ? fullPath : string.Empty;
        }

        public bool IsHtml5CachePath(string cachePath)
        {
            if (_html5CacheService.TryResolveHtml5Paths(cachePath, out _, out _))
            {
                return true;
            }

            if (_settingsService != null)
            {
                DateTime now = DateTime.UtcNow;
                if (
                    !_cachedActiveClient.HasValue
                    || (now - _cachedClientCheckedUtc).TotalSeconds > 2
                )
                {
                    _cachedActiveClient = _settingsService.Load().ActiveClient;
                    _cachedClientCheckedUtc = now;
                }

                if (_cachedActiveClient == ClientType.Html5)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsTargetCached(string cachePath, string target)
        {
            if (string.IsNullOrWhiteSpace(cachePath) || string.IsNullOrWhiteSpace(target))
            {
                return false;
            }

            if (IsHtml5CachePath(cachePath))
            {
                return Html5ResourceMapper.TryDecodeBase64Target(
                        target,
                        out Html5ResourceTarget? decoded
                    )
                    && decoded != null
                    && _html5CacheService.ResourceExists(cachePath, decoded.ResourceId);
            }

            string path = FileHelper.GetSafePath(cachePath, target);
            return File.Exists(path);
        }

        private static bool TryDownloadMissingAsset(string filePath)
        {
            try
            {
                string normalized = filePath.Replace('\\', '/');
                int texIndex = normalized.IndexOf("/Textures/", StringComparison.OrdinalIgnoreCase);
                string relativePath;
                if (texIndex >= 0)
                {
                    relativePath = normalized[(texIndex + 1)..];
                }
                else if (normalized.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = normalized;
                }
                else
                {
                    return false;
                }

                string url = $"{Constants.GitHubRawUrl}/{relativePath}";

                Log.Information(
                    "Attempting to auto-download missing asset from GitHub: {Url}",
                    url
                );

                using HttpResponseMessage response = _assetDownloadHttpClient
                    .GetAsync(url)
                    .GetAwaiter()
                    .GetResult();
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning(
                        "Asset not found on remote server ({StatusCode}): {Url}",
                        response.StatusCode,
                        url
                    );
                    return false;
                }

                byte[] data = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                if (data.Length == 0)
                {
                    return false;
                }

                string? dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    _ = Directory.CreateDirectory(dir);
                }

                File.WriteAllBytes(filePath, data);
                Log.Information("Successfully downloaded missing asset: {Path}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to download missing asset: {Path}", filePath);
                return false;
            }
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

            string encMirror =
                src.Replace(
                        "Textures/ShotEffects",
                        "Textures/Encrypted/ShotEffects",
                        StringComparison.OrdinalIgnoreCase
                    )
                    .Replace(
                        "Textures\\ShotEffects",
                        "Textures\\Encrypted\\ShotEffects",
                        StringComparison.OrdinalIgnoreCase
                    ) + ".enc";
            if (File.Exists(encMirror))
            {
                return (encMirror, true);
            }

            string ext = Path.GetExtension(src);
            if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
            {
                string alt = Path.ChangeExtension(src, ".jpg");
                if (File.Exists(alt))
                {
                    return (alt, false);
                }

                if (File.Exists(alt + ".enc"))
                {
                    return (alt + ".enc", true);
                }

                string altJpeg = Path.ChangeExtension(src, ".jpeg");
                if (File.Exists(altJpeg))
                {
                    return (altJpeg, false);
                }

                if (File.Exists(altJpeg + ".enc"))
                {
                    return (altJpeg + ".enc", true);
                }
            }
            else if (
                ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            )
            {
                string alt = Path.ChangeExtension(src, ".png");
                if (File.Exists(alt))
                {
                    return (alt, false);
                }

                if (File.Exists(alt + ".enc"))
                {
                    return (alt + ".enc", true);
                }
            }

            if (
                !src.Contains("Custom", StringComparison.OrdinalIgnoreCase)
                && !src.Contains("Backups", StringComparison.OrdinalIgnoreCase)
            )
            {
                if (TryDownloadMissingAsset(src))
                {
                    return (src, false);
                }

                if (TryDownloadMissingAsset(src + ".enc"))
                {
                    return (src + ".enc", true);
                }

                if (src.Contains("ShotEffects", StringComparison.OrdinalIgnoreCase))
                {
                    string remoteEncMirror =
                        src.Replace(
                                "Textures/ShotEffects",
                                "Textures/Encrypted/ShotEffects",
                                StringComparison.OrdinalIgnoreCase
                            )
                            .Replace(
                                "Textures\\ShotEffects",
                                "Textures\\Encrypted\\ShotEffects",
                                StringComparison.OrdinalIgnoreCase
                            ) + ".enc";
                    if (TryDownloadMissingAsset(remoteEncMirror))
                    {
                        return (remoteEncMirror, true);
                    }
                }

                if (!string.IsNullOrEmpty(ext))
                {
                    string[] altExtensions =
                        ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                            ? [".jpg", ".jpeg", ".png.enc", ".jpg.enc"]
                        : ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                        || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                            ? [".png", ".jpg.enc", ".png.enc"]
                        : [];

                    foreach (string altExt in altExtensions)
                    {
                        string altCandidate = Path.ChangeExtension(src, altExt);
                        if (TryDownloadMissingAsset(altCandidate))
                        {
                            bool isEnc = altCandidate.EndsWith(
                                ".enc",
                                StringComparison.OrdinalIgnoreCase
                            );
                            return (altCandidate, isEnc);
                        }
                    }
                }
            }

            return (null, false);
        }

        private static string ResolveTexturesDirectory(string relativeFolder)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string primary = FileHelper.GetSafePath(baseDir, relativeFolder);
            if (Directory.Exists(primary))
            {
                return primary;
            }

            string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            string devCandidate = FileHelper.GetSafePath(devRoot, relativeFolder);
            return Directory.Exists(devCandidate) ? devCandidate : primary;
        }

        private static string ResolveFallbackAssetPath(string relativePath)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string primary = FileHelper.GetSafePath(baseDir, relativePath);
            if (File.Exists(primary) || File.Exists(primary + ".enc"))
            {
                return primary;
            }

            string devRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            string devCandidate = FileHelper.GetSafePath(devRoot, relativePath);
            return File.Exists(devCandidate) || File.Exists(devCandidate + ".enc")
                ? devCandidate
                : primary;
        }

        private static async Task<byte[]> DecryptAssetToBytesAsync(
            string encryptedFilePath,
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

            return plaintext;
        }

        private static async Task DecryptAssetAsync(
            string encryptedFilePath,
            string destTempPath,
            byte[] key,
            CancellationToken ct
        )
        {
            byte[] plaintext = await DecryptAssetToBytesAsync(encryptedFilePath, key, ct);

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
                    ? [$"{skin.Name}.png", $"{skin.Name}.jpg", $"{skin.Name}.jpeg"]
                    :
                    [
                        "details.jpg",
                        "details.png",
                        "details.jpeg",
                        "lightmap.jpg",
                        "lightmap.png",
                        "alpha.jpg",
                        "alpha.png",
                        "para.jpg",
                        "para.png",
                        "tracks.jpg",
                        "tracks.png",
                        "object.3ds",
                    ];

                if (
                    Directory.Exists(texturesDir)
                    && DirectoryContainsMatch(
                        cfi,
                        texturesDir,
                        candidates,
                        SearchOption.TopDirectoryOnly
                    )
                )
                {
                    return true;
                }

                string? parentDir = Directory.GetParent(texturesDir)?.FullName;
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    if (
                        DirectoryContainsMatch(
                            cfi,
                            parentDir,
                            candidates,
                            SearchOption.AllDirectories
                        )
                    )
                    {
                        return true;
                    }

                    string? grandParentDir = Directory.GetParent(parentDir)?.FullName;
                    if (
                        !string.IsNullOrEmpty(grandParentDir)
                        && Directory.Exists(grandParentDir)
                        && grandParentDir.Contains("Textures", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        if (
                            DirectoryContainsMatch(
                                cfi,
                                grandParentDir,
                                candidates,
                                SearchOption.AllDirectories
                            )
                        )
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
                    foreach (
                        string file in Directory.EnumerateFiles(searchDir, candidate, searchOption)
                    )
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
                Log.Warning(
                    ex,
                    "Error scanning directory {Dir} for matching mod files.",
                    searchDir
                );
            }
            return false;
        }

        private void EnsureHtml5OriginalsBackup(string cachePath, IEnumerable<string?> targets)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string originalsDir = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, "Html5", Constants.OriginalsDir)
            );
            _ = Directory.CreateDirectory(originalsDir);

            HashSet<long> resourceIds = [];
            foreach (string? target in targets)
            {
                if (
                    Html5ResourceMapper.TryDecodeBase64Target(
                        target,
                        out Html5ResourceTarget? decoded
                    )
                    && decoded != null
                )
                {
                    _ = resourceIds.Add(decoded.ResourceId);
                }
            }

            foreach (long resId in resourceIds)
            {
                string backupFile = Path.Combine(originalsDir, $"{resId}.bnt");
                if (!File.Exists(backupFile))
                {
                    byte[]? rawBnt = _html5CacheService.ReadRawBntBytes(cachePath, resId);
                    if (rawBnt != null && rawBnt.Length > 0)
                    {
                        File.WriteAllBytes(backupFile, rawBnt);
                        Log.Information(
                            "Saved HTML5 original 1BNT backup for resource {ResourceId} to {Path}",
                            resId,
                            backupFile
                        );
                    }
                }
            }
        }

        private static string SanitizeFileNamePart(string value, string fallback = "skin")
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }

            string sanitized = new string(chars).Trim();
            return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
        }

        private string SelectiveHtml5Backup(
            string cachePath,
            string backupPrefix,
            IEnumerable<string?> targets
        )
        {
            string safePrefix = SanitizeFileNamePart(backupPrefix);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}"[..24];
            string backupZipPath = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, "Html5", $"{safePrefix}_{timestamp}.zip")
            );

            string? dir = Path.GetDirectoryName(backupZipPath);
            if (dir != null)
            {
                _ = Directory.CreateDirectory(dir);
            }

            string originalsDir = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, "Html5", Constants.OriginalsDir)
            );

            HashSet<long> resourceIds = [];
            foreach (string? target in targets)
            {
                if (
                    Html5ResourceMapper.TryDecodeBase64Target(
                        target,
                        out Html5ResourceTarget? decoded
                    )
                    && decoded != null
                )
                {
                    _ = resourceIds.Add(decoded.ResourceId);
                }
            }

            using ZipArchive archive = ZipFile.Open(backupZipPath, ZipArchiveMode.Create);
            foreach (long resId in resourceIds)
            {
                string entryName = $"{resId}.bnt";
                string originalBackupFile = Path.Combine(originalsDir, entryName);
                if (File.Exists(originalBackupFile))
                {
                    _ = archive.CreateEntryFromFile(originalBackupFile, entryName);
                    continue;
                }

                byte[]? rawBnt = _html5CacheService.ReadRawBntBytes(cachePath, resId);
                if (rawBnt != null && rawBnt.Length > 0)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(entryName);
                    using Stream entryStream = entry.Open();
                    entryStream.Write(rawBnt, 0, rawBnt.Length);
                }
            }

            return backupZipPath;
        }

        private void EnsureOriginalsBackup(string cachePath, SkinModel skin)
        {
            string[] targets =
            [
                skin.DetailsTarget,
                skin.LightmapTarget,
                skin.AlphaTarget,
                skin.ModelTarget,
                skin.TracksTarget,
                skin.ImagesXmlTarget,
                skin.SecondaryDetailsTarget,
                skin.SecondaryModelTarget,
            ];

            if (IsHtml5CachePath(cachePath))
            {
                EnsureHtml5OriginalsBackup(cachePath, targets);
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string originalsDir = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, Constants.OriginalsDir)
            );
            _ = Directory.CreateDirectory(originalsDir);

            string texturesDir = FileHelper.GetSafePath(
                AppDomain.CurrentDomain.BaseDirectory,
                skin.EffectiveSourceFolder
            );

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
            string[] targets =
            [
                skin.DetailsTarget,
                skin.LightmapTarget,
                skin.AlphaTarget,
                skin.ModelTarget,
                skin.TracksTarget,
                skin.ImagesXmlTarget,
                skin.SecondaryDetailsTarget,
                skin.SecondaryModelTarget,
            ];

            string rawBackupPrefix =
                !string.IsNullOrEmpty(skin.TargetTier)
                && !skin.TargetTier.Equals("M3", StringComparison.OrdinalIgnoreCase)
                    ? $"{skin.Name}_{skin.TargetTier}"
                    : skin.Name;
            string backupPrefix = SanitizeFileNamePart(rawBackupPrefix);

            if (IsHtml5CachePath(cachePath))
            {
                return SelectiveHtml5Backup(cachePath, backupPrefix, targets);
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}"[..24];
            string backupZipPath = FileHelper.GetSafePath(
                baseDir,
                Path.Combine(Constants.BackupsDir, $"{backupPrefix}_{timestamp}.zip")
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

                string originalsDir = FileHelper.GetSafePath(
                    baseDir,
                    Path.Combine(Constants.BackupsDir, Constants.OriginalsDir)
                );

                string texturesDir = FileHelper.GetSafePath(baseDir, skin.EffectiveSourceFolder);

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

        private async Task<string?> SwapHtml5SkinAsync(
            string cachePath,
            SkinModel skin,
            string? inGamePaintName,
            CancellationToken ct
        )
        {
            string[] candidateTargets = skin.Category.Equals(
                "Paints",
                StringComparison.OrdinalIgnoreCase
            )
                ? [skin.DetailsTarget]
                :
                [
                    skin.DetailsTarget,
                    skin.LightmapTarget,
                    skin.AlphaTarget,
                    skin.ModelTarget,
                    skin.TracksTarget,
                    skin.ImagesXmlTarget,
                    skin.SecondaryDetailsTarget,
                    skin.SecondaryModelTarget,
                ];

            HashSet<long> requiredResourceIds = [];
            foreach (string? target in candidateTargets)
            {
                if (
                    !string.IsNullOrEmpty(target)
                    && Html5ResourceMapper.TryDecodeBase64Target(
                        target,
                        out Html5ResourceTarget? decoded
                    )
                    && decoded != null
                )
                {
                    _ = requiredResourceIds.Add(decoded.ResourceId);
                }
            }

            if (
                requiredResourceIds.Count == 0
                || requiredResourceIds.Any(id => !_html5CacheService.ResourceExists(cachePath, id))
            )
            {
                Log.Error($"Missing HTML5 IndexedDB resource(s) for {skin.Name}");
                return skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(inGamePaintName)
                        ? $"Paint:{inGamePaintName}"
                    : skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase)
                        ? "NotCachedSupplies"
                    : "NotCached";
            }

            EnsureOriginalsBackup(cachePath, skin);
            string backupZip = SelectiveBackup(cachePath, skin);

            string texturesDir = ResolveTexturesDirectory(skin.EffectiveSourceFolder);

            List<(string Source, string TargetBase64)> copyOperations = [];
            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                string paintFile = $"{skin.Name}.png";
                if (!string.IsNullOrEmpty(skin.PreviewImage))
                {
                    string prevExt = Path.GetExtension(skin.PreviewImage);
                    if (
                        !string.IsNullOrEmpty(prevExt)
                        && !prevExt.Contains("preview", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        paintFile = $"{skin.Name}{prevExt}";
                    }
                }

                if (
                    !File.Exists(FileHelper.GetSafePath(texturesDir, paintFile))
                    && File.Exists(FileHelper.GetSafePath(texturesDir, $"{skin.Name}.jpg"))
                )
                {
                    paintFile = $"{skin.Name}.jpg";
                }

                if (!string.IsNullOrEmpty(skin.DetailsTarget))
                {
                    copyOperations.Add(
                        (FileHelper.GetSafePath(texturesDir, paintFile), skin.DetailsTarget)
                    );
                }
            }
            else
            {
                string detailsFile = "details.jpg";
                if (
                    skin.Category.Equals("Spawners", StringComparison.OrdinalIgnoreCase)
                    && skin.Name.Contains("Parachute", StringComparison.OrdinalIgnoreCase)
                )
                {
                    detailsFile = "para.jpg";
                }

                if (!string.IsNullOrEmpty(skin.DetailsTarget))
                {
                    copyOperations.Add(
                        (FileHelper.GetSafePath(texturesDir, detailsFile), skin.DetailsTarget)
                    );
                }
                if (!string.IsNullOrEmpty(skin.LightmapTarget))
                {
                    copyOperations.Add(
                        (FileHelper.GetSafePath(texturesDir, "lightmap.jpg"), skin.LightmapTarget)
                    );
                }
                if (!string.IsNullOrEmpty(skin.AlphaTarget))
                {
                    copyOperations.Add(
                        (FileHelper.GetSafePath(texturesDir, "alpha.jpg"), skin.AlphaTarget)
                    );
                }
                if (!string.IsNullOrEmpty(skin.ModelTarget))
                {
                    string modelSourcePath = FileHelper.GetSafePath(texturesDir, "object.3ds");
                    if (
                        !File.Exists(modelSourcePath)
                        && !File.Exists(modelSourcePath + ".enc")
                        && !string.IsNullOrEmpty(skin.FallbackModelPath)
                    )
                    {
                        modelSourcePath = ResolveFallbackAssetPath(skin.FallbackModelPath);
                    }
                    copyOperations.Add((modelSourcePath, skin.ModelTarget));
                }
                if (!string.IsNullOrEmpty(skin.TracksTarget))
                {
                    copyOperations.Add(
                        (FileHelper.GetSafePath(texturesDir, "tracks.jpg"), skin.TracksTarget)
                    );
                }
                if (!string.IsNullOrEmpty(skin.ImagesXmlTarget))
                {
                    copyOperations.Add(
                        (FileHelper.GetSafePath(texturesDir, "images.xml"), skin.ImagesXmlTarget)
                    );
                }
                if (!string.IsNullOrEmpty(skin.SecondaryDetailsTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "details.jpg"),
                            skin.SecondaryDetailsTarget
                        )
                    );
                }
                if (!string.IsNullOrEmpty(skin.SecondaryModelTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "object.3ds"),
                            skin.SecondaryModelTarget
                        )
                    );
                }
            }

            List<(
                string Source,
                Html5ResourceTarget Target,
                bool IsEncrypted
            )> resolvedOperations = [];
            List<string> missingRequired = [];
            bool hasEncryptedAssets = false;

            foreach ((string src, string targetB64) in copyOperations)
            {
                if (
                    !Html5ResourceMapper.TryDecodeBase64Target(
                        targetB64,
                        out Html5ResourceTarget? decoded
                    )
                    || decoded == null
                )
                {
                    continue;
                }

                string fileName = Path.GetFileName(src);
                bool isOptional =
                    fileName.Equals("alpha.jpg", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("images.xml", StringComparison.OrdinalIgnoreCase);

                (string? resolvedPath, bool isEncrypted) = ResolveAssetSource(src);
                if (resolvedPath != null)
                {
                    resolvedOperations.Add((resolvedPath, decoded, isEncrypted));
                    if (isEncrypted)
                    {
                        hasEncryptedAssets = true;
                    }
                }
                else if (!isOptional)
                {
                    missingRequired.Add(fileName);
                }
            }

            if (missingRequired.Count > 0)
            {
                return $"Failed to apply skin '{skin.Name}': missing required texture file(s): {string.Join(", ", missingRequired.Distinct(StringComparer.OrdinalIgnoreCase))}.";
            }

            if (resolvedOperations.Count == 0)
            {
                return $"Failed to apply skin: texture files for '{skin.Name}' were not found locally.";
            }

            byte[]? assetDecryptionKey = null;
            if (hasEncryptedAssets)
            {
                if (!IntegrityChecker.ValidateAssemblyIntegrity())
                {
                    return "Application integrity verification failed. Cannot decrypt premium assets.";
                }

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

            try
            {
                string originalsDir = FileHelper.GetSafePath(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Path.Combine(Constants.BackupsDir, "Html5", Constants.OriginalsDir)
                );

                foreach (
                    IGrouping<
                        long,
                        (string Source, Html5ResourceTarget Target, bool IsEncrypted)
                    > group in resolvedOperations.GroupBy(op => op.Target.ResourceId)
                )
                {
                    ct.ThrowIfCancellationRequested();
                    long resourceId = group.Key;

                    string origBntPath = Path.Combine(originalsDir, $"{resourceId}.bnt");
                    byte[]? baseBnt = File.Exists(origBntPath)
                        ? await File.ReadAllBytesAsync(origBntPath, ct)
                        : _html5CacheService.ReadRawBntBytes(cachePath, resourceId);

                    if (baseBnt == null)
                    {
                        Log.Error(
                            "Failed to read HTML5 1BNT payload for {SkinName} (resourceId: {ResourceId})",
                            skin.Name,
                            resourceId
                        );
                        return "NotCached";
                    }

                    List<TaraEntry> entries = _bntArchiveService.Unpack(baseBnt);
                    bool updatedAlpha = false;

                    foreach ((string src, Html5ResourceTarget target, bool isEncrypted) in group)
                    {
                        byte[] payload =
                            isEncrypted && assetDecryptionKey != null
                                ? await DecryptAssetToBytesAsync(src, assetDecryptionKey, ct)
                                : await File.ReadAllBytesAsync(src, ct);

                        if (
                            skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                            && (
                                skin.ItemName.Equals("Animated", StringComparison.OrdinalIgnoreCase)
                                || File.Exists(
                                    FileHelper.GetSafePath(texturesDir, $"{skin.Name}.json")
                                )
                            )
                        )
                        {
                            payload = EnsureAnimatedPaintMetadataHeader(
                                payload,
                                skin.Name,
                                texturesDir
                            );
                        }

                        TaraEntry? existing = entries.FirstOrDefault(e =>
                            e.FileName.Equals(
                                target.EntryFileName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );
                        if (existing != null)
                        {
                            existing.Data = payload;
                        }
                        else
                        {
                            entries.Add(
                                new TaraEntry { FileName = target.EntryFileName, Data = payload }
                            );
                        }

                        if (
                            target.EntryFileName.Equals(
                                "details_alpha.jpg",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            updatedAlpha = true;
                        }
                    }

                    if (updatedAlpha)
                    {
                        _bntArchiveService.UpdateModelImagesXml(entries, hasAlpha: true);
                    }

                    if (!_html5CacheService.WriteResourceEntries(cachePath, resourceId, entries))
                    {
                        throw new IOException(
                            $"Failed to write HTML5 resource {resourceId} to IndexedDB."
                        );
                    }
                }

                if (
                    skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase)
                    && (
                        skin.ItemName.Equals("Animated", StringComparison.OrdinalIgnoreCase)
                        || File.Exists(FileHelper.GetSafePath(texturesDir, $"{skin.Name}.json"))
                    )
                )
                {
                    if (!_html5CacheService.EnsureAnimatedPaintEnginePatched(cachePath))
                    {
                        Log.Warning(
                            "HTML5 core JS bundle was not found in Cache_Data; animated paint will remain static until cached."
                        );
                    }
                }

                Log.Information("HTML5 Skin {SkinName} swapped atomically.", skin.Name);
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "HTML5 skin swap failed for {SkinName}. Rolling back from {BackupZip}",
                    skin.Name,
                    backupZip
                );
                bool rolledBack = false;
                try
                {
                    rolledBack = RestoreFromBackup(cachePath, backupZip);
                }
                catch (Exception rbEx)
                {
                    Log.Error(rbEx, "Rollback failed for {SkinName}.", skin.Name);
                }
                return rolledBack
                    ? $"Swap failed and was rolled back: {ex.Message}"
                    : $"Swap failed and rollback was incomplete (backup: {backupZip}): {ex.Message}";
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

            if (FileHelper.IsCacheFileLocked(cachePath))
            {
                return "Cache files are currently locked by another process. Please close ProTanki and any browser/tools using the cache.";
            }

            if (IsHtml5CachePath(cachePath))
            {
                return await SwapHtml5SkinAsync(cachePath, skin, inGamePaintName, ct);
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
                    skin.SecondaryDetailsTarget,
                    skin.SecondaryModelTarget,
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
                skin.EffectiveSourceFolder
            );

            if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
            {
                string paintFile = $"{skin.Name}.png";
                if (!string.IsNullOrEmpty(skin.PreviewImage))
                {
                    string prevExt = Path.GetExtension(skin.PreviewImage);
                    if (
                        !string.IsNullOrEmpty(prevExt)
                        && !prevExt.Contains("preview", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        paintFile = $"{skin.Name}{prevExt}";
                    }
                }

                if (
                    !File.Exists(FileHelper.GetSafePath(texturesDir, paintFile))
                    && File.Exists(FileHelper.GetSafePath(texturesDir, $"{skin.Name}.jpg"))
                )
                {
                    paintFile = $"{skin.Name}.jpg";
                }

                copyOperations.Add(
                    (
                        FileHelper.GetSafePath(texturesDir, paintFile),
                        FileHelper.GetSafePath(cachePath, skin.DetailsTarget)
                    )
                );
            }
            else
            {
                string detailsFile = "details.jpg";
                if (
                    skin.Category.Equals("Spawners", StringComparison.OrdinalIgnoreCase)
                    && skin.Name.Contains("Parachute", StringComparison.OrdinalIgnoreCase)
                )
                {
                    detailsFile = "para.jpg";
                }

                copyOperations.Add(
                    (
                        FileHelper.GetSafePath(texturesDir, detailsFile),
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
                if (!string.IsNullOrEmpty(skin.SecondaryDetailsTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "details.jpg"),
                            FileHelper.GetSafePath(cachePath, skin.SecondaryDetailsTarget)
                        )
                    );
                }
                if (!string.IsNullOrEmpty(skin.SecondaryModelTarget))
                {
                    copyOperations.Add(
                        (
                            FileHelper.GetSafePath(texturesDir, "object.3ds"),
                            FileHelper.GetSafePath(cachePath, skin.SecondaryModelTarget)
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
            List<string> missingRequired = [];
            bool hasEncryptedAssets = false;

            foreach ((string src, string dst) in copyOperations)
            {
                string fileName = Path.GetFileName(src);
                bool isOptional =
                    fileName.Equals("alpha.jpg", StringComparison.OrdinalIgnoreCase)
                    || fileName.Equals("images.xml", StringComparison.OrdinalIgnoreCase);

                (string? resolvedPath, bool isEncrypted) = ResolveAssetSource(src);
                if (resolvedPath != null)
                {
                    resolvedOperations.Add((resolvedPath, dst, isEncrypted));
                    if (isEncrypted)
                    {
                        hasEncryptedAssets = true;
                    }
                }
                else if (!isOptional)
                {
                    missingRequired.Add(fileName);
                }
            }

            if (missingRequired.Count > 0)
            {
                return $"Failed to apply skin '{skin.Name}': missing required texture file(s): {string.Join(", ", missingRequired.Distinct(StringComparer.OrdinalIgnoreCase))}.";
            }

            if (resolvedOperations.Count == 0)
            {
                Log.Error(
                    $"No source texture files found locally for skin {skin.Category} {skin.Name} in {texturesDir}"
                );
                return $"Failed to apply skin: texture files for '{skin.Name}' were not found locally.";
            }

            byte[]? assetDecryptionKey = null;
            if (hasEncryptedAssets)
            {
                if (!IntegrityChecker.ValidateAssemblyIntegrity())
                {
                    return "Application integrity verification failed. Cannot decrypt premium assets.";
                }

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

                bool rolledBack = false;
                try
                {
                    rolledBack = RestoreFromBackup(cachePath, backupZip);
                }
                catch (Exception rbEx)
                {
                    Log.Error(rbEx, "Rollback failed after decryption error.");
                }

                return rolledBack
                    ? $"Failed to decrypt premium asset: {cryptEx.Message}"
                    : $"Failed to decrypt premium asset and rollback was incomplete (backup: {backupZip}): {cryptEx.Message}";
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

                bool rolledBack = false;
                try
                {
                    rolledBack = RestoreFromBackup(cachePath, backupZip);
                    if (rolledBack)
                    {
                        Log.Information(
                            "Rollback completed successfully. Game files restored to pre-swap state."
                        );
                    }
                    else
                    {
                        Log.Error("Rollback from {BackupZip} was incomplete.", backupZip);
                    }
                }
                catch (Exception rollbackEx)
                {
                    Log.Fatal(
                        rollbackEx,
                        "CRITICAL: Automated rollback failed after failed skin swap!"
                    );
                }

                return rolledBack
                    ? $"Swap failed and was rolled back: {ex.Message}"
                    : $"Swap failed and rollback was incomplete (backup: {backupZip}): {ex.Message}";
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

            if (FileHelper.IsCacheFileLocked(cachePath))
            {
                return "Cache files are currently locked by another process. Please close ProTanki and any browser/tools using the cache.";
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
                        string label =
                            !string.IsNullOrEmpty(skin.TargetTier)
                            && !skin.TargetTier.Equals("M3", StringComparison.OrdinalIgnoreCase)
                                ? $"{skin.Name} ({skin.ItemName} {skin.TargetTier})"
                                : skin.Name;
                        notCachedSkins.Add(label);
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

            if (FileHelper.IsCacheFileLocked(cachePath))
            {
                Log.Warning("RestoreFullCache aborted: cache files are locked.");
                return false;
            }

            if (IsHtml5CachePath(cachePath))
            {
                string originalsRoot = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    Constants.BackupsDir,
                    "Html5",
                    Constants.OriginalsDir
                );
                string originalsCacheDataDir = Path.Combine(originalsRoot, "Cache_Data");
                bool hasJsBackup =
                    Directory.Exists(originalsCacheDataDir)
                    && Directory.EnumerateFiles(originalsCacheDataDir, "f_*").Any();
                bool hasBntBackup =
                    Directory.Exists(originalsRoot)
                    && Directory
                        .EnumerateFiles(originalsRoot, "*.bnt", SearchOption.TopDirectoryOnly)
                        .Any();

                AppSettings? settings = ResolveSettingsService()?.Load();
                bool hadShotLightOverrides = settings?.ActiveHtml5ShotLighting.Count > 0;
                if (settings != null && hadShotLightOverrides)
                {
                    settings.ActiveHtml5ShotLighting.Clear();
                    ResolveSettingsService()?.Save(settings);
                }

                bool jsRestored =
                    !hasJsBackup || _html5CacheService.RestoreAnimatedPaintEngine(cachePath);
                bool bntRestored = !hasBntBackup || RestoreFromBackup(cachePath, originalsRoot);
                _html5CacheService.SyncShotDynamicLighting(cachePath);
                return (hasJsBackup || hasBntBackup || hadShotLightOverrides)
                    && jsRestored
                    && bntRestored;
            }

            string originalsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.BackupsDir,
                Constants.OriginalsDir
            );

            return RestoreFromBackup(cachePath, originalsDir);
        }

        public bool RestoreFromBackup(string cachePath, string backupPath)
        {
            if (FileHelper.IsCacheFileLocked(cachePath))
            {
                Log.Warning("RestoreFromBackup aborted: cache files are locked.");
                return false;
            }

            if (IsHtml5CachePath(cachePath))
            {
                if (Directory.Exists(backupPath))
                {
                    List<string> bntFiles =
                    [
                        .. Directory.EnumerateFiles(
                            backupPath,
                            "*.bnt",
                            SearchOption.AllDirectories
                        ),
                    ];
                    if (bntFiles.Count == 0)
                    {
                        Log.Warning(
                            $"HTML5 restore skipped: Backup directory {backupPath} has no .bnt files."
                        );
                        return false;
                    }

                    int expectedCount = 0;
                    int restoreCount = 0;
                    List<string> failedResources = [];
                    foreach (string file in bntFiles)
                    {
                        string stem = Path.GetFileNameWithoutExtension(file);
                        if (long.TryParse(stem, out long resId))
                        {
                            expectedCount++;
                            byte[] rawBnt = File.ReadAllBytes(file);
                            if (_html5CacheService.WriteRawBntBytes(cachePath, resId, rawBnt))
                            {
                                restoreCount++;
                            }
                            else
                            {
                                failedResources.Add(stem);
                            }
                        }
                    }

                    if (
                        expectedCount == 0
                        || restoreCount != expectedCount
                        || failedResources.Count > 0
                    )
                    {
                        Log.Warning(
                            "HTML5 restore incomplete from {BackupPath}: restored {Restored}/{Expected}. Failed: {Failed}",
                            backupPath,
                            restoreCount,
                            expectedCount,
                            string.Join(", ", failedResources)
                        );
                        return false;
                    }

                    Log.Information(
                        $"Successfully restored all {restoreCount} HTML5 resources from directory {backupPath}."
                    );
                    string trimmedBackup = Path.TrimEndingDirectorySeparator(backupPath);
                    if (
                        string.Equals(
                            Path.GetFileName(trimmedBackup),
                            Constants.OriginalsDir,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        ClearAllHtml5ShotLighting(cachePath);
                    }
                    return true;
                }
                else if (
                    File.Exists(backupPath)
                    && backupPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                )
                {
                    using ZipArchive archive = ZipFile.OpenRead(backupPath);
                    int expectedCount = 0;
                    int restoreCount = 0;
                    List<string> failedResources = [];
                    HashSet<long> restoredResourceIds = [];
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!entry.Name.EndsWith(".bnt", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        string stem = Path.GetFileNameWithoutExtension(entry.Name);
                        if (long.TryParse(stem, out long resId))
                        {
                            expectedCount++;
                            using Stream s = entry.Open();
                            using MemoryStream ms = new();
                            s.CopyTo(ms);
                            if (_html5CacheService.WriteRawBntBytes(cachePath, resId, ms.ToArray()))
                            {
                                restoreCount++;
                                _ = restoredResourceIds.Add(resId);
                            }
                            else
                            {
                                failedResources.Add(stem);
                            }
                        }
                    }

                    if (
                        expectedCount == 0
                        || restoreCount != expectedCount
                        || failedResources.Count > 0
                    )
                    {
                        Log.Warning(
                            "HTML5 zip restore incomplete from {BackupPath}: restored {Restored}/{Expected}. Failed: {Failed}",
                            backupPath,
                            restoreCount,
                            expectedCount,
                            string.Join(", ", failedResources)
                        );
                        return false;
                    }

                    Log.Information(
                        $"Successfully restored all {restoreCount} HTML5 resources from zip backup {backupPath}."
                    );
                    string zipStem = Path.GetFileNameWithoutExtension(backupPath);
                    ClearHtml5ShotLightingOnZipRestore(cachePath, zipStem, restoredResourceIds);
                    return true;
                }

                return false;
            }

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
                    string dirName = Path.GetFileName(dir);
                    if (
                        dirName.Equals(Constants.OriginalsDir, StringComparison.OrdinalIgnoreCase)
                        || dirName.Equals("Html5", StringComparison.OrdinalIgnoreCase)
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

                string html5BackupsRoot = Path.Combine(backupsRoot, "Html5");
                if (Directory.Exists(html5BackupsRoot))
                {
                    foreach (string file in Directory.EnumerateFiles(html5BackupsRoot, "*.zip"))
                    {
                        FileInfo fi = new(file);
                        if (fi.CreationTime < threshold)
                        {
                            fi.Delete();
                            purgeCount++;
                        }
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

        private static bool IsKnownFlashCacheDirectory(string cachePath)
        {
            try
            {
                string fullPath = Path.GetFullPath(cachePath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string? root = Path.GetPathRoot(fullPath);
                if (
                    string.IsNullOrEmpty(fullPath)
                    || string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return false;
                }

                string dirName = Path.GetFileName(fullPath);
                string? parentName = Path.GetFileName(
                    Path.GetDirectoryName(fullPath) ?? string.Empty
                );

                return (
                        dirName.Equals(Constants.CacheDirName, StringComparison.OrdinalIgnoreCase)
                        && parentName.Equals(
                            Constants.LocalStoreDir,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    || Directory
                        .EnumerateDirectories(fullPath, "aHR0cDov*", SearchOption.TopDirectoryOnly)
                        .Any();
            }
            catch
            {
                return false;
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

            if (FileHelper.IsCacheFileLocked(cachePath))
            {
                Log.Warning($"ClearCache aborted: cache files at {cachePath} are locked.");
                return;
            }

            if (
                _html5CacheService.TryResolveHtml5Paths(
                    cachePath,
                    out string levelDbDir,
                    out string blobDir
                )
            )
            {
                try
                {
                    _html5CacheService.InvalidateCache();
                    if (Directory.Exists(levelDbDir))
                    {
                        Directory.Delete(levelDbDir, true);
                    }
                    if (Directory.Exists(blobDir))
                    {
                        Directory.Delete(blobDir, true);
                    }
                    Log.Information("Successfully cleared HTML5 IndexedDB cache directories.");
                    ClearAllHtml5ShotLighting(cachePath);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to clear HTML5 IndexedDB cache directories.");
                }
                return;
            }

            if (!IsKnownFlashCacheDirectory(cachePath))
            {
                Log.Warning(
                    "Refusing to clear unrecognized directory '{CachePath}' that does not match Flash or HTML5 cache signatures.",
                    cachePath
                );
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
                    if (
                        DirectoryContainsMatch(
                            cfi,
                            parentDir,
                            [targetFileName],
                            SearchOption.AllDirectories
                        )
                    )
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
            if (IsHtml5CachePath(cachePath))
            {
                EnsureHtml5OriginalsBackup(cachePath, shotEffect.Targets);
                return;
            }

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
            if (IsHtml5CachePath(cachePath))
            {
                return SelectiveHtml5Backup(
                    cachePath,
                    $"{shotEffect.Turret}_{shotEffect.Name}",
                    shotEffect.Targets
                );
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string timestamp = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}"[..24];
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

                string texturesDir = FileHelper.GetSafePath(baseDir, shotEffect.SourceFolder);

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

        private async Task<string?> SwapHtml5ShotEffectAsync(
            string cachePath,
            ShotEffectModel shotEffect,
            CancellationToken ct
        )
        {
            List<Html5ResourceTarget> decodedTargets = [];
            foreach (string target in shotEffect.Targets)
            {
                if (
                    Html5ResourceMapper.TryDecodeBase64Target(
                        target,
                        out Html5ResourceTarget? decoded
                    )
                    && decoded != null
                )
                {
                    decodedTargets.Add(decoded);
                }
            }

            List<Html5ResourceTarget> cachedTargets =
            [
                .. decodedTargets.Where(t =>
                    _html5CacheService.ResourceExists(cachePath, t.ResourceId)
                ),
            ];

            if (cachedTargets.Count == 0)
            {
                Log.Error(
                    $"Missing HTML5 IndexedDB resources for shot effect {shotEffect.Turret} {shotEffect.Name}"
                );
                return "NotCached";
            }

            EnsureOriginalsBackup(cachePath, shotEffect);
            string backupZip = SelectiveBackup(cachePath, shotEffect);
            string originalsDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.BackupsDir,
                "Html5",
                Constants.OriginalsDir
            );

            string texturesDir = FileHelper.GetSafePath(
                AppDomain.CurrentDomain.BaseDirectory,
                shotEffect.SourceFolder
            );

            List<(
                string Source,
                Html5ResourceTarget Target,
                bool IsEncrypted
            )> resolvedOperations = [];
            bool hasEncryptedAssets = false;

            foreach (Html5ResourceTarget target in cachedTargets)
            {
                string src = FileHelper.GetSafePath(texturesDir, target.Base64Target);
                (string? resolvedPath, bool isEncrypted) = ResolveAssetSource(src);
                if (resolvedPath == null)
                {
                    string unpadded = target.Base64Target.TrimEnd('=');
                    if (!unpadded.Equals(target.Base64Target, StringComparison.Ordinal))
                    {
                        string altSrc = FileHelper.GetSafePath(texturesDir, unpadded);
                        (resolvedPath, isEncrypted) = ResolveAssetSource(altSrc);
                    }
                }
                if (resolvedPath != null)
                {
                    resolvedOperations.Add((resolvedPath, target, isEncrypted));
                    if (isEncrypted)
                    {
                        hasEncryptedAssets = true;
                    }
                }
            }

            if (resolvedOperations.Count == 0)
            {
                return $"Failed to apply shot effect: texture files for '{shotEffect.Name}' were not found locally.";
            }

            byte[]? assetDecryptionKey = null;
            if (hasEncryptedAssets)
            {
                if (!IntegrityChecker.ValidateAssemblyIntegrity())
                {
                    return "Application integrity verification failed. Cannot decrypt premium assets.";
                }

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

            try
            {
                int swappedCount = 0;
                foreach (
                    IGrouping<
                        long,
                        (string Source, Html5ResourceTarget Target, bool IsEncrypted)
                    > group in resolvedOperations.GroupBy(op => op.Target.ResourceId)
                )
                {
                    ct.ThrowIfCancellationRequested();
                    long resourceId = group.Key;
                    string origBntPath = Path.Combine(originalsDir, $"{resourceId}.bnt");
                    byte[]? baseBnt = File.Exists(origBntPath)
                        ? await File.ReadAllBytesAsync(origBntPath, ct)
                        : _html5CacheService.ReadRawBntBytes(cachePath, resourceId);
                    if (baseBnt == null)
                    {
                        continue;
                    }

                    List<TaraEntry> entries = _bntArchiveService.Unpack(baseBnt);

                    foreach ((string src, Html5ResourceTarget target, bool isEncrypted) in group)
                    {
                        byte[] payload =
                            isEncrypted && assetDecryptionKey != null
                                ? await DecryptAssetToBytesAsync(src, assetDecryptionKey, ct)
                                : await File.ReadAllBytesAsync(src, ct);

                        TaraEntry? existing = entries.FirstOrDefault(e =>
                            e.FileName.Equals(
                                target.EntryFileName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );
                        if (existing != null)
                        {
                            existing.Data = payload;
                        }
                        else
                        {
                            entries.Add(
                                new TaraEntry { FileName = target.EntryFileName, Data = payload }
                            );
                        }
                    }

                    if (!_html5CacheService.WriteResourceEntries(cachePath, resourceId, entries))
                    {
                        throw new IOException(
                            $"Failed to write HTML5 shot effect resource {resourceId} to IndexedDB."
                        );
                    }

                    swappedCount++;
                }

                if (swappedCount == 0)
                {
                    return "NotCached";
                }

                UpdateHtml5ShotLightingOnSwap(cachePath, shotEffect, cachedTargets);

                Log.Information(
                    $"HTML5 Shot effect {shotEffect.Turret} {shotEffect.Name} applied successfully."
                );
                return null;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "HTML5 shot effect swap failed for {ShotEffect}. Rolling back from {BackupZip}",
                    shotEffect.Name,
                    backupZip
                );
                bool rolledBack = false;
                try
                {
                    rolledBack = RestoreFromBackup(cachePath, backupZip);
                }
                catch (Exception rbEx)
                {
                    Log.Error(rbEx, "Rollback failed for {ShotEffectName}.", shotEffect.Name);
                }
                return rolledBack
                    ? $"Swap failed and was rolled back: {ex.Message}"
                    : $"Swap failed and rollback was incomplete (backup: {backupZip}): {ex.Message}";
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

            if (FileHelper.IsCacheFileLocked(cachePath))
            {
                return "Cache files are currently locked by another process. Please close ProTanki and any browser/tools using the cache.";
            }

            if (IsHtml5CachePath(cachePath))
            {
                return await SwapHtml5ShotEffectAsync(cachePath, shotEffect, ct);
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

            if (resolvedOperations.Count == 0)
            {
                Log.Error(
                    $"No source texture files found locally for shot effect {shotEffect.Turret} {shotEffect.Name} in {texturesDir}"
                );
                return $"Failed to apply shot effect: texture files for '{shotEffect.Name}' were not found locally.";
            }

            if (resolvedOperations.Count < copyOperations.Count)
            {
                Log.Warning(
                    $"Only {resolvedOperations.Count} of {copyOperations.Count} files found for shot effect {shotEffect.Turret} {shotEffect.Name}."
                );
            }

            byte[]? assetDecryptionKey = null;
            if (hasEncryptedAssets)
            {
                if (!IntegrityChecker.ValidateAssemblyIntegrity())
                {
                    return "Application integrity verification failed. Cannot decrypt premium assets.";
                }

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

                bool rolledBack = false;
                try
                {
                    rolledBack = RestoreFromBackup(cachePath, backupZip);
                }
                catch (Exception rbEx)
                {
                    Log.Error(rbEx, "Rollback failed after decryption error.");
                }

                return rolledBack
                    ? $"Failed to decrypt premium shot effect: {cryptEx.Message}"
                    : $"Failed to decrypt premium shot effect and rollback was incomplete (backup: {backupZip}): {cryptEx.Message}";
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

                bool rolledBack = false;
                try
                {
                    rolledBack = RestoreFromBackup(cachePath, backupZip);
                    if (rolledBack)
                    {
                        Log.Information(
                            "Rollback completed successfully. Files restored to pre-swap state."
                        );
                    }
                    else
                    {
                        Log.Error("Rollback from {BackupZip} was incomplete.", backupZip);
                    }
                }
                catch (Exception rollbackEx)
                {
                    Log.Fatal(
                        rollbackEx,
                        "CRITICAL: Automated rollback failed after failed shot effect swap!"
                    );
                }

                return rolledBack
                    ? $"Swap failed and was rolled back: {ex.Message}"
                    : $"Swap failed and rollback was incomplete (backup: {backupZip}): {ex.Message}";
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

        private static byte[] EnsureAnimatedPaintMetadataHeader(
            byte[] pngBytes,
            string skinName,
            string texturesDir
        )
        {
            try
            {
                using MemoryStream ms = new(pngBytes);
                BitmapDecoder decoder = BitmapDecoder.Create(
                    ms,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad
                );
                if (decoder.Frames.Count == 0)
                {
                    return pngBytes;
                }

                BitmapSource frame = decoder.Frames[0];
                if (frame.PixelWidth < 3 || frame.PixelHeight < 1)
                {
                    return pngBytes;
                }

                BitmapSource bgra =
                    frame.Format == PixelFormats.Bgra32
                        ? frame
                        : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);

                byte[] topRow = new byte[12];
                bgra.CopyPixels(new Int32Rect(0, 0, 3, 1), topRow, 12, 0);

                if (topRow[2] == 0x54 && topRow[1] == 0x4F && topRow[0] == 0x53)
                {
                    return pngBytes;
                }

                string jsonPath = FileHelper.GetSafePath(texturesDir, $"{skinName}.json");
                if (!File.Exists(jsonPath))
                {
                    return pngBytes;
                }

                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(jsonPath));
                JsonElement root = doc.RootElement;
                int frameWidth = root.TryGetProperty("frameWidth", out JsonElement fwEl)
                    ? fwEl.GetInt32()
                    : 128;
                int frameHeight = root.TryGetProperty("frameHeight", out JsonElement fhEl)
                    ? fhEl.GetInt32()
                    : 128;
                int fps = root.TryGetProperty("fps", out JsonElement fpsEl)
                    ? Math.Clamp((int)Math.Round(fpsEl.GetDouble()), 1, 255)
                    : 24;
                int cols = Math.Clamp(bgra.PixelWidth / Math.Max(1, frameWidth), 1, 255);
                int rows = Math.Clamp(bgra.PixelHeight / Math.Max(1, frameHeight), 1, 255);
                int numFrames = root.TryGetProperty("numFrames", out JsonElement nfEl)
                    ? Math.Clamp(nfEl.GetInt32(), 1, 65535)
                    : cols * rows;

                WriteableBitmap wb = new(bgra);
                byte[] headerPixels =
                [
                    0x53,
                    0x4F,
                    0x54,
                    0xFF,
                    (byte)fps,
                    (byte)rows,
                    (byte)cols,
                    0xFF,
                    0x00,
                    (byte)(numFrames & 0xFF),
                    (byte)((numFrames >> 8) & 0xFF),
                    0xFF,
                ];
                wb.WritePixels(new Int32Rect(0, 0, 3, 1), headerPixels, 12, 0);

                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(wb));
                using MemoryStream outMs = new();
                encoder.Save(outMs);
                return outMs.ToArray();
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "Failed to stamp TOS header on animated paint {SkinName}",
                    skinName
                );
                return pngBytes;
            }
        }

        private ISettingsService? ResolveSettingsService()
        {
            return _settingsService
                ?? App.ServiceProvider?.GetService(typeof(ISettingsService)) as ISettingsService;
        }

        private void UpdateHtml5ShotLightingOnSwap(
            string cachePath,
            ShotEffectModel shotEffect,
            IReadOnlyList<Html5ResourceTarget> swappedTargets
        )
        {
            if (
                !ShotLightingColorHelper.TryMapTurretToHtml5Kind(
                    shotEffect.Turret,
                    out string html5Kind
                )
            )
            {
                return;
            }

            ISettingsService? svc = ResolveSettingsService();
            if (svc == null)
            {
                return;
            }

            Html5TurretLightOverride? lightOverride = ShotLightingColorHelper.ResolveOverride(
                shotEffect
            );
            if (lightOverride == null)
            {
                return;
            }

            lightOverride.ResourceIds = [.. swappedTargets.Select(t => t.ResourceId).Distinct()];

            AppSettings settings = svc.Load();
            settings.ActiveHtml5ShotLighting[html5Kind] = lightOverride;
            svc.Save(settings);

            _html5CacheService.SyncShotDynamicLighting(cachePath);
        }

        private void ClearHtml5ShotLightingOnZipRestore(
            string cachePath,
            string zipStem,
            HashSet<long> restoredResourceIds
        )
        {
            ISettingsService? svc = ResolveSettingsService();
            if (svc == null)
            {
                return;
            }

            AppSettings settings = svc.Load();
            if (settings.ActiveHtml5ShotLighting.Count == 0)
            {
                return;
            }

            bool removedAny = false;
            int us = zipStem.IndexOf('_');
            string? stemKind =
                us > 0
                && ShotLightingColorHelper.TryMapTurretToHtml5Kind(
                    zipStem[..us],
                    out string parsedKind
                )
                    ? parsedKind
                    : null;

            foreach (string kind in settings.ActiveHtml5ShotLighting.Keys.ToList())
            {
                Html5TurretLightOverride ov = settings.ActiveHtml5ShotLighting[kind];
                bool matchesByResource =
                    ov.ResourceIds.Count > 0 && restoredResourceIds.Any(ov.ResourceIds.Contains);
                bool matchesByNameFallback =
                    ov.ResourceIds.Count == 0
                    && string.Equals(kind, stemKind, StringComparison.OrdinalIgnoreCase);

                if (matchesByResource || matchesByNameFallback)
                {
                    _ = settings.ActiveHtml5ShotLighting.Remove(kind);
                    removedAny = true;
                }
            }

            if (removedAny)
            {
                svc.Save(settings);
                _html5CacheService.SyncShotDynamicLighting(cachePath);
            }
        }

        private void ClearAllHtml5ShotLighting(string cachePath)
        {
            ISettingsService? svc = ResolveSettingsService();
            if (svc != null)
            {
                AppSettings settings = svc.Load();
                if (settings.ActiveHtml5ShotLighting.Count > 0)
                {
                    settings.ActiveHtml5ShotLighting.Clear();
                    svc.Save(settings);
                }
            }

            _html5CacheService.SyncShotDynamicLighting(cachePath);
        }
    }
}
