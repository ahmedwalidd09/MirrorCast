using System.Buffers.Binary;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MirrorCast.Receiver.Models;
using Serilog;

namespace MirrorCast.Receiver.Network;

/// <summary>
/// Primary high-throughput TCP server on port 59100.
/// Handles initial JSON client handshake, validates device parameters, and streams raw length-prefixed H.264 NAL units.
/// </summary>
public class TcpStreamServer : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TcpStreamServer>();

    private const int MaxNalUnitLength = 16 * 1024 * 1024; // 16 MB desync threshold
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private TcpClient? _currentClient;
    private NetworkStream? _currentStream;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when a client successfully completes the handshake negotiation.
    /// </summary>
    public event Action<DeviceInfo>? DeviceConnected;

    /// <summary>
    /// Event raised when a raw NAL unit is successfully extracted from the incoming TCP stream.
    /// </summary>
    public event Action<byte[]>? NalUnitReceived;

    /// <summary>
    /// Event raised when the connected client disconnects or the TCP socket drops.
    /// </summary>
    public event Action? DeviceDisconnected;

    /// <summary>
    /// Event raised when stream desynchronization or corrupt length headers are detected.
    /// </summary>
    public event Action? StreamDesyncDetected;

    /// <summary>
    /// Gets a value indicating whether a client is currently connected and streaming.
    /// </summary>
    public bool IsClientConnected { get; private set; }

    /// <summary>
    /// Gets the connected device's metadata, or null if no device is connected.
    /// </summary>
    public DeviceInfo? CurrentDevice { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TcpStreamServer"/> class.
    /// </summary>
    /// <param name="port">The primary streaming port (default 59100).</param>
    public TcpStreamServer(int port = 59100)
    {
        _port = port;
    }

    /// <summary>
    /// Starts the TCP listener on 0.0.0.0 and enters the client accept loop.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_listener != null)
                return;

            try
            {
                _cts = new CancellationTokenSource();
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start(10);

                Logger.Information("TcpStreamServer listening on 0.0.0.0:{Port}", _port);
                _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start TcpStreamServer on port {Port}", _port);
                throw;
            }
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(ct);
                ConfigureSocket(client);

                Logger.Information("Incoming TCP connection from {RemoteEndPoint}", client.Client.RemoteEndPoint);

                // If a client is already connected, disconnect old one to prioritize new session
                DisconnectCurrentClient();

                _currentClient = client;
                _currentStream = client.GetStream();

                _ = Task.Run(() => HandleClientSessionAsync(client, _currentStream, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    Logger.Warning(ex, "Exception in TcpStreamServer accept loop");
                    await Task.Delay(200, ct);
                }
            }
        }
    }

    private static void ConfigureSocket(TcpClient client)
    {
        client.NoDelay = true; // Disable Nagle's algorithm for sub-10ms latency
        client.ReceiveBufferSize = 2 * 1024 * 1024; // 2 MB buffer
        client.SendBufferSize = 64 * 1024;
        client.ReceiveTimeout = 10000; // 10s receive timeout
    }

    private async Task HandleClientSessionAsync(TcpClient client, NetworkStream stream, CancellationToken ct)
    {
        try
        {
            // Step 1: Perform Handshake
            DeviceInfo? deviceInfo = await ReadHandshakeAsync(stream, ct);
            if (deviceInfo == null)
            {
                Logger.Warning("Handshake failed or was invalid; closing connection.");
                byte[] errorResp = Encoding.UTF8.GetBytes("{\"status\":\"error\",\"message\":\"Invalid handshake payload\"}\n");
                await stream.WriteAsync(errorResp, ct);
                return;
            }

            CurrentDevice = deviceInfo;
            IsClientConnected = true;

            // Respond with OK handshake
            string okResponse = JsonSerializer.Serialize(new
            {
                status = "ok",
                protocolVersion = 1,
                server = "MirrorCast-Windows",
                controlPort = 59101
            }) + "\n";

            byte[] okBytes = Encoding.UTF8.GetBytes(okResponse);
            await stream.WriteAsync(okBytes, ct);
            await stream.FlushAsync(ct);

            Logger.Information("Handshake accepted for device: {DeviceInfo}", deviceInfo);
            DeviceConnected?.Invoke(deviceInfo);

            // Step 2: Stream raw NAL units
            await StreamNalUnitsAsync(stream, ct);
        }
        catch (OperationCanceledException)
        {
            // Server shutting down
        }
        catch (IOException ioEx)
        {
            Logger.Information("Client stream disconnected: {Message}", ioEx.Message);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Unexpected error during client stream session");
        }
        finally
        {
            DisconnectCurrentClient();
        }
    }

    private static async Task<DeviceInfo?> ReadHandshakeAsync(NetworkStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        string? handshakeLine = await reader.ReadLineAsync(ct);
        if (string.IsNullOrWhiteSpace(handshakeLine))
        {
            return null;
        }

        try
        {
            Logger.Debug("Received handshake JSON: {Handshake}", handshakeLine);
            return JsonSerializer.Deserialize<DeviceInfo>(handshakeLine, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse handshake JSON: {Handshake}", handshakeLine);
            return null;
        }
    }

    private async Task StreamNalUnitsAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] lengthHeader = new byte[4];

        while (!ct.IsCancellationRequested && stream.CanRead)
        {
            // Read 4-byte big-endian length prefix
            int headerRead = await ReadExactAsync(stream, lengthHeader, 0, 4, ct);
            if (headerRead < 4)
            {
                Logger.Information("End of stream reached on video socket.");
                break;
            }

            uint nalLength = BinaryPrimitives.ReadUInt32BigEndian(lengthHeader);

            // Sanity check length prefix
            if (nalLength == 0)
            {
                continue;
            }

            if (nalLength > MaxNalUnitLength)
            {
                Logger.Error("Detected corrupt NAL unit length: {Length} bytes (exceeds {Max} limit). Desync detected!", nalLength, MaxNalUnitLength);
                StreamDesyncDetected?.Invoke();
                break;
            }

            // Read the NAL unit payload
            byte[] nalBuffer = new byte[nalLength];
            int payloadRead = await ReadExactAsync(stream, nalBuffer, 0, (int)nalLength, ct);
            if (payloadRead < nalLength)
            {
                Logger.Warning("Truncated NAL unit received (expected {Expected}, got {Actual}).", nalLength, payloadRead);
                break;
            }

            // Dispatch NAL unit
            NalUnitReceived?.Invoke(nalBuffer);
        }
    }

    private static async Task<int> ReadExactAsync(NetworkStream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), ct);
            if (bytesRead == 0)
            {
                return totalRead; // End of stream
            }
            totalRead += bytesRead;
        }
        return totalRead;
    }

    /// <summary>
    /// Closes any active client connection gracefully and notifies listeners.
    /// </summary>
    public void DisconnectCurrentClient()
    {
        lock (_lock)
        {
            if (_currentClient != null || IsClientConnected)
            {
                Logger.Information("Disconnecting current client session...");
                try
                {
                    _currentStream?.Close();
                    _currentClient?.Close();
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex, "Error closing client connection");
                }
                finally
                {
                    _currentStream = null;
                    _currentClient = null;
                    IsClientConnected = false;
                    CurrentDevice = null;
                    DeviceDisconnected?.Invoke();
                }
            }
        }
    }

    /// <summary>
    /// Stops the TCP listener and terminates active streaming connections.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            DisconnectCurrentClient();

            try
            {
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error stopping TcpStreamServer listener");
            }
            finally
            {
                _listener = null;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the TCP stream server.
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
