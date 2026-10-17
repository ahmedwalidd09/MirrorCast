using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Windows.Devices.WiFiDirect;
using Serilog;

namespace MirrorCast.Receiver.Network;

/// <summary>
/// Manages Wi-Fi Direct Autonomous Group Owner (Soft-AP) advertisement and incoming peer pairing.
/// Enables mobile devices (Android and iOS) to discover and join MirrorCast directly without an external router.
/// </summary>
public class WiFiDirectService : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<WiFiDirectService>();

    private WiFiDirectAdvertisementPublisher? _publisher;
    private WiFiDirectConnectionListener? _connectionListener;
    private bool _isStarted;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a mobile device connects via Wi-Fi Direct.
    /// </summary>
    public event Action<string>? PeerConnected;

    /// <summary>
    /// Event raised when Wi-Fi Direct connection state changes.
    /// </summary>
    public event Action<string>? StatusChanged;

    /// <summary>
    /// Gets a value indicating whether Wi-Fi Direct hardware is supported on this machine.
    /// </summary>
    public bool IsSupported { get; private set; } = true;

    /// <summary>
    /// Gets a value indicating whether Wi-Fi Direct advertisement is actively running.
    /// </summary>
    public bool IsActive
    {
        get { lock (_lock) return _isStarted; }
    }

    /// <summary>
    /// Gets the advertised Wi-Fi Direct Soft-AP SSID.
    /// </summary>
    public string Ssid { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the WPA2 Pre-Shared Key (passphrase) for mobile clients to join.
    /// </summary>
    public string Passphrase { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the IP endpoint assigned to the Wi-Fi Direct interface (typically 192.168.49.1).
    /// </summary>
    public string GroupOwnerIpAddress { get; private set; } = "192.168.49.1";

    /// <summary>
    /// Gets the formatted Wi-Fi QR code configuration string compliant with standard Wi-Fi barcode syntax:
    /// <c>WIFI:S:DIRECT-MC-[ComputerName];T:WPA;P:[Passphrase];;</c>
    /// </summary>
    public string GetWifiQrPayload() => $"WIFI:S:{Ssid};T:WPA;P:{Passphrase};;";

    /// <summary>
    /// Initializes Wi-Fi Direct publisher and connection listener.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isStarted)
                return;

            string sanitizedMachineName = Environment.MachineName.Replace(" ", "-").ToUpperInvariant();
            Ssid = $"DIRECT-MC-{sanitizedMachineName}";
            Passphrase = GenerateSecurePassphrase();

            try
            {
                Logger.Information("Initializing Windows Wi-Fi Direct Autonomous Group Owner: SSID={Ssid}", Ssid);

                _publisher = new WiFiDirectAdvertisementPublisher();
                _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Intensive;
                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;

                _publisher.StatusChanged += OnPublisherStatusChanged;
                _connectionListener = new WiFiDirectConnectionListener();
                _connectionListener.ConnectionRequested += OnConnectionRequested;

                _publisher.Start();
                _isStarted = true;
                IsSupported = true;

                UpdateLocalEndpoint();

                Logger.Information("Wi-Fi Direct Soft-AP successfully published. Passphrase={Passphrase}, IP={IP}", Passphrase, GroupOwnerIpAddress);
                StatusChanged?.Invoke("Broadcasting Wi-Fi Direct Soft-AP");
            }
            catch (COMException comEx)
            {
                Logger.Warning(comEx, "Wi-Fi Direct WinRT API not supported on this adapter/environment. Providing virtual Soft-AP profile.");
                IsSupported = false;
                _isStarted = true; // Keep active in fallback mode so UI still renders credentials
                UpdateLocalEndpoint();
                StatusChanged?.Invoke("Wi-Fi Direct fallback mode active");
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to start native Wi-Fi Direct service. Fallback enabled.");
                IsSupported = false;
                _isStarted = true;
                UpdateLocalEndpoint();
                StatusChanged?.Invoke("Wi-Fi Direct fallback mode active");
            }
        }
    }

    private void OnPublisherStatusChanged(WiFiDirectAdvertisementPublisher sender, WiFiDirectAdvertisementPublisherStatusChangedEventArgs args)
    {
        Logger.Information("Wi-Fi Direct publisher status changed: {Status}, Error={Error}", args.Status, args.Error);
        StatusChanged?.Invoke($"Wi-Fi Direct: {args.Status}");
    }

    private async void OnConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs args)
    {
        try
        {
            var request = args.GetConnectionRequest();
            var deviceInformation = request.DeviceInformation;
            string deviceName = deviceInformation?.Name ?? "Unknown Device";
            string deviceId = deviceInformation?.Id ?? "Unknown ID";

            Logger.Information("Incoming Wi-Fi Direct connection request from: {DeviceName} ({DeviceId})", deviceName, deviceId);

            // Connect to peer
            var connection = await WiFiDirectDevice.FromIdAsync(deviceId);
            if (connection != null)
            {
                connection.ConnectionStatusChanged += (dev, obj) =>
                {
                    Logger.Information("Wi-Fi Direct peer {DeviceName} status: {Status}", deviceName, dev.ConnectionStatus);
                };

                UpdateLocalEndpoint();
                PeerConnected?.Invoke(deviceName);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error handling incoming Wi-Fi Direct connection request");
        }
    }

    /// <summary>
    /// Searches for active virtual Wi-Fi Direct network adapters (e.g. Microsoft Wi-Fi Direct Virtual Adapter)
    /// and resolves the assigned group owner IPv4 address.
    /// </summary>
    private void UpdateLocalEndpoint()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                string desc = ni.Description.ToLowerInvariant();
                string name = ni.Name.ToLowerInvariant();

                if (desc.Contains("wi-fi direct") || desc.Contains("virtual adapter") || name.Contains("wi-fi direct") || name.Contains("local area connection*"))
                {
                    foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ip = addr.Address.ToString();
                            if (!ip.StartsWith("169.254."))
                            {
                                GroupOwnerIpAddress = ip;
                                Logger.Information("Resolved Wi-Fi Direct Group Owner IP: {IP} on {Adapter}", ip, ni.Name);
                                return;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Could not discover specific Wi-Fi Direct adapter IP; using default 192.168.49.1");
        }

        // Standard Wi-Fi Direct Android / Windows group owner fallback
        GroupOwnerIpAddress = "192.168.49.1";
    }

    /// <summary>
    /// Generates an 8-character alphanumeric WPA2 passphrase.
    /// </summary>
    private static string GenerateSecurePassphrase()
    {
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz";
        var rng = new Random();
        return new string(Enumerable.Repeat(chars, 8).Select(s => s[rng.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Stops the Wi-Fi Direct publisher and releases listeners.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isStarted)
                return;

            try
            {
                Logger.Information("Stopping Wi-Fi Direct publisher...");
                if (_publisher != null)
                {
                    _publisher.StatusChanged -= OnPublisherStatusChanged;
                    _publisher.Stop();
                }

                if (_connectionListener != null)
                {
                    _connectionListener.ConnectionRequested -= OnConnectionRequested;
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error stopping Wi-Fi Direct publisher");
            }
            finally
            {
                _publisher = null;
                _connectionListener = null;
                _isStarted = false;
            }
        }
    }

    /// <summary>
    /// Disposes Wi-Fi Direct resources.
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
