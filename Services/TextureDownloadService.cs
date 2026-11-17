using Serilog;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class TextureDownloadService(ISettingsService? settingsService = null) : ITextureDownloadService
    {
        private static readonly HttpClient _httpClient;
        private const int StreamBufferSize = 131072;
        private readonly ISettingsService? _settingsService = settingsService;

        static TextureDownloadService()
        {
            SocketsHttpHandler handler = new()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectTimeout = TimeSpan.FromSeconds(20),
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                EnableMultipleHttp2Connections = true,
            };

            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(15) };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProTankiTextureSwapper");
        }

        public static void CleanupOrphanedTempFiles()
        {
            try
            {
                string tempDir = Path.GetTempPath();
                foreach (string file in Directory.EnumerateFiles(tempDir, "textures_*.*"))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (
                        ext == ".zip"
                        || ext == ".part"
                        || ext == ".tmp"
                        || file.EndsWith(".zip.part", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch
                        {
                            Log.Error(
                                "Failed to delete orphaned temporary textures file: {Path}",
                                file
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to clean orphaned textures temp files.");
            }
        }

        public bool AreTexturesInstalled()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string versionFile = Path.Combine(
                    baseDir,
                    "Textures",
                    Constants.TexturesVersionFileName
                );
                if (!File.Exists(versionFile))
                {
                    return false;
                }

                string versionContent = File.ReadAllText(versionFile);
                if (
                    !versionContent.Contains(
                        $"Version={Constants.RequiredTexturesPackVersion}",
                        StringComparison.Ordinal
                    )
                )
                {
                    return false;
                }

                string hullsDir = Path.Combine(baseDir, "Textures", "Hulls");
                string turretsDir = Path.Combine(baseDir, "Textures", "Turrets");
                string shotEffectsDir = Path.Combine(baseDir, "Textures", "ShotEffects");
                string paintsDir = Path.Combine(baseDir, "Textures", "Paints");
                string animatedPaintsDir = Path.Combine(
                    baseDir,
                    "Textures",
                    "Encrypted",
                    "Paints",
                    "Animated"
                );

                if (
                    !Directory.Exists(hullsDir)
                    || !Directory.Exists(turretsDir)
                    || !Directory.Exists(shotEffectsDir)
                    || !Directory.Exists(paintsDir)
                    || !Directory.Exists(animatedPaintsDir)
                )
                {
                    return false;
                }

                bool hasHulls = Directory
                    .EnumerateFiles(hullsDir, "*.*", SearchOption.AllDirectories)
                    .Any();
                bool hasTurrets = Directory
                    .EnumerateFiles(turretsDir, "*.*", SearchOption.AllDirectories)
                    .Any();
                bool hasShotEffects = Directory
                    .EnumerateFiles(shotEffectsDir, "*.*", SearchOption.AllDirectories)
                    .Any();
                bool hasPaints = Directory
                    .EnumerateFiles(paintsDir, "*.*", SearchOption.AllDirectories)
                    .Any();
                bool hasAnimatedPaints = Directory
                    .EnumerateFiles(animatedPaintsDir, "*.*", SearchOption.AllDirectories)
                    .Any();

                return hasHulls && hasTurrets && hasShotEffects && hasPaints && hasAnimatedPaints;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to verify existing textures directory status.");
                return false;
            }
        }

        public async Task<bool> DownloadAndExtractTexturesAsync(
            Action<string, double>? onProgress = null,
            CancellationToken cancellationToken = default
        )
        {
            CleanupOrphanedTempFiles();

            string tempZipPath = Path.Combine(
                Path.GetTempPath(),
                $"textures_{Guid.NewGuid():N}.zip"
            );
            string tempPartPath = tempZipPath + ".part";
            bool isLocalFileOverride = false;

            try
            {
                string? envOverride = Environment.GetEnvironmentVariable("TEXTURES_DOWNLOAD_URL");
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

#if DEBUG
                string localFallbackZip = Path.Combine(baseDir, Constants.TexturesZipFileName);
                string projectOutputZip = Path.GetFullPath(
                    Path.Combine(baseDir, "..", "..", "..", "Output", Constants.TexturesZipFileName)
                );

                if (!string.IsNullOrEmpty(envOverride) && File.Exists(envOverride))
                {
                    Log.Information("Using local file override for textures: {Path}", envOverride);
                    tempZipPath = envOverride;
                    isLocalFileOverride = true;
                }
                else if (File.Exists(localFallbackZip))
                {
                    Log.Information(
                        "Found local textures.zip in application directory: {Path}",
                        localFallbackZip
                    );
                    tempZipPath = localFallbackZip;
                    isLocalFileOverride = true;
                }
                else if (File.Exists(projectOutputZip) && string.IsNullOrEmpty(envOverride))
                {
                    Log.Information(
                        "Auto-detected Output/textures.zip in project directory: {Path}",
                        projectOutputZip
                    );
                    tempZipPath = projectOutputZip;
                    isLocalFileOverride = true;
                }
#else
                string localFallbackZip = Path.Combine(baseDir, Constants.TexturesZipFileName);
                if (!string.IsNullOrEmpty(envOverride) && File.Exists(envOverride))
                {
                    Log.Information("Using local file override for textures: {Path}", envOverride);
                    tempZipPath = envOverride;
                    isLocalFileOverride = true;
                }
                else if (File.Exists(localFallbackZip))
                {
                    Log.Information(
                        "Found local textures.zip in application directory: {Path}",
                        localFallbackZip
                    );
                    tempZipPath = localFallbackZip;
                    isLocalFileOverride = true;
                }
#endif

                if (!isLocalFileOverride)
                {
                    string preferredMirror = _settingsService?.Load()?.PreferredTextureMirror ?? "Cloudflare";
                    string[] candidateMirrors = !string.IsNullOrEmpty(envOverride)
                        ? [envOverride]
                        : string.Equals(preferredMirror, "GitHub", StringComparison.OrdinalIgnoreCase)
                            ? [
                            Constants.TexturesLatestDownloadUrl,
                            Constants.TexturesWorkerDownloadUrl,
                        ]
                            : Constants.TexturesDownloadMirrors;
                    bool downloadSucceeded = false;
                    Exception? lastException = null;

                    for (int mirrorIndex = 0; mirrorIndex < candidateMirrors.Length; mirrorIndex++)
                    {
                        string downloadUrl = candidateMirrors[mirrorIndex];
                        const int maxAttemptsPerMirror = 2;

                        for (int attempt = 1; attempt <= maxAttemptsPerMirror; attempt++)
                        {
                            try
                            {
                                onProgress?.Invoke("Connecting to download server...", 0);
                                Log.Information(
                                    "Downloading textures (Mirror {MirrorIndex}/{TotalMirrors}, Attempt {Attempt}/{MaxAttempts}) from {Url}",
                                    mirrorIndex + 1,
                                    candidateMirrors.Length,
                                    attempt,
                                    maxAttemptsPerMirror,
                                    downloadUrl
                                );

                                using HttpResponseMessage response = await _httpClient.GetAsync(
                                    downloadUrl,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    cancellationToken
                                );

                                if (
                                    response.StatusCode == HttpStatusCode.NotFound
                                    && downloadUrl == Constants.TexturesLatestDownloadUrl
                                )
                                {
                                    Log.Warning(
                                        "Latest release download URL returned 404. Checking GitHub releases for latest textures.zip asset..."
                                    );
                                    string? resolvedUrl = await TryResolveReleaseAssetUrlAsync(
                                        cancellationToken
                                    );
                                    if (
                                        !string.IsNullOrEmpty(resolvedUrl)
                                        && resolvedUrl != downloadUrl
                                    )
                                    {
                                        downloadUrl = resolvedUrl;
                                        Log.Information(
                                            "Retrying download with resolved release URL: {Url}",
                                            downloadUrl
                                        );
                                        attempt--;
                                        continue;
                                    }
                                }

                                _ = response.EnsureSuccessStatusCode();
                                long totalBytes = response.Content.Headers.ContentRange?.Length
                                    ?? response.Content.Headers.ContentLength
                                    ?? -1L;

                                using Stream contentStream = await response.Content.ReadAsStreamAsync(
                                    cancellationToken
                                );
                                using (
                                    FileStream fileStream = new(
                                        tempPartPath,
                                        FileMode.Create,
                                        FileAccess.Write,
                                        FileShare.None,
                                        StreamBufferSize,
                                        FileOptions.Asynchronous | FileOptions.SequentialScan
                                    )
                                )
                                {
                                    byte[] buffer = new byte[StreamBufferSize];
                                    long totalRead = 0L;
                                    int read;
                                    long lastReportedBytes = 0L;
                                    Stopwatch speedStopwatch = Stopwatch.StartNew();

                                    while (
                                        (
                                            read = await contentStream.ReadAsync(
                                                buffer.AsMemory(0, buffer.Length),
                                                cancellationToken
                                            )
                                        ) > 0
                                    )
                                    {
                                        await fileStream.WriteAsync(
                                            buffer.AsMemory(0, read),
                                            cancellationToken
                                        );
                                        totalRead += read;

                                        if (
                                            speedStopwatch.ElapsedMilliseconds >= 150
                                            || totalRead == totalBytes
                                        )
                                        {
                                            double elapsedSec = speedStopwatch.Elapsed.TotalSeconds;
                                            double speedMbps =
                                                elapsedSec > 0
                                                    ? (totalRead - lastReportedBytes)
                                                        / (1024.0 * 1024.0)
                                                        / elapsedSec
                                                    : 0;

                                            if (totalBytes > 0)
                                            {
                                                double percent = (double)totalRead / totalBytes * 100.0;
                                                double currentMb = totalRead / (1024.0 * 1024.0);
                                                double totalMb = totalBytes / (1024.0 * 1024.0);
                                                onProgress?.Invoke(
                                                    $"Downloading textures... {percent:F0}% ({currentMb:F1} / {totalMb:F1} MB - {speedMbps:F1} MB/s)",
                                                    percent
                                                );
                                            }
                                            else
                                            {
                                                double mbRead = totalRead / (1024.0 * 1024.0);
                                                onProgress?.Invoke(
                                                    $"Downloading textures... ({mbRead:F1} MB - {speedMbps:F1} MB/s)",
                                                    0
                                                );
                                            }

                                            lastReportedBytes = totalRead;
                                            speedStopwatch.Restart();
                                        }
                                    }

                                    if (totalBytes > 0 && totalRead < totalBytes)
                                    {
                                        throw new IOException(
                                            $"Download was incomplete: received {totalRead} of {totalBytes} bytes."
                                        );
                                    }
                                }

                                if (File.Exists(tempZipPath))
                                {
                                    try
                                    {
                                        File.Delete(tempZipPath);
                                    }
                                    catch { }
                                }
                                File.Move(tempPartPath, tempZipPath);

                                downloadSucceeded = true;
                                break;
                            }
                            catch (Exception ex)
                                when (attempt < maxAttemptsPerMirror
                                    && !cancellationToken.IsCancellationRequested
                                )
                            {
                                lastException = ex;
                                int delaySeconds = (int)Math.Pow(2, attempt - 1);
                                Log.Warning(
                                    ex,
                                    "Download attempt {Attempt} from mirror {Url} failed. Retrying in {Delay}s...",
                                    attempt,
                                    downloadUrl,
                                    delaySeconds
                                );
                                onProgress?.Invoke(
                                    $"Connection interrupted. Retrying in {delaySeconds}s...",
                                    0
                                );

                                if (File.Exists(tempPartPath))
                                {
                                    try
                                    {
                                        File.Delete(tempPartPath);
                                    }
                                    catch { }
                                }
                                if (File.Exists(tempZipPath))
                                {
                                    try
                                    {
                                        File.Delete(tempZipPath);
                                    }
                                    catch { }
                                }

                                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                            }
                            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                            {
                                lastException = ex;
                                Log.Warning(
                                    ex,
                                    "Mirror {Url} failed. Falling back to next mirror...",
                                    downloadUrl
                                );

                                if (File.Exists(tempPartPath))
                                {
                                    try
                                    {
                                        File.Delete(tempPartPath);
                                    }
                                    catch { }
                                }
                                if (File.Exists(tempZipPath))
                                {
                                    try
                                    {
                                        File.Delete(tempZipPath);
                                    }
                                    catch { }
                                }
                                break;
                            }
                        }

                        if (downloadSucceeded)
                        {
                            break;
                        }
                    }

                    if (!downloadSucceeded)
                    {
                        throw new HttpRequestException(
                            "Failed to download textures from all configured mirrors.",
                            lastException
                        );
                    }
                }

                onProgress?.Invoke("Verifying texture pack...", 100);
                Log.Information("Validating archive integrity: {ZipPath}", tempZipPath);

                await Task.Run(
                    () =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        using ZipArchive verifyArchive = ZipFile.OpenRead(tempZipPath);
                        if (verifyArchive.Entries.Count == 0)
                        {
                            throw new InvalidDataException(
                                "Downloaded texture pack archive is empty."
                            );
                        }
                    },
                    cancellationToken
                );

                onProgress?.Invoke("Extracting textures...", 0);
                Log.Information("Starting extraction from {ZipPath}", tempZipPath);

                await Task.Run(
                    () =>
                    {
                        using ZipArchive archive = ZipFile.OpenRead(tempZipPath);

                        string texturesFolder = Path.Combine(baseDir, "Textures");
                        string safeTargetRoot = Path.GetFullPath(texturesFolder);
                        if (!safeTargetRoot.EndsWith(Path.DirectorySeparatorChar))
                        {
                            safeTargetRoot += Path.DirectorySeparatorChar;
                        }

                        string versionFile = Path.Combine(
                            safeTargetRoot,
                            Constants.TexturesVersionFileName
                        );

                        if (Directory.Exists(safeTargetRoot) && !File.Exists(versionFile))
                        {
                            Log.Warning(
                                "Previous incomplete or unversioned Textures directory detected. Cleaning up before fresh extraction..."
                            );
                            try
                            {
                                Directory.Delete(safeTargetRoot, recursive: true);
                            }
                            catch (Exception ex)
                            {
                                Log.Warning(ex, "Failed to fully purge partial Textures directory.");
                            }
                        }

                        if (File.Exists(versionFile))
                        {
                            File.Delete(versionFile);
                        }

                        Directory.CreateDirectory(safeTargetRoot);

                        int totalEntries = archive.Entries.Count;
                        int processedEntries = 0;
                        int failedEntries = 0;

                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string entryPath = entry.FullName.Replace('\\', '/').TrimStart('/');

                            if (
                                entryPath.StartsWith(
                                    "Textures/",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                entryPath = entryPath["Textures/".Length..];
                            }

                            if (string.IsNullOrWhiteSpace(entryPath))
                            {
                                continue;
                            }

                            string destPath = Path.GetFullPath(
                                Path.Combine(safeTargetRoot, entryPath)
                            );

                            if (
                                !destPath.StartsWith(
                                    safeTargetRoot,
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                Log.Warning(
                                    "Zip Slip attempt detected and blocked: {Entry}",
                                    entry.FullName
                                );
                                continue;
                            }

                            if (string.IsNullOrEmpty(entry.Name) || entryPath.EndsWith('/'))
                            {
                                _ = Directory.CreateDirectory(destPath);
                                continue;
                            }

                            string? parentDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                _ = Directory.CreateDirectory(parentDir);
                            }

                            if (File.Exists(destPath))
                            {
                                try
                                {
                                    File.SetAttributes(destPath, FileAttributes.Normal);
                                }
                                catch { }
                            }

                            try
                            {
                                entry.ExtractToFile(destPath, overwrite: true);
                            }
                            catch (Exception ex)
                            {
                                failedEntries++;
                                Log.Warning(
                                    ex,
                                    "Failed to extract '{Entry}' to '{Dest}'.",
                                    entry.FullName,
                                    destPath
                                );
                                continue;
                            }

                            processedEntries++;
                            if (processedEntries % 25 == 0 || processedEntries == totalEntries)
                            {
                                double percent =
                                    totalEntries > 0
                                        ? (double)processedEntries / totalEntries * 100.0
                                        : 100.0;
                                onProgress?.Invoke(
                                    $"Extracting textures... {percent:F0}% ({processedEntries}/{totalEntries})",
                                    percent
                                );
                            }
                        }

                        if (failedEntries > 0 || (totalEntries > 0 && processedEntries == 0))
                        {
                            throw new IOException(
                                $"Texture archive extraction incomplete: {failedEntries} entry/entries failed."
                            );
                        }

                        File.WriteAllText(
                            versionFile,
                            $"Version={Constants.RequiredTexturesPackVersion}\nInstalledAt={DateTime.UtcNow:O}"
                        );
                    },
                    cancellationToken
                );

                Log.Information("Textures extraction completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to download and extract texture pack.");
                return false;
            }
            finally
            {
                if (!isLocalFileOverride)
                {
                    if (File.Exists(tempPartPath))
                    {
                        try
                        {
                            File.Delete(tempPartPath);
                        }
                        catch { }
                    }

                    if (File.Exists(tempZipPath))
                    {
                        try
                        {
                            File.Delete(tempZipPath);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(
                                ex,
                                "Failed to delete temporary textures zip file: {Path}",
                                tempZipPath
                            );
                        }
                    }
                }
            }
        }

        private static async Task<string?> TryResolveReleaseAssetUrlAsync(
            CancellationToken cancellationToken
        )
        {
            try
            {
                using HttpResponseMessage response = await _httpClient.GetAsync(
                    $"{Constants.GitHubApiUrl}/releases",
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken
                );

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                List<GitHubReleaseModel>? releases = await JsonSerializer.DeserializeAsync<
                    List<GitHubReleaseModel>
                >(stream, cancellationToken: cancellationToken);

                if (releases != null)
                {
                    foreach (GitHubReleaseModel release in releases)
                    {
                        GitHubAssetModel? asset = release.Assets.FirstOrDefault(a =>
                            a.Name.Equals(
                                Constants.TexturesZipFileName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        );

                        if (asset != null && !string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                        {
                            Log.Information(
                                "Found textures.zip in release {Tag}: {Url}",
                                release.TagName,
                                asset.BrowserDownloadUrl
                            );
                            return asset.BrowserDownloadUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to resolve textures.zip via GitHub releases API.");
            }

            return null;
        }
    }
}
