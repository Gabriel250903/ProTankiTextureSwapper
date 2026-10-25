using System.IO;
using System.Text.Json;
using System.Windows;
using Serilog;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;

namespace TextureSwapper.Services
{
    public sealed class LocalizationService : ILocalizationService
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private readonly Dictionary<string, string> _translations = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, string> _fallbackTranslations = new(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly object _lock = new();
        private ResourceDictionary? _localizationDictionary;

        public static readonly IReadOnlyList<LanguageInfo> Languages = new List<LanguageInfo>
        {
            new("en", "English"),
            new("ru", "Русский"),
            new("uk", "Українська"),
        }.AsReadOnly();

        public IReadOnlyList<LanguageInfo> SupportedLanguages => Languages;

        public string CurrentLanguage { get; private set; } = "en";

        public event EventHandler<string>? LanguageChanged;

        public string this[string key] => GetString(key);

        public LocalizationService()
        {
            _instance = this;
            LoadFallbackTranslations();
        }

        private void LoadFallbackTranslations()
        {
            lock (_lock)
            {
                _fallbackTranslations.Clear();
                string enPath = GetLocaleFilePath("en");
                if (File.Exists(enPath))
                {
                    try
                    {
                        string json = File.ReadAllText(enPath);
                        Dictionary<string, string>? dict = JsonSerializer.Deserialize<
                            Dictionary<string, string>
                        >(json);
                        if (dict != null)
                        {
                            foreach (KeyValuePair<string, string> kvp in dict)
                            {
                                _fallbackTranslations[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to load base English locale file at {Path}.", enPath);
                    }
                }
            }
        }

        private static string GetLocaleFilePath(string languageCode)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(baseDir, "Assets", "Locales", $"{languageCode}.json");
            if (!File.Exists(path))
            {
                try
                {
                    string devPath = Path.GetFullPath(
                        Path.Combine(
                            baseDir,
                            "..",
                            "..",
                            "..",
                            "Assets",
                            "Locales",
                            $"{languageCode}.json"
                        )
                    );
                    if (File.Exists(devPath))
                    {
                        return devPath;
                    }
                }
                catch { }
            }
            return path;
        }

        public void SetLanguage(string languageCode)
        {
            string normalized = string.IsNullOrWhiteSpace(languageCode)
                ? "en"
                : languageCode.Trim().ToLowerInvariant();
            if (normalized is not "en" and not "ru" and not "uk")
            {
                normalized = "en";
            }

            lock (_lock)
            {
                if (_fallbackTranslations.Count == 0)
                {
                    LoadFallbackTranslations();
                }

                _translations.Clear();
                foreach (KeyValuePair<string, string> kvp in _fallbackTranslations)
                {
                    _translations[kvp.Key] = kvp.Value;
                }

                if (normalized != "en")
                {
                    string targetPath = GetLocaleFilePath(normalized);
                    if (File.Exists(targetPath))
                    {
                        try
                        {
                            string json = File.ReadAllText(targetPath);
                            Dictionary<string, string>? dict = JsonSerializer.Deserialize<
                                Dictionary<string, string>
                            >(json);
                            if (dict != null)
                            {
                                foreach (KeyValuePair<string, string> kvp in dict)
                                {
                                    _translations[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(
                                ex,
                                "Failed to load locale file for '{Lang}' at {Path}.",
                                normalized,
                                targetPath
                            );
                        }
                    }
                    else
                    {
                        Log.Warning(
                            "Locale file not found for '{Lang}' at {Path}. Falling back to English.",
                            normalized,
                            targetPath
                        );
                    }
                }

                CurrentLanguage = normalized;
            }

            UpdateMergedDictionaries();
            LanguageChanged?.Invoke(this, CurrentLanguage);
        }

        private void UpdateMergedDictionaries()
        {
            void ApplyToApp()
            {
                try
                {
                    Application? app = Application.Current;
                    if (app == null)
                    {
                        return;
                    }

                    if (_localizationDictionary == null)
                    {
                        _localizationDictionary = [];
                        app.Resources.MergedDictionaries.Add(_localizationDictionary);
                    }

                    lock (_lock)
                    {
                        foreach (KeyValuePair<string, string> kvp in _translations)
                        {
                            _localizationDictionary[kvp.Key] = kvp.Value;
                        }
                    }

                    foreach (Window window in app.Windows)
                    {
                        try
                        {
                            ResourceDictionary? winDict =
                                window.Resources.MergedDictionaries.FirstOrDefault(d =>
                                    d.Contains("Loc_Settings_Title") || d.Contains("Loc_AppName")
                                );
                            if (winDict == null)
                            {
                                winDict = [];
                                window.Resources.MergedDictionaries.Add(winDict);
                            }
                            lock (_lock)
                            {
                                foreach (KeyValuePair<string, string> kvp in _translations)
                                {
                                    winDict[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                        catch (Exception winEx)
                        {
                            Log.Verbose(winEx, "Failed to update window localization dictionary.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to update dynamic ResourceDictionary for localization.");
                }
            }

            if (
                Application.Current?.Dispatcher != null
                && !Application.Current.Dispatcher.CheckAccess()
            )
            {
                Application.Current.Dispatcher.Invoke(ApplyToApp);
            }
            else
            {
                ApplyToApp();
            }
        }

        public string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            lock (_lock)
            {
                if (_translations.TryGetValue(key, out string? value))
                {
                    return value;
                }

                if (_fallbackTranslations.TryGetValue(key, out string? fallbackValue))
                {
                    return fallbackValue;
                }
            }

            return key;
        }

        public string GetString(string key, params object[] args)
        {
            string format = GetString(key);
            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (Exception ex)
            {
                Log.Warning(
                    ex,
                    "Failed to format localized string for key '{Key}' with format '{Format}'.",
                    key,
                    format
                );
                return format;
            }
        }
    }
}
