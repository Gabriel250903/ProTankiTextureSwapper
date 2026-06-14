using Serilog;
using System.IO;
using System.Windows;
using TextureSwapper.Core;

namespace TextureSwapper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Constants.LogsDir, Constants.AppLogFile);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
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
