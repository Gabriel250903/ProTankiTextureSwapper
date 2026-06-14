using Serilog;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
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
                string json = await _httpClient.GetStringAsync(url);
                var skins = JsonSerializer.Deserialize<List<SkinModel>>(json);
                
                if (skins != null)
                {
                    // Save local copy for offline use
                    string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.SkinsJson);
                    await File.WriteAllTextAsync(localPath, json);
                }

                return skins;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to fetch remote skins. Falling back to local copy.");
                return null;
            }
        }

        public async Task EnsureAssetsExistAsync(SkinModel skin, Action<string>? onProgress = null)
        {
            try
            {
                // Check PreviewImage
                await EnsureFileExistsAsync(skin.PreviewImage, onProgress);

                // Check Source Folder contents
                string[] files = { "details.png", "lightmap.png", "alpha.png" };
                foreach (var file in files)
                {
                    string relativePath = Path.Combine(skin.SourceFolder, file).Replace("\\", "/");
                    await EnsureFileExistsAsync(relativePath, onProgress);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to ensure assets exist for {SkinName}", skin.Name);
            }
        }

        private async Task EnsureFileExistsAsync(string relativePath, Action<string>? onProgress = null)
        {
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath);
            if (File.Exists(localPath)) return;

            Log.Information("Downloading missing asset: {Path}", relativePath);
            onProgress?.Invoke($"Downloading {Path.GetFileName(relativePath)}...");

            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

            string url = $"{Constants.GitHubRawUrl}/{relativePath.Replace("\\", "/")}";
            byte[] data = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(localPath, data);
        }

        public async Task<GitHubRelease?> CheckForAppUpdatesAsync()
        {
            try
            {
                Log.Information("Checking for app updates on GitHub...");
                string url = $"{Constants.GitHubApiUrl}/releases/latest";
                var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(url);

                if (release != null)
                {
                    string currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
                    string latestVersion = release.TagName.TrimStart('v');

                    if (IsNewerVersion(currentVersion, latestVersion))
                    {
                        Log.Information("New version available: {Version}", latestVersion);
                        return release;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to check for app updates.");
            }

            return null;
        }

        private bool IsNewerVersion(string current, string latest)
        {
            if (Version.TryParse(current, out Version? vCurrent) && Version.TryParse(latest, out Version? vLatest))
            {
                return vLatest > vCurrent;
            }
            return false;
        }

        public async Task DownloadAndRunInstallerAsync(string downloadUrl, Action<double>? onProgress = null)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "ProTankiTextureSwapper_Setup.exe");
            
            Log.Information("Downloading installer from {Url} to {Path}", downloadUrl, tempPath);

            using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    var totalRead = 0L;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;

                        if (totalBytes != -1)
                        {
                            onProgress?.Invoke((double)totalRead / totalBytes * 100);
                        }
                    }
                }
            }

            Log.Information("Launching installer: {Path}", tempPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(tempPath) { UseShellExecute = true });
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
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    public class GitHubAsset
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}
