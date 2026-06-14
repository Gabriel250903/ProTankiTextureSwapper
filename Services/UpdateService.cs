using Serilog;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using TextureSwapper.Core;
using TextureSwapper.Models;

namespace TextureSwapper.Services
{
    public class UpdateService
    {
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProTankiTextureSwapper");
        }

        public async Task<List<SkinModel>?> FetchRemoteSkinsAsync()
        {
            try
            {
                Log.Information("Fetching remote skins.json from GitHub...");
                string url = $"{Constants.GitHubRawUrl}/{Constants.SkinsJson}?t={DateTime.Now.Ticks}";

                HttpResponseMessage response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Could not fetch remote skins. Status: {Status}. URL: {Url}", response.StatusCode, url);
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                List<SkinModel>? skins = JsonSerializer.Deserialize<List<SkinModel>>(json);

                if (skins != null)
                {
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.SkinsJson);
                    await File.WriteAllTextAsync(localPath, json);
                }

                return skins;
            }
            catch (Exception ex)
            {
                Log.Warning("Network error while fetching remote skins: {Message}", ex.Message);
                return null;
            }
        }

        public async Task EnsureAssetsExistAsync(SkinModel skin, Action<string>? onProgress = null)
        {
            try
            {
                await EnsureFileExistsAsync(skin.PreviewImage, onProgress);

                string[] files = ["details.png", "lightmap.png", "alpha.png"];
                foreach (string file in files)
                {
                    string relativePath = Path.Combine(skin.SourceFolder, file).Replace("\\", "/");
                    await EnsureFileExistsAsync(relativePath, onProgress);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Failed to ensure assets exist for {SkinName}: {Message}", skin.Name, ex.Message);
            }
        }

        private async Task EnsureFileExistsAsync(string relativePath, Action<string>? onProgress = null)
        {
            string normalizedPath = relativePath.Replace("\\", "/");
            string localPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, normalizedPath));

            if (File.Exists(localPath))
            {
                Log.Debug("Asset already exists locally: {Path}", localPath);
                return;
            }

            Log.Information("Asset missing. Starting download: {Path}", localPath);
            string url = $"{Constants.GitHubRawUrl}/{normalizedPath}";
            onProgress?.Invoke($"Downloading {Path.GetFileName(normalizedPath)}...");

            _ = Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            HttpResponseMessage response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Failed to download asset {Path}. Status: {Status}. URL: {Url}", normalizedPath, response.StatusCode, url);
                return;
            }

            byte[] data = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localPath, data);
            Log.Information("Asset downloaded and saved: {Path}", localPath);
        }

        public async Task<GitHubRelease?> CheckForAppUpdatesAsync()
        {
            try
            {
                Log.Information("Checking for app updates on GitHub...");
                string url = $"{Constants.GitHubApiUrl}/releases/latest";

                HttpResponseMessage response = await _httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Log.Information("No app releases found on GitHub.");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warning("Failed to check for updates. Status: {Status}", response.StatusCode);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<GitHubRelease>();
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

    public class GitHubRelease
    {
        [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string Body { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = [];
    }

    public class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
