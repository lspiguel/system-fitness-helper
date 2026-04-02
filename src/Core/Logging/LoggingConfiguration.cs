using Serilog;
using Serilog.Events;

namespace SystemFitnessHelper.Logging;

/// <summary>
/// Bootstraps the Serilog logger used throughout the application.
/// Writes structured logs to both the console and a rolling daily file under
/// <c>%APPDATA%\SystemFitnessHelper\logs\</c>; verbosity switches between Information and Debug.
/// </summary>
public static class LoggingConfiguration
{
    public static void Configure(bool verbose = false)
    {
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SystemFitnessHelper", "logs", "sfh-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
            .WriteTo.Console(
                outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
