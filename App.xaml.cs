using Serilog;
using System.IO;
using System.Windows;
using TextureSwapper.Core;

namespace TextureSwapper
{
    public partial class App : Application
    {
        public static string CurrentLogFilePath { get; private set; } = string.Empty;

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
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("Application Closing...");
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }
}
