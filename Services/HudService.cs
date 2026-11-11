using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Helpers.Utilities;

namespace TextureSwapper.Services
{
    public sealed class HudService : IHudService
    {
        private static readonly string PatchedLoaderSha256 =
            "911051a8ec65634a911d741527f66456413909539731746da7466f8dee857799";

        public string GetLocalStorePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, Constants.StandaloneLoaderDir, Constants.LocalStoreDir);
        }

        public string GetGameInstallPath()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string standardPath = Path.Combine(programFiles, "ProTanki Online");
            if (Directory.Exists(standardPath))
            {
                return standardPath;
            }

            string programFilesX86 = Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86
            );
            string x86Path = Path.Combine(programFilesX86, "ProTanki Online");
            return Directory.Exists(x86Path) ? x86Path : standardPath;
        }

        private static string? GetSourceCenteredLibraryPath()
        {
            string[] candidates =
            [
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "HUD",
                    "Centered",
                    "library.swf"
                ),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets",
                    "HUD",
                    "Centered",
                    "library.swf"
                ),
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "Assets",
                    "HUD",
                    "Centered",
                    "library.swf"
                ),
            ];

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }

        private static async Task<string?> EnsureCenteredLibraryPathAsync()
        {
            string? sourcePath = GetSourceCenteredLibraryPath();
            if (sourcePath != null && File.Exists(sourcePath))
            {
                return sourcePath;
            }

            string defaultPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets",
                "HUD",
                "Centered",
                "library.swf"
            );

            try
            {
                string dir = Path.GetDirectoryName(defaultPath)!;
                _ = Directory.CreateDirectory(dir);

                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.Add("User-Agent", "ProTankiTextureSwapper");

                string swfUrl = $"{Constants.GitHubRawUrl}/Assets/HUD/Centered/library.swf";
                Log.Information("Downloading missing Centered library.swf from {Url}", swfUrl);
                byte[] swfBytes = await client.GetByteArrayAsync(swfUrl);
                await File.WriteAllBytesAsync(defaultPath, swfBytes);

                if (File.Exists(defaultPath))
                {
                    return defaultPath;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to download missing Centered library.swf from GitHub.");
            }

            return sourcePath;
        }

        private static string? GetInstallBatchScriptPath()
        {
            string[] candidates =
            [
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets",
                    "HUD",
                    "InstallLoaderPatch.bat"
                ),
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "Assets",
                    "HUD",
                    "InstallLoaderPatch.bat"
                ),
                Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "Assets",
                    "HUD",
                    "InstallLoaderPatch.bat"
                ),
            ];

            foreach (string path in candidates)
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }

            return null;
        }

        private static async Task<string?> EnsureInstallBatchScriptAsync()
        {
            string? scriptPath = GetInstallBatchScriptPath();
            if (scriptPath != null && File.Exists(scriptPath))
            {
                string dir = Path.GetDirectoryName(scriptPath)!;
                string siblingSwf = Path.Combine(dir, "StandaloneLoader_patched.swf");
                if (File.Exists(siblingSwf))
                {
                    return scriptPath;
                }
            }

            string defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "HUD");
            string defaultBat = Path.Combine(defaultDir, "InstallLoaderPatch.bat");
            string defaultSwf = Path.Combine(defaultDir, "StandaloneLoader_patched.swf");

            try
            {
                _ = Directory.CreateDirectory(defaultDir);
                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(30) };
                client.DefaultRequestHeaders.Add("User-Agent", "ProTankiTextureSwapper");

                if (!File.Exists(defaultBat))
                {
                    string batUrl = $"{Constants.GitHubRawUrl}/Assets/HUD/InstallLoaderPatch.bat";
                    Log.Information("Downloading missing InstallLoaderPatch.bat from {Url}", batUrl);
                    byte[] batBytes = await client.GetByteArrayAsync(batUrl);
                    await File.WriteAllBytesAsync(defaultBat, batBytes);
                }

                if (!File.Exists(defaultSwf))
                {
                    string swfUrl = $"{Constants.GitHubRawUrl}/Assets/HUD/StandaloneLoader_patched.swf";
                    Log.Information("Downloading missing StandaloneLoader_patched.swf from {Url}", swfUrl);
                    byte[] swfBytes = await client.GetByteArrayAsync(swfUrl);
                    await File.WriteAllBytesAsync(defaultSwf, swfBytes);
                }

                if (File.Exists(defaultBat) && File.Exists(defaultSwf))
                {
                    return defaultBat;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to download missing HUD patch assets from GitHub.");
            }

            return scriptPath;
        }

        public bool IsCenteredHudEnabled()
        {
            try
            {
                string targetPath = Path.Combine(GetLocalStorePath(), "library.swf");
                return File.Exists(targetPath) && new FileInfo(targetPath).Length > 0;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to check if Centered HUD is enabled.");
                return false;
            }
        }

        public bool IsLoaderPatched()
        {
            try
            {
                string gameDir = GetGameInstallPath();
                string loaderPath = Path.Combine(gameDir, "StandaloneLoader.swf");
                if (!File.Exists(loaderPath))
                {
                    return false;
                }

                using FileStream stream = File.OpenRead(loaderPath);
                using SHA256 sha256 = SHA256.Create();
                byte[] hashBytes = sha256.ComputeHash(stream);
                string hash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                return string.Equals(hash, PatchedLoaderSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to verify StandaloneLoader.swf patch status.");
                return false;
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ApplyCenteredHudAsync(
            CancellationToken cancellationToken = default
        )
        {
            try
            {
                string? sourcePath = await EnsureCenteredLibraryPathAsync();
                if (sourcePath is null || !File.Exists(sourcePath))
                {
                    string err = $"Centered HUD asset not found at '{sourcePath}'.";
                    Log.Error(err);
                    return (false, err);
                }

                string targetDir = GetLocalStorePath();
                if (!Directory.Exists(targetDir))
                {
                    _ = Directory.CreateDirectory(targetDir);
                }

                string targetPath = Path.Combine(targetDir, "library.swf");

                if (FileHelper.IsGameRunning())
                {
                    Log.Warning(
                        "ProTanki is currently running. Centered HUD will apply on next restart."
                    );
                }

                File.Copy(sourcePath, targetPath, overwrite: true);
                Log.Information(
                    "Centered HUD successfully applied to Local Store: {TargetPath}",
                    targetPath
                );
                return (true, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply Centered HUD.");
                return (false, ex.Message);
            }
        }

        public (bool Success, string? ErrorMessage) RestoreDefaultHud()
        {
            try
            {
                string targetPath = Path.Combine(GetLocalStorePath(), "library.swf");
                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                    Log.Information(
                        "Centered HUD removed from Local Store: {TargetPath}",
                        targetPath
                    );
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore default HUD.");
                return (false, ex.Message);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> InstallLoaderPatchAsync()
        {
            try
            {
                string? scriptPath = await EnsureInstallBatchScriptAsync();
                if (scriptPath is null || !File.Exists(scriptPath))
                {
                    string err = $"Installer script not found at '{scriptPath}'.";
                    Log.Error(err);
                    return (false, err);
                }

                if (FileHelper.IsGameRunning())
                {
                    string err =
                        "ProTanki is running! Please close the game before patching StandaloneLoader.";
                    Log.Warning(err);
                    return (false, err);
                }

                ProcessStartInfo psi = new()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{scriptPath}\"\"",
                    UseShellExecute = true,
                    Verb = "runas",
                };

                using Process? proc = Process.Start(psi);
                if (proc == null)
                {
                    return (false, "Could not launch elevated patch installer.");
                }

                await proc.WaitForExitAsync();

                if (IsLoaderPatched())
                {
                    Log.Information("StandaloneLoader patch verified successfully.");
                    return (true, null);
                }

                return (false, "StandaloneLoader patch did not complete or was cancelled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to execute patch installer.");
                return (false, ex.Message);
            }
        }
    }
}
