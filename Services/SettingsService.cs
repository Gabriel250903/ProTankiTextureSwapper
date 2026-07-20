using Serilog;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public sealed class SettingsService : ISettingsService
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();

                    if (!string.IsNullOrEmpty(settings.HuggingFaceToken))
                    {
                        try
                        {
                            byte[] encryptedBytes = Convert.FromBase64String(settings.HuggingFaceToken);
                            byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
                            settings.HuggingFaceToken = Encoding.UTF8.GetString(decryptedBytes);
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Failed to decrypt API token using DPAPI. The token may be corrupted or was not encrypted.");
                        }
                    }

                    return settings;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load settings.");
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            try
            {
                AppSettings copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings, _jsonOptions), _jsonOptions) ?? new AppSettings();

                if (!string.IsNullOrEmpty(copy.HuggingFaceToken))
                {
                    try
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(copy.HuggingFaceToken);
                        byte[] encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
                        copy.HuggingFaceToken = Convert.ToBase64String(encryptedBytes);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to encrypt API token using DPAPI. The token may be corrupted or was not encrypted.");
                    }
                }

                string json = JsonSerializer.Serialize(copy, _jsonOptions);
                string tempPath = SettingsPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, SettingsPath, true);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save settings.");
            }
        }
    }
}
