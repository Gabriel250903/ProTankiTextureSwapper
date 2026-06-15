using Serilog;
using System.IO;
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
                    _ = response.EnsureSuccessStatusCode();
                    return response;
                }
                catch (HttpRequestException ex) when (i < maxRetries - 1)
                {
                    Log.Warning("Request failed, retrying in {Delay}s. Error: {Message}", Math.Pow(2, i), ex.Message);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                }
            }
            throw new HttpRequestException("Max retries exceeded.");
        }

        public async Task<(List<SkinModel>? Skins, string? RawJson)> FetchRemoteSkinsAsync()
        {
            try
            {
                Log.Information("Fetching remote skins.json from GitHub...");
                string url = $"{Constants.GitHubRawUrl}/{Constants.SkinsJson}?t={DateTime.Now.Ticks}";

                HttpResponseMessage response = await GetWithRetryAsync(url);
                string json = await response.Content.ReadAsStringAsync();
                List<SkinModel>? skins = JsonSerializer.Deserialize<List<SkinModel>>(json);

                return (skins, json);
            }
            catch (Exception ex)
            {
                Log.Warning("Network error while fetching remote skins: {Message}", ex.Message);
                return (null, null);
            }
        }

        public async Task EnsureAssetsExistAsync(SkinModel skin, Action<string>? onProgress = null)
        {
            try
            {
                await EnsureFileExistsAsync(skin.PreviewImage, "Preview", skin.SourceFolder, onProgress);

                string[] prefixes = ["details", "lightmap", "alpha"];
                foreach (string prefix in prefixes)
                {
                    await EnsureFileExistsAsync(string.Empty, prefix, skin.SourceFolder, onProgress);
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
            }

            string fileNameWithDefaultExtension = !string.IsNullOrEmpty(exactRelativePath)
                ? Path.GetFileName(exactRelativePath)
                : $"{filePrefix}.png";

            string normalizedRelativePath = Path.Combine(sourceFolder, fileNameWithDefaultExtension).Replace("\\", "/");
            string downloadLocalPath = FileHelper.GetSafePath(baseDir, normalizedRelativePath);

            Log.Information("Asset missing. Starting download: {Path}", downloadLocalPath);
            string url = $"{Constants.GitHubRawUrl}/{normalizedRelativePath}";
            onProgress?.Invoke($"Downloading {fileNameWithDefaultExtension}...");

            _ = Directory.CreateDirectory(Path.GetDirectoryName(downloadLocalPath)!);

            HttpResponseMessage response = await GetWithRetryAsync(url);
            byte[] data = await response.Content.ReadAsByteArrayAsync();

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
            string tempPath = Path.Combine(Path.GetTempPath(), "ProTankiTextureSwapper_Setup.exe");

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
