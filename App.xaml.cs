using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Services;
using TextureSwapper.Services.Composers;
using TextureSwapper.ViewModels;
using TextureSwapper.ViewModels.Editors;
using TextureSwapper.Views.Windows;

namespace TextureSwapper
{
    public partial class App : Application
    {
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveFontResourceEx(string lpFileName, uint fl, IntPtr pdv);

        private static string? _loadedFontPath;
        public static string CurrentLogFilePath { get; private set; } = string.Empty;
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
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
                    if (AddFontResourceEx(fontPath, 0x10, IntPtr.Zero) != 0)
                    {
                        _loadedFontPath = fontPath;
                    }
                }
            }
            catch { }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            CurrentLogFilePath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                Constants.LogsDir,
                $"app-log_{timestamp}.txt"
            );

            try
            {
                string? logDir = Path.GetDirectoryName(CurrentLogFilePath);
                if (!string.IsNullOrEmpty(logDir))
                {
                    _ = Directory.CreateDirectory(logDir);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to create log directory: {ex.Message}");
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(CurrentLogFilePath)
                .CreateLogger();

            Log.Information("Application Starting...");

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            try
            {
                ILocalizationService locService =
                    ServiceProvider.GetRequiredService<ILocalizationService>();
                ISettingsService settingsService =
                    ServiceProvider.GetRequiredService<ISettingsService>();
                Core.Models.AppSettings settings = settingsService.Load();
                locService.SetLanguage(
                    string.IsNullOrEmpty(settings.Language) ? "en" : settings.Language
                );
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize localization on startup.");
            }

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IUpdateService, UpdateService>();
            _ = services.AddSingleton<ITextureDownloadService, TextureDownloadService>();
            _ = services.AddSingleton<ISwapService, SwapService>();
            _ = services.AddSingleton<ICacheService, CacheService>();
            _ = services.AddSingleton<ISettingsService, SettingsService>();
            _ = services.AddSingleton<ISkinSyncService, SkinSyncService>();
            _ = services.AddSingleton<IWindowService, WindowService>();
            _ = services.AddSingleton<INotificationService, NotificationService>();
            _ = services.AddSingleton<IDeviceBanService, DeviceBanService>();
            _ = services.AddSingleton<ILocalizationService, LocalizationService>();
            _ = services.AddSingleton<IPremiumService, PremiumService>();
            _ = services.AddSingleton<ITaraService, TaraService>();
            _ = services.AddSingleton<ITextureComposer, GenericTextureComposer>();
            _ = services.AddSingleton<IFirebirdShotComposer, FirebirdShotComposer>();
            _ = services.AddSingleton<IFreezeShotComposer, FreezeShotComposer>();
            _ = services.AddSingleton<IRicochetShotComposer, RicochetShotComposer>();
            _ = services.AddSingleton<ITwinsShotComposer, TwinsShotComposer>();
            _ = services.AddSingleton<IRailgunShotComposer, RailgunShotComposer>();
            _ = services.AddSingleton<IHammerShotComposer, HammerShotComposer>();
            _ = services.AddSingleton<IIsidaShotComposer, IsidaShotComposer>();
            _ = services.AddSingleton<IShaftShotComposer, ShaftShotComposer>();
            _ = services.AddSingleton<IVulcanShotComposer, VulcanShotComposer>();
            _ = services.AddSingleton<IThunderShotComposer, ThunderShotComposer>();
            _ = services.AddSingleton<IDustEffectComposer, DustEffectComposer>();
            _ = services.AddSingleton<HalloweenThemeService>();
            _ = services.AddSingleton<HalloweenSoundService>();
            _ = services.AddSingleton<ISmokyShotComposer, SmokyShotComposer>();
            _ = services.AddSingleton<IParachuteComposer, ParachuteComposer>();
            _ = services.AddSingleton<IPropComposer, PropComposer>();
            _ = services.AddSingleton<IShotEffectComposerRegistry, ShotEffectComposerRegistry>();
            _ = services.AddSingleton<IPropArchiveService, PropArchiveService>();
            _ = services.AddSingleton<IMeshService, MeshService>();
            _ = services.AddSingleton<IAdminAuthService, AdminAuthService>();
            _ = services.AddSingleton<MainViewModel>();
            _ = services.AddSingleton<MainWindow>();

            _ = services.AddTransient<SettingsViewModel>();
            _ = services.AddTransient<ParachuteEditorViewModel>();
            _ = services.AddTransient<PropsEditorViewModel>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (!string.IsNullOrEmpty(_loadedFontPath))
            {
                try
                {
                    _ = RemoveFontResourceEx(_loadedFontPath, 0x10, IntPtr.Zero);
                }
                catch { }
            }
            Log.Information("Application Closing...");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
