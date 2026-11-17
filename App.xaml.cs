using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Diagnostics;
using System.IO;
using System.Windows;
using TextureSwapper.Core;
using TextureSwapper.Core.Interfaces;
using TextureSwapper.Seasonal;
using TextureSwapper.Services;
using TextureSwapper.Services.Composers;
using TextureSwapper.ViewModels;
using TextureSwapper.ViewModels.Editors;
using TextureSwapper.Views.Windows;

namespace TextureSwapper
{
    public partial class App : Application
    {
        public static string CurrentLogFilePath { get; private set; } = string.Empty;
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Log.Fatal(ex, "Unhandled AppDomain exception (IsTerminating={IsTerminating})", args.IsTerminating);
                }
                Log.CloseAndFlush();
            };

            DispatcherUnhandledException += (_, args) =>
            {
                Log.Fatal(args.Exception, "Unhandled Dispatcher exception");
                Log.CloseAndFlush();
            };

            Log.Information("Application Starting...");

#if !DEBUG
            if (!Helpers.Utilities.IntegrityChecker.ValidateAssemblyIntegrity())
            {
                Log.Fatal("Startup binary integrity check failed.");
                MessageBox.Show(
                    "Application integrity verification failed. Please reinstall TextureSwapper from the official source.",
                    Constants.AppName,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Shutdown(-1);
                return;
            }
#endif

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            try
            {
                ILocalizationService locService =
                    ServiceProvider.GetRequiredService<ILocalizationService>();
                ISettingsService settingsService =
                    ServiceProvider.GetRequiredService<ISettingsService>();
                ICacheService cacheService =
                    ServiceProvider.GetRequiredService<ICacheService>();
                Core.Models.AppSettings settings = settingsService.Load();
                if (settings.EnablePackLogListener || settings.EnableProTankiPack)
                {
                    ServiceProvider.GetRequiredService<PackLogListener>().Start();
                }
                locService.SetLanguage(
                    string.IsNullOrEmpty(settings.Language) ? "en" : settings.Language
                );

                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                if (!settings.HasSelectedClient)
                {
                    bool isFlashDetected = cacheService.IsClientDetected(Core.Models.ClientType.Flash);
                    bool isHtml5Detected = cacheService.IsClientDetected(Core.Models.ClientType.Html5);

                    ClientSelectionDialog clientDialog = new(
                        settings.ActiveClient,
                        isFlashDetected,
                        isHtml5Detected
                    );
                    if (clientDialog.ShowDialog() == true)
                    {
                        settings.ActiveClient = clientDialog.SelectedClient;
                        settings.HasSelectedClient = clientDialog.RememberChoice;
                        settingsService.Save(settings);
                        Log.Information(
                            "User selected ProTanki client on launch: {Client} (Remember={Remember})",
                            settings.ActiveClient,
                            settings.HasSelectedClient
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to initialize startup client selection or localization.");
            }

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IUpdateService, UpdateService>();
            _ = services.AddSingleton<ITextureDownloadService, TextureDownloadService>();
            _ = services.AddSingleton<IBntArchiveService, BntArchiveService>();
            _ = services.AddSingleton<IHtml5CacheService, Html5CacheService>();
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
            _ = services.AddSingleton<SeasonalThemeService>();
            _ = services.AddSingleton<SeasonalAudioService>();
            _ = services.AddSingleton<ISmokyShotComposer, SmokyShotComposer>();
            _ = services.AddSingleton<IParachuteComposer, ParachuteComposer>();
            _ = services.AddSingleton<IPropComposer, PropComposer>();
            _ = services.AddSingleton<IShotEffectComposerRegistry, ShotEffectComposerRegistry>();
            _ = services.AddSingleton<IPropArchiveService, PropArchiveService>();
            _ = services.AddSingleton<IMeshService, MeshService>();
            _ = services.AddSingleton<IAdminAuthService, AdminAuthService>();
            _ = services.AddSingleton<PackLogListener>();
            _ = services.AddSingleton<MainViewModel>();
            _ = services.AddSingleton<MainWindow>();

            _ = services.AddTransient<SettingsViewModel>();
            _ = services.AddTransient<ParachuteEditorViewModel>();
            _ = services.AddTransient<PropsEditorViewModel>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                (ServiceProvider?.GetService(typeof(PackLogListener)) as PackLogListener)?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error("Failed to dispose PackLogListener on exit: {0}", ex.Message);
            }
            Log.Information("Application Closing...");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
