using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class SettingsService : ISettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Settings.json"
        );
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private static readonly object _saveLock = new();

        public AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    AppSettings settings =
                        JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                        ?? new AppSettings();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load Settings.");
            }

            return new AppSettings();
        }

        public void Save(AppSettings settings)
        {
            lock (_saveLock)
            {
                string tempPath = SettingsPath + $".tmp_{Guid.NewGuid():N}";
                try
                {
                    string json = JsonSerializer.Serialize(settings, _jsonOptions);

                    if (File.Exists(SettingsPath))
                    {
                        var attrs = File.GetAttributes(SettingsPath);
                        if (attrs.HasFlag(FileAttributes.ReadOnly))
                        {
                            File.SetAttributes(SettingsPath, attrs & ~FileAttributes.ReadOnly);
                        }
                    }

                    File.WriteAllText(tempPath, json);

                    bool moved = false;
                    for (int attempt = 1; attempt <= 5; attempt++)
                    {
                        try
                        {
                            File.Move(tempPath, SettingsPath, overwrite: true);
                            moved = true;
                            break;
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                        {
                            if (attempt == 5)
                            {
                                Log.Warning(ex, "Failed to atomically replace Settings.json via File.Move after 5 attempts. Falling back to direct write.");
                            }
                            else
                            {
                                Thread.Sleep(30);
                            }
                        }
                    }

                    if (!moved)
                    {
                        using var stream = new FileStream(SettingsPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                        using var writer = new StreamWriter(stream);
                        writer.Write(json);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to save Settings.");
                }
                finally
                {
                    try
                    {
                        if (File.Exists(tempPath))
                        {
                            File.Delete(tempPath);
                        }
                    }
                    catch
                    {
                        Log.Error("Failed to delete temporary settings file: {TempPath}", tempPath);
                    }
                }
            }
        }
    }
}
