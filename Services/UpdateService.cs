using Serilog;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using TextureSwapper.Core;
using TextureSwapper.Helpers;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public sealed class UpdateService : IUpdateService
    {
        private static readonly HttpClient _httpClient;

        public bool IsOffline { get; private set; }

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
                        response.Dispose();
                        throw new HttpRequestException($"Resource not found (404): {url}", null, HttpStatusCode.NotFound);
                    }
                    _ = response.EnsureSuccessStatusCode();
                    IsOffline = false;
                    return response;
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Warning($"Resource not found (404): {url}");
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    if (ex.InnerException is SocketException socketEx &&
                        (socketEx.SocketErrorCode == SocketError.HostNotFound ||
                         socketEx.SocketErrorCode == SocketError.NetworkUnreachable ||
                         socketEx.SocketErrorCode == SocketError.ConnectionRefused ||
                         socketEx.SocketErrorCode == SocketError.ConnectionAborted ||
                         socketEx.SocketErrorCode == SocketError.TimedOut))
                    {
                        Log.Warning("Network connection error. Switching to Offline Mode.");
                        IsOffline = true;
                        throw;
                    }

                    if (i < maxRetries - 1)
                    {
                        Log.Warning($"Request failed, retrying in {Math.Pow(2, i)}s. Error: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                    }
                    else
                    {
                        IsOffline = true;
                        throw new HttpRequestException("Max retries exceeded.", ex);
                    }
                }
            }
            IsOffline = true;
            throw new HttpRequestException("Max retries exceeded.");
        }

        public async Task<(List<SkinModel>? Skins, string? RawJson)> FetchRemoteSkinsFileAsync(string fileName)
        {
            try
            {
                Log.Information($"Fetching remote {fileName} from GitHub...");
                string url = $"{Constants.GitHubRawUrl}/{fileName}?t={DateTime.Now.Ticks}";

                using HttpResponseMessage response = await GetWithRetryAsync(url);
                string json = await response.Content.ReadAsStringAsync();
                List<SkinModel>? skins = JsonSerializer.Deserialize<List<SkinModel>>(json);

                return (skins, json);
            }
            catch (Exception ex)
            {
                IsOffline = true;
                Log.Warning($"Network error while fetching remote skins ({fileName}): {ex.Message}");
                return (null, null);
            }
        }

        public async Task<(List<ShotEffectModel>? ShotEffects, string? RawJson)> FetchRemoteShotEffectsFileAsync(string fileName)
        {
            try
            {
                Log.Information($"Fetching remote {fileName} from GitHub...");
                string url = $"{Constants.GitHubRawUrl}/{fileName}?t={DateTime.Now.Ticks}";

                using HttpResponseMessage response = await GetWithRetryAsync(url);
                string json = await response.Content.ReadAsStringAsync();
                List<ShotEffectModel>? shotEffects = JsonSerializer.Deserialize<List<ShotEffectModel>>(json);

                return (shotEffects, json);
            }
            catch (Exception ex)
            {
                IsOffline = true;
                Log.Warning($"Network error while fetching remote shot effects ({fileName}): {ex.Message}");
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
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to ensure assets exist for {skin.Name}: {ex.Message}");
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
                        Log.Debug($"Asset already exists locally: {file}");
                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(exactRelativePath))
            {
                string exactLocalPath = FileHelper.GetSafePath(baseDir, exactRelativePath.Replace("\\", "/"));
                if (File.Exists(exactLocalPath))
                {
                    Log.Debug($"Asset already exists locally: {exactLocalPath}");
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
                            Log.Debug($"Asset already exists locally with alternative extension: {altPath}");
                            return;
                        }
                    }
                }
            }

            string downloadLocalPath = string.Empty;
            byte[]? data = null;


            string fileNameWithDefaultExtension;
            string normalizedRelativePath;
            if (!string.IsNullOrEmpty(exactRelativePath))
            {
                fileNameWithDefaultExtension = Path.GetFileName(exactRelativePath);
                normalizedRelativePath = exactRelativePath.Replace("\\", "/");
                downloadLocalPath = FileHelper.GetSafePath(baseDir, normalizedRelativePath);

                Log.Information("Asset missing. Starting download: {Path}", downloadLocalPath);
                string url = $"{Constants.GitHubRawUrl}/{normalizedRelativePath}";
                onProgress?.Invoke($"Downloading {fileNameWithDefaultExtension}...");

                _ = Directory.CreateDirectory(Path.GetDirectoryName(downloadLocalPath)!);
                using HttpResponseMessage response = await GetWithRetryAsync(url);
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
                        Log.Information($"Trying download: {downloadLocalPath}");
                        using HttpResponseMessage response = await GetWithRetryAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            data = await response.Content.ReadAsByteArrayAsync();
                            break;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    {
                        Log.Warning($"Resource not found (404): {url}. Status: {HttpStatusCode.NotFound}.");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"Non-404 error trying to download {downloadLocalPath}: {ex.Message}");
                    }
                }

                if (data == null)
                {
                    throw new FileNotFoundException($"Could not find remote asset with prefix '{filePrefix}' in folder '{sourceFolder}' on GitHub.");
                }
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(downloadLocalPath)!);
            await VerifyAndSaveFileAsync(data, downloadLocalPath);
            Log.Information($"Asset downloaded and saved: {downloadLocalPath}");
        }

        private async Task VerifyAndSaveFileAsync(byte[] data, string targetPath, string? expectedSha256 = null)
        {
            byte[] hashBytes = SHA256.HashData(data);
            string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (!string.IsNullOrEmpty(expectedSha256) && !computedHash.Equals(expectedSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                Log.Error($"Hash mismatch for {targetPath}. Expected: {expectedSha256}, Computed: {computedHash}");
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

                using HttpResponseMessage response = await GetWithRetryAsync(url);
                string json = await response.Content.ReadAsStringAsync();
                List<InGamePaintModel>? paints = JsonSerializer.Deserialize<List<InGamePaintModel>>(json);

                return (paints, json);
            }
            catch (Exception ex)
            {
                IsOffline = true;
                Log.Warning($"Network error while fetching remote in-game paints ({fileName}): {ex.Message}");
                return (null, null);
            }
        }

        public async Task<GitHubReleaseModel?> CheckForAppUpdatesAsync()
        {
            try
            {
                Log.Information("Checking for app updates on GitHub...");
                string url = $"{Constants.GitHubApiUrl}/releases/latest";

                using HttpResponseMessage response = await GetWithRetryAsync(url);
                return await response.Content.ReadFromJsonAsync<GitHubReleaseModel>();
            }
            catch (Exception ex)
            {
                IsOffline = true;
                Log.Error($"Network error while checking for app updates: {ex.Message}");
                return null;
            }
        }

        public async Task DownloadAndRunInstallerAsync(string downloadUrl, Action<double>? onProgress = null)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"TextureSwapper_Setup_{Guid.NewGuid():N}.exe");

            Log.Information($"Downloading installer from {downloadUrl} to {tempPath}");

            string? expectedHash = null;
            try
            {
                using HttpResponseMessage hashResponse = await _httpClient.GetAsync(downloadUrl + ".sha256");
                if (hashResponse.IsSuccessStatusCode)
                {
                    string hashText = await hashResponse.Content.ReadAsStringAsync();
                    expectedHash = hashText.Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                                           .FirstOrDefault()?
                                           .Trim();
                    Log.Information($"Found expected SHA256 hash for installer: {expectedHash}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"Could not fetch companion installer SHA256: {ex.Message}");
            }

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

            using (FileStream fs = new(tempPath, FileMode.Open, FileAccess.Read))
            {
                if (fs.Length < 2)
                {
                    throw new InvalidDataException("Downloaded installer is too small to be valid.");
                }
                int b1 = fs.ReadByte();
                int b2 = fs.ReadByte();
                if (b1 != 0x4D || b2 != 0x5A)
                {
                    throw new InvalidDataException("Downloaded installer is not a valid executable file.");
                }
            }

            byte[] fileBytes = await File.ReadAllBytesAsync(tempPath);

            if (!string.IsNullOrEmpty(expectedHash))
            {
                byte[] hashBytes = SHA256.HashData(fileBytes);
                string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                if (!computedHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new CryptographicException($"Installer hash mismatch! Expected: {expectedHash}, Computed: {computedHash}");
                }
                Log.Information("Installer hash integrity check passed successfully.");
            }

            string signatureUrl = downloadUrl + ".sig";
            byte[] signatureBytes;
            try
            {
                Log.Information($"Fetching cryptographic signature from {signatureUrl}");
                using HttpResponseMessage sigResponse = await _httpClient.GetAsync(signatureUrl);
                if (!sigResponse.IsSuccessStatusCode)
                {
                    throw new CryptographicException("Failed to fetch installer cryptographic signature (.sig).");
                }
                signatureBytes = await sigResponse.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex) when (ex is not CryptographicException)
            {
                throw new CryptographicException($"Could not download installer security signature: {ex.Message}");
            }

            try
            {
                using ECDsa ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(Constants.PublisherPublicKey), out _);

                bool isSignatureValid = ecdsa.VerifyData(fileBytes, signatureBytes, HashAlgorithmName.SHA256);
                if (!isSignatureValid)
                {
                    throw new CryptographicException("Installer digital signature is invalid!");
                }
                Log.Information("Installer digital signature verified successfully.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Digital signature verification failed.");
                throw;
            }

            Log.Information($"Launching installer: {tempPath}");
            Log.CloseAndFlush();
            _ = Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
            Application.Current?.Dispatcher.Invoke(Application.Current.Shutdown);
        }
    }
}
