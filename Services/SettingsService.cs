using Serilog;
using System.IO;
using System.Text.Json;
using TextureSwapper.Models;
using TextureSwapper.Services.Interfaces;

namespace TextureSwapper.Services
{
    public class SettingsService : ISettingsService
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
                    return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
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
                string json = JsonSerializer.Serialize(settings, _jsonOptions);
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
