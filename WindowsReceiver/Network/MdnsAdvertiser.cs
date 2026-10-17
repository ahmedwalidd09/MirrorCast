using System.Net;
using Makaretu.Dns;
using Serilog;

namespace MirrorCast.Receiver.Network;

/// <summary>
/// Advertises the MirrorCast receiver on the local Wi-Fi network using mDNS / DNS-SD (Zeroconf).
/// Registers the service type <c>_screenmirror._tcp.local</c> on port 59100 with required metadata.
/// </summary>
public class MdnsAdvertiser : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<MdnsAdvertiser>();

    private const string ServiceType = "_screenmirror._tcp";
    private readonly int _port;
    private MulticastService? _multicastService;
    private ServiceDiscovery? _serviceDiscovery;
    private ServiceProfile? _serviceProfile;
    private bool _isAdvertising;
    private readonly object _lock = new();

    /// <summary>
    /// Gets a value indicating whether mDNS advertising is currently active.
    /// </summary>
    public bool IsAdvertising
    {
        get { lock (_lock) return _isAdvertising; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MdnsAdvertiser"/> class.
    /// </summary>
    /// <param name="port">The primary TCP streaming port (default 59100).</param>
    public MdnsAdvertiser(int port = 59100)
    {
        _port = port;
    }

    /// <summary>
    /// Starts broadcasting mDNS / DNS-SD announcements on all available network adapters.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isAdvertising)
                return;

            try
            {
                Logger.Information("Starting mDNS discovery advertiser for {ServiceType} on port {Port}...", ServiceType, _port);

                string instanceName = $"{Environment.MachineName}";
                _multicastService = new MulticastService();
                _serviceDiscovery = new ServiceDiscovery(_multicastService);

                _serviceProfile = new ServiceProfile(instanceName, ServiceType, (ushort)_port);
                _serviceProfile.AddProperty("app", "MirrorCast");
                _serviceProfile.AddProperty("v", "1");
                _serviceProfile.AddProperty("name", Environment.MachineName);
                _serviceProfile.AddProperty("fps", "60");
                _serviceProfile.AddProperty("direct", "false");

                _multicastService.Start();
                _serviceDiscovery.Advertise(_serviceProfile);

                _isAdvertising = true;
                Logger.Information("mDNS advertisement active: {InstanceName}.{ServiceType} on port {Port}", instanceName, ServiceType, _port);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start mDNS advertiser");
                Stop();
            }
        }
    }

    /// <summary>
    /// Stops advertising the mDNS service and releases multicast sockets.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isAdvertising)
                return;

            try
            {
                Logger.Information("Stopping mDNS advertiser...");
                if (_serviceDiscovery != null && _serviceProfile != null)
                {
                    _serviceDiscovery.Unadvertise(_serviceProfile);
                }

                _multicastService?.Stop();
                _serviceDiscovery?.Dispose();
                _multicastService?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error occurred while stopping mDNS advertiser");
            }
            finally
            {
                _serviceProfile = null;
                _serviceDiscovery = null;
                _multicastService = null;
                _isAdvertising = false;
            }
        }
    }

    /// <summary>
    /// Disposes all managed multicast and DNS resources.
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
