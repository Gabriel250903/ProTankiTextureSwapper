using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.IO;
using System.Windows;
using TextureSwapper.Core;
using TextureSwapper.Services;
using TextureSwapper.Services.Interfaces;
using TextureSwapper.ViewModels;

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
            CurrentLogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.LogsDir, $"app-log_{timestamp}.txt");

            try
            {
                string? logDir = Path.GetDirectoryName(CurrentLogFilePath);
                if (!string.IsNullOrEmpty(logDir))
                {
                    _ = Directory.CreateDirectory(logDir);
                }
            }
            catch { }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(CurrentLogFilePath)
                .CreateLogger();

            Log.Information("Application Starting...");

            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            MainWindow mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddSingleton<IUpdateService, UpdateService>();
            _ = services.AddSingleton<ISwapService, SwapService>();
            _ = services.AddSingleton<ICacheService, CacheService>();
            _ = services.AddSingleton<ISettingsService, SettingsService>();
            _ = services.AddSingleton<ISkinSyncService, SkinSyncService>();
            _ = services.AddSingleton<IWindowService, WindowService>();

            _ = services.AddSingleton<MainViewModel>();
            _ = services.AddTransient<SettingsViewModel>();

            _ = services.AddSingleton<INotificationService, NotificationService>();

            _ = services.AddSingleton<MainWindow>();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application Closing...");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
