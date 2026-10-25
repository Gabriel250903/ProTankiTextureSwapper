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

namespace TextureSwapper.Services
{
    public sealed class HalloweenThemeService(ISettingsService settingsService)
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        private const uint FR_PRIVATE = 0x10;
        private static readonly Color PumpkinOrange = Color.FromRgb(0xFF, 0x75, 0x18);

        private readonly ISettingsService _settingsService = settingsService;
        private AppSettings Settings => _settingsService.Load();
        private ResourceDictionary? _halloweenDictionary;
        private bool? _isThemeApplied;
        private bool _isUpdating;

        public bool IsRemoteAllowed { get; private set; } = true;

        public event EventHandler<bool>? HalloweenThemeChanged;

        public bool IsWithinHalloweenWindow => IsRemoteAllowed;

        public bool IsHalloweenActive =>
            IsRemoteAllowed
            && (Settings.IsHalloweenThemeSelected || Settings.ForceHalloweenPreview);

        public async Task SyncRemoteHalloweenConfigAsync(IUpdateService updateService)
        {
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
                        bool changed = IsRemoteAllowed != config.HalloweenEnabled;
                        IsRemoteAllowed = config.HalloweenEnabled;
                        Log.Information(
                            "Remote Halloween configuration received: Enabled={Enabled}",
                            IsRemoteAllowed
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

        public void CheckAndApply(bool notify = true, bool force = false)
        {
            if (_isUpdating)
            {
                return;
            }

            bool shouldBeActive = IsHalloweenActive;
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
                    ApplyHalloweenTheme();
                }
                else
                {
                    RevertToStandardTheme();
                }

                if (notify)
                {
                    HalloweenThemeChanged?.Invoke(this, shouldBeActive);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to apply or revert Halloween theme.");
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void ApplyHalloweenTheme()
        {
            try
            {
                ApplicationAccentColorManager.Apply(
                    PumpkinOrange,
                    ApplicationTheme.Dark,
                    systemGlassColor: false,
                    systemAccentColor: false
                );

                _halloweenDictionary ??= new ResourceDictionary
                {
                    Source = new Uri(
                        "pack://application:,,,/Assets/Themes/Halloween/HalloweenThemeDictionary.xaml",
                        UriKind.Absolute
                    ),
                };

                if (
                    !Application.Current.Resources.MergedDictionaries.Contains(_halloweenDictionary)
                )
                {
                    Application.Current.Resources.MergedDictionaries.Add(_halloweenDictionary);
                }

                try
                {
                    if (Settings.EnableHalloweenSpookyFont)
                    {
                        string fontPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "Assets",
                            "Themes",
                            "Halloween",
                            "Creepster-Regular.ttf"
                        );
                        if (File.Exists(fontPath))
                        {
                            _ = AddFontResourceEx(fontPath, FR_PRIVATE, IntPtr.Zero);
                            string normalized = fontPath.Replace('\\', '/');
                            FontFamily spookyFont = new($"file:///{normalized}#Creepster");

                            Application.Current.Resources["ActiveDisplayFont"] = spookyFont;
                            Application.Current.Resources["HalloweenSpookyFont"] = spookyFont;

                            if (_halloweenDictionary != null)
                            {
                                _halloweenDictionary["ActiveDisplayFont"] = spookyFont;
                                _halloweenDictionary["HalloweenSpookyFont"] = spookyFont;
                            }

                            if (Application.Current.MainWindow != null)
                            {
                                Application.Current.MainWindow.Resources["ActiveDisplayFont"] =
                                    spookyFont;
                                Application.Current.MainWindow.Resources["HalloweenSpookyFont"] =
                                    spookyFont;
                            }
                        }
                    }
                    else
                    {
                        FontFamily standardFont = new("Segoe UI");
                        Application.Current.Resources["ActiveDisplayFont"] = standardFont;
                        Application.Current.Resources["HalloweenSpookyFont"] = standardFont;

                        if (_halloweenDictionary != null)
                        {
                            _halloweenDictionary["ActiveDisplayFont"] = standardFont;
                            _halloweenDictionary["HalloweenSpookyFont"] = standardFont;
                        }

                        if (Application.Current.MainWindow != null)
                        {
                            Application.Current.MainWindow.Resources.Remove("ActiveDisplayFont");
                            Application.Current.MainWindow.Resources.Remove("HalloweenSpookyFont");
                        }
                    }
                }
                catch (Exception fontEx)
                {
                    Log.Debug(fontEx, "Failed to register or assign spooky font.");
                }

                Log.Information("Halloween Theme activated.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error applying Halloween theme resources.");
            }
        }

        private void RevertToStandardTheme()
        {
            try
            {
                List<ResourceDictionary> halloweenDicts =
                [
                    .. Application.Current.Resources.MergedDictionaries.Where(d =>
                        d.Source != null
                        && d.Source.OriginalString.Contains(
                            "HalloweenThemeDictionary.xaml",
                            StringComparison.OrdinalIgnoreCase
                        )
                    ),
                ];
                foreach (ResourceDictionary dict in halloweenDicts)
                {
                    _ = Application.Current.Resources.MergedDictionaries.Remove(dict);
                }

                if (_halloweenDictionary != null)
                {
                    _ = Application.Current.Resources.MergedDictionaries.Remove(
                        _halloweenDictionary
                    );
                }

                try
                {
                    if (Application.Current.MainWindow != null)
                    {
                        Application.Current.MainWindow.Resources.Remove("ActiveDisplayFont");
                        Application.Current.MainWindow.Resources.Remove("HalloweenSpookyFont");
                    }

                    FontFamily standardFont = new("Segoe UI");
                    Application.Current.Resources["ActiveDisplayFont"] = standardFont;
                    Application.Current.Resources["HalloweenSpookyFont"] = standardFont;
                }
                catch { }

                try
                {
                    FieldInfo? field = typeof(ApplicationThemeManager).GetField(
                        "_cachedApplicationTheme",
                        System.Reflection.BindingFlags.Static
                            | System.Reflection.BindingFlags.NonPublic
                    );
                    field?.SetValue(null, ApplicationTheme.Unknown);
                }
                catch { }

                ApplicationThemeManager.Apply(Settings.Theme, updateAccent: true);
                ApplicationAccentColorManager.ApplySystemAccent();

                Log.Information("Halloween Theme deactivated.");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error reverting Halloween theme resources.");
            }
        }
    }
}
