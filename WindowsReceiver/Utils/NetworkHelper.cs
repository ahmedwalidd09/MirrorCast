using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Serilog;

namespace MirrorCast.Receiver.Utils;

/// <summary>
/// Helper utilities for discovering local network interfaces and primary IPv4 addresses.
/// </summary>
public static class NetworkHelper
{
    private static readonly ILogger Logger = Log.ForContext(typeof(NetworkHelper));

    /// <summary>
    /// Telemetry metrics for local network interfaces and wireless hardware.
    /// </summary>
    public class NetworkTelemetry
    {
        public string AdapterName { get; set; } = "Wi-Fi";
        public string Description { get; set; } = "Intel Wireless";
        public long LinkSpeedMbps { get; set; } = 300;
        public string IpAddress { get; set; } = "127.0.0.1";
        public bool IsWifiDirectActive { get; set; } = false;
        public string WifiDirectAdapterName { get; set; } = "";
    }

    /// <summary>
    /// Gathers live hardware and telemetry details from active network interfaces.
    /// </summary>
    public static NetworkTelemetry GetActiveTelemetry()
    {
        var telemetry = new NetworkTelemetry();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (ni.Description.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase) ||
                    ni.Name.Contains("Direct", StringComparison.OrdinalIgnoreCase))
                {
                    telemetry.IsWifiDirectActive = true;
                    telemetry.WifiDirectAdapterName = ni.Description;
                }
                else if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         ni.Name.Equals("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                         ni.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase))
                {
                    telemetry.AdapterName = ni.Name;
                    telemetry.Description = ni.Description;
                    telemetry.LinkSpeedMbps = ni.Speed / 1_000_000;

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !addr.Address.ToString().StartsWith("169.254."))
                        {
                            telemetry.IpAddress = addr.Address.ToString();
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to gather network telemetry.");
        }

        return telemetry;
    }

    /// <summary>
    /// Gets the primary active local IPv4 address of this machine.
    /// Prefers active Wi-Fi or Ethernet interfaces with default gateways.
    /// </summary>
    /// <returns>A string representation of the IPv4 address, or "127.0.0.1" if no network is active.</returns>
    public static string GetPrimaryLocalIpAddress()
    {
        try
        {
            // Approach 1: Query active unicast addresses on operational interfaces
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;

                // Skip loopback and tunnel
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                var ipProps = ni.GetIPProperties();
                // Check if this interface has at least one gateway
                if (ipProps.GatewayAddresses.Count == 0)
                    continue;

                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ipStr = addr.Address.ToString();
                        // Ignore APIPA / 169.254.x.x
                        if (!ipStr.StartsWith("169.254."))
                        {
                            Logger.Information("Discovered primary local IPv4 address: {IpAddress} on {InterfaceName}", ipStr, ni.Name);
                            return ipStr;
                        }
                    }
                }
            }

            // Approach 2: Use UDP routing check
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                string ip = endPoint.Address.ToString();
                Logger.Information("Discovered local IP via route probe: {IpAddress}", ip);
                return ip;
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to resolve primary local IP address; falling back to 127.0.0.1");
        }

        return "127.0.0.1";
    }

    /// <summary>
    /// Gets all active operational IPv4 addresses across all network interfaces.
    /// </summary>
    public static List<string> GetAllLocalIpAddresses()
    {
        var result = new List<string>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                    continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();
                        if (!ip.StartsWith("169.254.") && !result.Contains(ip))
                        {
                            result.Add(ip);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error querying all local IP addresses");
        }

        if (result.Count == 0)
        {
            result.Add("127.0.0.1");
        }
        return result;
    }
}
