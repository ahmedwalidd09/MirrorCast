using System.Diagnostics;
using System.IO;
using Serilog;

namespace MirrorCast.Receiver.Network;

/// <summary>
/// Manages the native AirPlay receiver engine (uxplay-windows) for seamless iOS screen mirroring.
/// Allows any iPhone, iPad, or Mac to mirror directly via Control Center without installing an app on the Apple device.
/// </summary>
public class AirPlayManager : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<AirPlayManager>();

    private Process? _engineProcess;
    private bool _isDisposed;

    /// <summary>
    /// Event fired when the AirPlay engine status changes.
    /// </summary>
    public event Action<bool, string>? StatusChanged;

    /// <summary>
    /// Gets a value indicating whether the AirPlay engine is currently running.
    /// </summary>
    public bool IsRunning => _engineProcess != null && !_engineProcess.HasExited;

    /// <summary>
    /// Gets the advertised AirPlay receiver name.
    /// </summary>
    public string ReceiverName { get; } = "MirrorCast";

    /// <summary>
    /// Starts the native AirPlay engine if it is not already running.
    /// </summary>
    public void Start()
    {
        if (IsRunning)
        {
            Logger.Information("AirPlay engine is already running.");
            StatusChanged?.Invoke(true, "Active");
            return;
        }

        try
        {
            EnsureConfiguration();

            string? enginePath = FindEngineExecutable();
            if (string.IsNullOrEmpty(enginePath) || !File.Exists(enginePath))
            {
                Logger.Warning("AirPlay engine executable (uxplay-windows.exe) not found.");
                StatusChanged?.Invoke(false, "Engine binary missing");
                return;
            }

            // Check if another instance of uxplay-windows is already running
            Process[] existing = Process.GetProcessesByName("uxplay-windows");
            if (existing.Length > 0)
            {
                Logger.Information("Detected existing uxplay-windows process (PID {Pid}). Attaching to active session.", existing[0].Id);
                _engineProcess = existing[0];
                StatusChanged?.Invoke(true, "Active");
                return;
            }

            string workingDir = Path.GetDirectoryName(enginePath)!;
            var startInfo = new ProcessStartInfo
            {
                FileName = enginePath,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            _engineProcess = Process.Start(startInfo);
            if (_engineProcess != null)
            {
                Logger.Information("AirPlay receiver engine started successfully (PID {Pid}, Name '{Name}').", _engineProcess.Id, ReceiverName);
                StatusChanged?.Invoke(true, "Active");
            }
            else
            {
                Logger.Error("Failed to launch AirPlay engine process.");
                StatusChanged?.Invoke(false, "Failed to start");
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception while starting AirPlay engine.");
            StatusChanged?.Invoke(false, "Error: " + ex.Message);
        }
    }

    /// <summary>
    /// Restarts the AirPlay engine process.
    /// </summary>
    public void Restart()
    {
        Stop();
        Thread.Sleep(500);
        Start();
    }

    /// <summary>
    /// Stops the AirPlay engine process.
    /// </summary>
    public void Stop()
    {
        try
        {
            if (_engineProcess != null && !_engineProcess.HasExited)
            {
                Logger.Information("Stopping AirPlay engine (PID {Pid})...", _engineProcess.Id);
                _engineProcess.Kill(true);
                _engineProcess.WaitForExit(2000);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error stopping AirPlay engine process.");
        }
        finally
        {
            _engineProcess = null;
            StatusChanged?.Invoke(false, "Stopped");
        }
    }

    /// <summary>
    /// Writes the arguments.txt configuration into %APPDATA%\leapbtw\uxplay-windows so that
    /// the AirPlay receiver is always named 'MirrorCast' without hostname suffix.
    /// </summary>
    private void EnsureConfiguration()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configDir = Path.Combine(appData, "leapbtw", "uxplay-windows");
            Directory.CreateDirectory(configDir);

            string configPath = Path.Combine(configDir, "arguments.txt");
            string expectedConfig = $"-n {ReceiverName} -nh";
            if (!File.Exists(configPath) || File.ReadAllText(configPath).Trim() != expectedConfig)
            {
                File.WriteAllText(configPath, expectedConfig);
                Logger.Information("Wrote AirPlay config: '{Config}' to {Path}", expectedConfig, configPath);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not write AirPlay engine configuration file.");
        }
    }

    private string? FindEngineExecutable()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDir, "AirPlayEngine", "uxplay-windows.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "AirPlayEngine", "uxplay-windows.exe"),
            Path.Combine(baseDir, "..", "AirPlayEngine", "uxplay-windows.exe"),
            @"C:\Users\moody\.gemini\antigravity\scratch\ScreenMirror\AirPlayEngine\uxplay-windows.exe"
        ];

        foreach (string candidate in candidates)
        {
            string fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
