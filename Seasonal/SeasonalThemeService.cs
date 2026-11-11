using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Core.Models;
using Wpf.Ui.Appearance;

namespace TextureSwapper.Seasonal
{
    public sealed class SeasonalThemeService(ISettingsService settingsService)
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveFontResourceEx(string lpFileName, uint fl, IntPtr pdv);

        private const uint FR_PRIVATE = 0x10;

        private readonly ISettingsService _settingsService = settingsService;
        private AppSettings Settings => _settingsService.Load();
        private ResourceDictionary? _seasonalDictionary;
        private bool? _isThemeApplied;
        private bool _isUpdating;
        private string? _registeredFontPath;

        public bool IsRemoteAllowed { get; private set; } = true;
        public bool IsThematicGoldAllowed { get; private set; } = true;

        public bool IsThematicGoldActive =>
            IsRemoteAllowed && IsThematicGoldAllowed && !Settings.DisableThematicGold;

        public event EventHandler<bool>? SeasonalThemeChanged;
        public event EventHandler<bool>? HalloweenThemeChanged
        {
            add => SeasonalThemeChanged += value;
            remove => SeasonalThemeChanged -= value;
        }

        public bool IsWithinSeasonalWindow =>
            IsRemoteAllowed && SeasonalThemeConfig.ActiveSeason != SeasonType.None;

        public bool IsWithinHalloweenWindow => IsWithinSeasonalWindow;

        public bool IsSeasonalActive =>
            SeasonalThemeConfig.ActiveSeason != SeasonType.None
            && IsRemoteAllowed
            && (Settings.IsSeasonalThemeSelected || Settings.ForceSeasonalPreview);

        public bool IsHalloweenActive => IsSeasonalActive;

        private void TryLoadLocalConfig()
        {
            try
            {
                string localConfigPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "remote_config.json"
                );
                if (File.Exists(localConfigPath))
                {
                    string json = File.ReadAllText(localConfigPath);
                    RemoteConfigModel? cfg = JsonSerializer.Deserialize<RemoteConfigModel>(json);
                    if (cfg != null)
                    {
                        IsRemoteAllowed = cfg.SeasonalEnabled || cfg.HalloweenEnabled;
                        IsThematicGoldAllowed = cfg.ThematicGoldEnabled;
                    }
                }
            }
            catch { }
        }

        public async Task SyncRemoteSeasonalConfigAsync(IUpdateService updateService)
        {
            TryLoadLocalConfig();
            try
            {
                string url =
                    $"{Constants.GitHubRawUrl}/remote_config.json?t={DateTime.UtcNow.Ticks}";
                using HttpResponseMessage res = await updateService.GetWithRetryAsync(
                    url,
                    maxRetries: 2
                );
                if (res.IsSuccessStatusCode)
                {
                    string json = await res.Content.ReadAsStringAsync();
                    RemoteConfigModel? config = JsonSerializer.Deserialize<RemoteConfigModel>(json);
                    if (config != null)
                    {
                        bool remoteAllowed = config.SeasonalEnabled || config.HalloweenEnabled;
                        bool changed = IsRemoteAllowed != remoteAllowed;
                        IsRemoteAllowed = remoteAllowed;
                        IsThematicGoldAllowed = config.ThematicGoldEnabled;

                        Log.Information(
                            "Remote seasonal config received: SeasonalEnabled={SeasonalEnabled}, ThematicGoldEnabled={ThematicGoldEnabled}",
                            IsRemoteAllowed,
                            IsThematicGoldAllowed
                        );

                        if (changed)
                        {
                            CheckAndApply(notify: true, force: true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Could not fetch remote_config.json from GitHub: {Message}", ex.Message);
            }
        }

        public Task SyncRemoteHalloweenConfigAsync(IUpdateService updateService)
        {
            return SyncRemoteSeasonalConfigAsync(updateService);
        }

        public void CheckAndApply(bool notify = true, bool force = false)
        {
            if (_isUpdating)
            {
                return;
            }

            bool shouldBeActive = IsSeasonalActive;
            if (!force && _isThemeApplied.HasValue && shouldBeActive == _isThemeApplied.Value)
            {
                return;
            }

            _isUpdating = true;
            try
            {
                _isThemeApplied = shouldBeActive;

                if (shouldBeActive)
                {
                    ApplySeasonalTheme();
                }
                else
                {
                    RevertToStandardTheme();
                }

                if (notify)
                {
                    SeasonalThemeChanged?.Invoke(this, shouldBeActive);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply or revert seasonal theme.");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void ApplySeasonalTheme()
        {
            try
            {
                SeasonalThemeDefinition current = SeasonalThemeConfig.Current;

                ApplicationAccentColorManager.Apply(
                    current.AccentColor,
                    ApplicationTheme.Dark,
                    systemGlassColor: false,
                    systemAccentColor: false
                );

                _seasonalDictionary ??= new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/Assets/Themes/Seasonal/SeasonalThemeDictionary.xaml",
                        UriKind.Absolute
                    ),
                };

                if (!Application.Current.Resources.MergedDictionaries.Contains(_seasonalDictionary))
                {
                    Application.Current.Resources.MergedDictionaries.Add(_seasonalDictionary);
                }

                try
                {
                    if (
                        Settings.EnableSeasonalCustomFont
                        && !string.IsNullOrEmpty(current.FontFileName)
                    )
                    {
                        string fontPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "Assets",
                            "Themes",
                            current.Season.ToString(),
                            current.FontFileName
                        );

                        if (File.Exists(fontPath))
                        {
                            _ = AddFontResourceEx(fontPath, FR_PRIVATE, IntPtr.Zero);
                            _registeredFontPath = fontPath;
                            string normalized = fontPath.Replace('\\', '/');
                            FontFamily seasonalFont = new(
                                $"file:///{normalized}#{current.FontFamilyName}"
                            );

                            Application.Current.Resources["ActiveDisplayFont"] = seasonalFont;
                            Application.Current.Resources["SeasonalDisplayFont"] = seasonalFont;
                            Application.Current.Resources["HalloweenSpookyFont"] = seasonalFont;

                            if (_seasonalDictionary != null)
                            {
                                _seasonalDictionary["ActiveDisplayFont"] = seasonalFont;
                                _seasonalDictionary["SeasonalDisplayFont"] = seasonalFont;
                                _seasonalDictionary["HalloweenSpookyFont"] = seasonalFont;
                            }
                        }
                    }
                    else
                    {
                        FontFamily standardFont = new("Segoe UI");
                        Application.Current.Resources["ActiveDisplayFont"] = standardFont;
                        Application.Current.Resources["SeasonalDisplayFont"] = standardFont;
                        Application.Current.Resources["HalloweenSpookyFont"] = standardFont;
                    }
                }
                catch (Exception fontEx)
                {
                    Log.Debug(fontEx, "Failed to register seasonal font.");
                }

                Log.Information("Seasonal Theme ({Season}) activated.", current.DisplayName);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error applying seasonal theme resources.");
            }
        }

        private void RevertToStandardTheme()
        {
            try
            {
                List<ResourceDictionary> toRemove =
                [
                    .. Application.Current.Resources.MergedDictionaries.Where(d =>
                        d.Source != null
                        && (
                            d.Source.OriginalString.Contains(
                                "SeasonalThemeDictionary.xaml",
                                StringComparison.OrdinalIgnoreCase
                            )
                            || d.Source.OriginalString.Contains(
                                "HalloweenThemeDictionary.xaml",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    ),
                ];

                foreach (ResourceDictionary dict in toRemove)
                {
                    _ = Application.Current.Resources.MergedDictionaries.Remove(dict);
                }

                if (_seasonalDictionary != null)
                {
                    _ = Application.Current.Resources.MergedDictionaries.Remove(
                        _seasonalDictionary
                    );
                }

                if (!string.IsNullOrEmpty(_registeredFontPath))
                {
                    try
                    {
                        _ = RemoveFontResourceEx(_registeredFontPath, FR_PRIVATE, IntPtr.Zero);
                    }
                    catch { }
                    _registeredFontPath = null;
                }

                FontFamily standardFont = new("Segoe UI");
                Application.Current.Resources["ActiveDisplayFont"] = standardFont;
                Application.Current.Resources["SeasonalDisplayFont"] = standardFont;
                Application.Current.Resources["HalloweenSpookyFont"] = standardFont;

                try
                {
                    FieldInfo? field = typeof(ApplicationThemeManager).GetField(
                        "_cachedApplicationTheme",
                        BindingFlags.Static | BindingFlags.NonPublic
                    );
                    field?.SetValue(null, ApplicationTheme.Unknown);
                }
                catch { }

                ApplicationThemeManager.Apply(Settings.Theme, updateAccent: true);
                ApplicationAccentColorManager.ApplySystemAccent();

                Log.Information("Seasonal Theme deactivated.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reverting seasonal theme resources.");
            }
        }
    }
}
