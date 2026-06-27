using Serilog;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;

namespace TextureSwapper.Services
{
    public class UpdateService
    {
        private static readonly HttpClient _httpClient;

        static UpdateService()
        {
            SocketsHttpHandler handler = new()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProTankiTextureSwapper");
        }

        public async Task<HttpResponseMessage> GetWithRetryAsync(string url, int maxRetries = 3)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    HttpResponseMessage response = await _httpClient.GetAsync(url);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        throw new HttpRequestException($"Resource not found (404): {url}", null, HttpStatusCode.NotFound);
                    }
                    _ = response.EnsureSuccessStatusCode();
                    return response;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Warning($"Resource not found (404): {url}", null, HttpStatusCode.NotFound);
                }
                catch (HttpRequestException ex) when (i < maxRetries - 1)
                {
                    Log.Warning("Request failed, retrying in {Delay}s. Error: {Message}", Math.Pow(2, i), ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                }
            }
            throw new HttpRequestException("Max retries exceeded.");
        }

        public async Task<(List<SkinModel>? Skins, string? RawJson)> FetchRemoteSkinsFileAsync(string fileName)
        {
            try
            {
                Log.Information("Fetching remote {FileName} from GitHub...", fileName);
                string url = $"{Constants.GitHubRawUrl}/{fileName}?t={DateTime.Now.Ticks}";

                HttpResponseMessage response = await GetWithRetryAsync(url);
                string json = await response.Content.ReadAsStringAsync();
                List<SkinModel>? skins = JsonSerializer.Deserialize<List<SkinModel>>(json);

                return (skins, json);
            }
            catch (Exception ex)
            {
                Log.Warning("Network error while fetching remote skins ({FileName}): {Message}", fileName, ex.Message);
                return (null, null);
            }
        }

        public async Task EnsureAssetsExistAsync(SkinModel skin, Action<string>? onProgress = null)
        {
            try
            {
                if (skin.Category.Equals("Paints", StringComparison.OrdinalIgnoreCase))
                {
                    await EnsureFileExistsAsync(string.Empty, skin.Name, skin.SourceFolder, onProgress);
                    return;
                }

                await EnsureFileExistsAsync(skin.PreviewImage, "Preview", skin.SourceFolder, onProgress);

                List<string> prefixes = ["details"];
                if (!skin.Category.Equals("Supplies", StringComparison.OrdinalIgnoreCase))
                {
                    prefixes.Add("lightmap");
                    prefixes.Add("alpha");
                }

                foreach (string prefix in prefixes)
                {
                    await EnsureFileExistsAsync(string.Empty, prefix, skin.SourceFolder, onProgress);
                }

                if (!string.IsNullOrEmpty(skin.ModelTarget))
                {
                    string modelPath = Path.Combine(skin.SourceFolder, "object.3ds").Replace("\\", "/");
                    await EnsureFileExistsAsync(modelPath, "object", skin.SourceFolder, onProgress);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to ensure assets exist for {SkinName}: {Message}", skin.Name, ex.Message);
            }
        }

        private async Task EnsureFileExistsAsync(string exactRelativePath, string filePrefix, string sourceFolder, Action<string>? onProgress = null)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localFolder = FileHelper.GetSafePath(baseDir, sourceFolder.Replace("\\", "/"));

            string[] validExtensions = [".png", ".jpg", ".jpeg"];

            if (Directory.Exists(localFolder))
            {
                string[] localFiles = Directory.GetFiles(localFolder, $"{filePrefix}.*");
                foreach (string file in localFiles)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (validExtensions.Contains(ext))
                    {
                        Log.Debug("Asset already exists locally: {Path}", file);
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(exactRelativePath))
            {
                string exactLocalPath = FileHelper.GetSafePath(baseDir, exactRelativePath.Replace("\\", "/"));
                if (File.Exists(exactLocalPath))
                {
                    Log.Debug("Asset already exists locally: {Path}", exactLocalPath);
                    return;
                }

                string ext = Path.GetExtension(exactLocalPath).ToLower();
                string[] altExts = [".png", ".jpg", ".jpeg"];
                if (altExts.Contains(ext))
                {
                    foreach (string alt in altExts)
                    {
                        if (alt == ext)
                        {
                            continue;
                        }

                        string altPath = Path.ChangeExtension(exactLocalPath, alt);
                        if (File.Exists(altPath))
                        {
                            Log.Debug("Asset already exists locally with alternative extension: {Path}", altPath);
                            return;
                        }
                    }
                }
            }

            string fileNameWithDefaultExtension = string.Empty;
            string normalizedRelativePath = string.Empty;
            string downloadLocalPath = string.Empty;
            byte[]? data = null;

            if (!string.IsNullOrEmpty(exactRelativePath))
            {
                fileNameWithDefaultExtension = Path.GetFileName(exactRelativePath);
                normalizedRelativePath = exactRelativePath.Replace("\\", "/");
                downloadLocalPath = FileHelper.GetSafePath(baseDir, normalizedRelativePath);

                Log.Information("Asset missing. Starting download: {Path}", downloadLocalPath);
                string url = $"{Constants.GitHubRawUrl}/{normalizedRelativePath}";
                onProgress?.Invoke($"Downloading {fileNameWithDefaultExtension}...");

                _ = Directory.CreateDirectory(Path.GetDirectoryName(downloadLocalPath)!);
                HttpResponseMessage response = await GetWithRetryAsync(url);
                data = await response.Content.ReadAsByteArrayAsync();
            }
            else
            {
                string[] extensionsToTry = [".png", ".jpg", ".jpeg"];
                foreach (string ext in extensionsToTry)
                {
                    fileNameWithDefaultExtension = $"{filePrefix}{ext}";
                    normalizedRelativePath = Path.Combine(sourceFolder, fileNameWithDefaultExtension).Replace("\\", "/");
                    downloadLocalPath = FileHelper.GetSafePath(baseDir, normalizedRelativePath);
                    string url = $"{Constants.GitHubRawUrl}/{normalizedRelativePath}";

                    try
                    {
                        Log.Information("Trying download: {Path}", downloadLocalPath);
                        HttpResponseMessage response = await GetWithRetryAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            data = await response.Content.ReadAsByteArrayAsync();
                            break;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        Log.Warning($"Resource not found (404): {url}", null, HttpStatusCode.NotFound);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("Non-404 error trying to download {Path}: {Message}", downloadLocalPath, ex.Message);
                    }
                }

                if (data == null)
                {
                    throw new FileNotFoundException($"Could not find remote asset with prefix '{filePrefix}' in folder '{sourceFolder}' on GitHub.");
                }
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(downloadLocalPath)!);
            await VerifyAndSaveFileAsync(data, downloadLocalPath);
            Log.Information("Asset downloaded and saved: {Path}", downloadLocalPath);
        }

        private async Task VerifyAndSaveFileAsync(byte[] data, string targetPath, string? expectedSha256 = null)
        {
            byte[] hashBytes = SHA256.HashData(data);
            string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (!string.IsNullOrEmpty(expectedSha256) && !computedHash.Equals(expectedSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                Log.Error("Hash mismatch for {Path}. Expected: {Expected}, Computed: {Computed}", targetPath, expectedSha256, computedHash);
                throw new CryptographicException($"Hash mismatch for {targetPath}. File compromised.");
            }

            await File.WriteAllBytesAsync(targetPath, data);
        }

        public async Task<(List<InGamePaintModel>? Paints, string? RawJson)> FetchRemoteInGamePaintsFileAsync(string fileName)
        {
            try
            {
                Log.Information("Fetching remote {FileName} from GitHub...", fileName);
                string url = $"{Constants.GitHubRawUrl}/{fileName}?t={DateTime.Now.Ticks}";

                HttpResponseMessage response = await GetWithRetryAsync(url);
                string json = await response.Content.ReadAsStringAsync();
                List<InGamePaintModel>? paints = JsonSerializer.Deserialize<List<InGamePaintModel>>(json);

                return (paints, json);
            }
            catch (Exception ex)
            {
                Log.Warning("Network error while fetching remote in-game paints ({FileName}): {Message}", fileName, ex.Message);
                return (null, null);
            }
        }

        public async Task<GitHubReleaseModel?> CheckForAppUpdatesAsync()
        {
            try
            {
                Log.Information("Checking for app updates on GitHub...");
                string url = $"{Constants.GitHubApiUrl}/releases/latest";

                HttpResponseMessage response = await GetWithRetryAsync(url);
                return await response.Content.ReadFromJsonAsync<GitHubReleaseModel>();
            }
            catch (Exception ex)
            {
                Log.Error("Network error while checking for app updates: {Message}", ex.Message);
                return null;
            }
        }

        public async Task DownloadAndRunInstallerAsync(string downloadUrl, Action<double>? onProgress = null)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "TextureSwapper_Setup.exe");

            Log.Information("Downloading installer from {Url} to {Path}", downloadUrl, tempPath);

            using (HttpResponseMessage response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                _ = response.EnsureSuccessStatusCode();
                long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using Stream contentStream = await response.Content.ReadAsStreamAsync();
                using FileStream fileStream = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                byte[] buffer = new byte[8192];
                long totalRead = 0L;
                int read;

                while ((read = await contentStream.ReadAsync(buffer)) != 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (totalBytes != -1)
                    {
                        onProgress?.Invoke((double)totalRead / totalBytes * 100);
                    }
                }
            }

            Log.Information("Launching installer: {Path}", tempPath);
            _ = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
            Environment.Exit(0);
        }
    }
}
