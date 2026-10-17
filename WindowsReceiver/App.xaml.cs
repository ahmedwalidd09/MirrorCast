using System.IO;
using System.Windows;
using Serilog;

namespace MirrorCast.Receiver;

/// <summary>
/// Main application entry point for the MirrorCast Windows Receiver.
/// Configures logging and global exception handling.
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Initializes logging and application event subscriptions.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);
        string logPath = Path.Combine(logDir, "receiver-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        Log.Information("==================================================");
        Log.Information(" MirrorCast Windows Receiver Starting Up (.NET 8)");
        Log.Information(" OS: {OSDescription}", System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        Log.Information("==================================================");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log.Fatal(args.ExceptionObject as Exception, "Unhandled AppDomain exception occurred.");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Error(args.Exception, "Unhandled Dispatcher exception caught.");
            args.Handled = true; // Prevent crash, keep app resilient
        };
    }

    /// <summary>
    /// Flushes log buffers when shutting down.
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("MirrorCast Receiver exiting.");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
