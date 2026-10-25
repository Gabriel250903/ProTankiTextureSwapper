using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class TextureDownloadService() : ITextureDownloadService
    {
        private static readonly HttpClient _httpClient;
        private const int StreamBufferSize = 131072;

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

        public bool AreTexturesInstalled()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string hullsDir = Path.Combine(baseDir, "Textures", "Hulls");
                string turretsDir = Path.Combine(baseDir, "Textures", "Turrets");

                if (!Directory.Exists(hullsDir) || !Directory.Exists(turretsDir))
                {
                    return false;
                }

                bool hasHulls = Directory
                    .EnumerateFiles(hullsDir, "*.*", SearchOption.AllDirectories)
                    .Any();
                bool hasTurrets = Directory
                    .EnumerateFiles(turretsDir, "*.*", SearchOption.AllDirectories)
                    .Any();

                return hasHulls && hasTurrets;
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
            string tempZipPath = Path.Combine(
                Path.GetTempPath(),
                $"textures_{Guid.NewGuid():N}.zip"
            );
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
                    string downloadUrl = !string.IsNullOrEmpty(envOverride)
                        ? envOverride
                        : Constants.TexturesLatestDownloadUrl;

                    const int maxAttempts = 3;
                    bool downloadSucceeded = false;

                    for (int attempt = 1; attempt <= maxAttempts; attempt++)
                    {
                        try
                        {
                            onProgress?.Invoke("Connecting to download server...", 0);
                            Log.Information(
                                "Downloading textures (Attempt {Attempt}/{MaxAttempts}) from {Url}",
                                attempt,
                                maxAttempts,
                                downloadUrl
                            );

                            using HttpResponseMessage response = await _httpClient.GetAsync(
                                downloadUrl,
                                HttpCompletionOption.ResponseHeadersRead,
                                cancellationToken
                            );

                            // If GitHub latest release endpoint returns 404, query releases to find the newest release with textures.zip
                            if (response.StatusCode == HttpStatusCode.NotFound
                                && downloadUrl == Constants.TexturesLatestDownloadUrl)
                            {
                                Log.Warning(
                                    "Latest release download URL returned 404. Checking GitHub releases for latest textures.zip asset..."
                                );
                                string? resolvedUrl = await TryResolveReleaseAssetUrlAsync(cancellationToken);
                                if (!string.IsNullOrEmpty(resolvedUrl) && resolvedUrl != downloadUrl)
                                {
                                    downloadUrl = resolvedUrl;
                                    Log.Information("Retrying download with resolved release URL: {Url}", downloadUrl);
                                    attempt--;
                                    continue;
                                }
                            }

                            _ = response.EnsureSuccessStatusCode();
                            long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                            using Stream contentStream = await response.Content.ReadAsStreamAsync(
                                cancellationToken
                            );
                            using FileStream fileStream = new(
                                tempZipPath,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None,
                                StreamBufferSize,
                                FileOptions.Asynchronous | FileOptions.SequentialScan
                            );

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

                            downloadSucceeded = true;
                            break;
                        }
                        catch (Exception ex)
                            when (attempt < maxAttempts
                                && !cancellationToken.IsCancellationRequested
                            )
                        {
                            int delaySeconds = (int)Math.Pow(2, attempt - 1);
                            Log.Warning(
                                ex,
                                "Download attempt {Attempt} failed. Retrying in {Delay}s...",
                                attempt,
                                delaySeconds
                            );
                            onProgress?.Invoke(
                                $"Connection interrupted. Retrying in {delaySeconds}s (attempt {attempt}/{maxAttempts})...",
                                0
                            );

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
                    }

                    if (!downloadSucceeded)
                    {
                        throw new HttpRequestException(
                            $"Failed to download textures after {maxAttempts} attempts."
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

                        Directory.CreateDirectory(safeTargetRoot);

                        int totalEntries = archive.Entries.Count;
                        int processedEntries = 0;

                        foreach (ZipArchiveEntry entry in archive.Entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // 1. Normalize slashes and trim leading slashes to prevent Path.Combine drive-root jump
                            string entryPath = entry.FullName.Replace('\\', '/').TrimStart('/');

                            // 2. Strip redundant top-level 'Textures/' folder if present
                            if (entryPath.StartsWith("Textures/", StringComparison.OrdinalIgnoreCase))
                            {
                                entryPath = entryPath.Substring("Textures/".Length);
                            }

                            if (string.IsNullOrWhiteSpace(entryPath))
                            {
                                continue;
                            }

                            // 3. Resolve destination strictly inside safeTargetRoot (baseDir/Textures/)
                            string destPath = Path.GetFullPath(
                                Path.Combine(safeTargetRoot, entryPath)
                            );

                            // Zip Slip protection - guarantee files never escape safeTargetRoot
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

                            // Directory entry in ZIP
                            if (string.IsNullOrEmpty(entry.Name) || entryPath.EndsWith('/'))
                            {
                                _ = Directory.CreateDirectory(destPath);
                                continue;
                            }

                            // Ensure parent directory exists
                            string? parentDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                _ = Directory.CreateDirectory(parentDir);
                            }

                            // Clear ReadOnly attribute if file already exists to avoid UnauthorizedAccessException
                            if (File.Exists(destPath))
                            {
                                try
                                {
                                    File.SetAttributes(destPath, FileAttributes.Normal);
                                }
                                catch { }
                            }

                            entry.ExtractToFile(destPath, overwrite: true);

                            processedEntries++;
                            if (processedEntries % 25 == 0 || processedEntries == totalEntries)
                            {
                                double percent = totalEntries > 0
                                    ? (double)processedEntries / totalEntries * 100.0
                                    : 100.0;
                                onProgress?.Invoke(
                                    $"Extracting textures... {percent:F0}% ({processedEntries}/{totalEntries})",
                                    percent
                                );
                            }
                        }

                        string versionFile = Path.Combine(
                            safeTargetRoot,
                            Constants.TexturesVersionFileName
                        );
                        File.WriteAllText(versionFile, $"InstalledAt={DateTime.UtcNow:O}");
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
                if (!isLocalFileOverride && File.Exists(tempZipPath))
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

        private static async Task<string?> TryResolveReleaseAssetUrlAsync(CancellationToken cancellationToken)
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
                List<GitHubReleaseModel>? releases = await JsonSerializer.DeserializeAsync<List<GitHubReleaseModel>>(
                    stream,
                    cancellationToken: cancellationToken
                );

                if (releases != null)
                {
                    foreach (GitHubReleaseModel release in releases)
                    {
                        GitHubAssetModel? asset = release.Assets.FirstOrDefault(a =>
                            a.Name.Equals(Constants.TexturesZipFileName, StringComparison.OrdinalIgnoreCase)
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
