using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MirrorCast.Receiver.Models;
using Serilog;

namespace MirrorCast.Receiver.Network;

/// <summary>
/// Secondary bidirectional control channel server running on port 59101.
/// Handles keepalive heartbeats (ping/pong), pause/resume signals, dynamic configuration, and IDR keyframe requests.
/// </summary>
public class ControlSocketServer : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<ControlSocketServer>();

    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private TcpClient? _activeClient;
    private NetworkStream? _activeStream;
    private readonly object _lock = new();

    /// <summary>
    /// Event raised when mobile client pauses mirroring.
    /// </summary>
    public event Action? StreamPaused;

    /// <summary>
    /// Event raised when mobile client resumes mirroring.
    /// </summary>
    public event Action? StreamResumed;

    /// <summary>
    /// Event raised when mobile client updates stream configuration (width, height, bitrate, fps).
    /// </summary>
    public event Action<ControlMessage>? ConfigUpdated;

    /// <summary>
    /// Event raised when mobile client requests disconnection.
    /// </summary>
    public event Action<string?>? DisconnectRequested;

    /// <summary>
    /// Gets a value indicating whether an active control client is connected.
    /// </summary>
    public bool IsConnected
    {
        get { lock (_lock) return _activeClient != null && _activeClient.Connected; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ControlSocketServer"/> class.
    /// </summary>
    /// <param name="port">The control channel port (default 59101).</param>
    public ControlSocketServer(int port = 59101)
    {
        _port = port;
    }

    /// <summary>
    /// Starts the control channel TCP listener.
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
                _listener.Start(5);

                Logger.Information("ControlSocketServer listening on 0.0.0.0:{Port}", _port);
                _ = Task.Run(() => AcceptControlClientsAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to start ControlSocketServer on port {Port}", _port);
                throw;
            }
        }
    }

    private async Task AcceptControlClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(ct);
                client.NoDelay = true;

                Logger.Information("Control socket client connected from {RemoteEndPoint}", client.Client.RemoteEndPoint);

                lock (_lock)
                {
                    _activeStream?.Close();
                    _activeClient?.Close();
                    _activeClient = client;
                    _activeStream = client.GetStream();
                }

                _ = Task.Run(() => ProcessControlMessagesAsync(client, client.GetStream(), ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    Logger.Warning(ex, "Error accepting control socket connection");
                    await Task.Delay(200, ct);
                }
            }
        }
    }

    private async Task ProcessControlMessagesAsync(TcpClient client, NetworkStream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        try
        {
            while (!ct.IsCancellationRequested && client.Connected)
            {
                string? line = await reader.ReadLineAsync(ct);
                if (line == null)
                {
                    Logger.Information("Control socket client disconnected.");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                await HandleControlMessageAsync(line, stream, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (IOException)
        {
            Logger.Information("Control socket closed by peer.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error processing control socket message");
        }
        finally
        {
            lock (_lock)
            {
                if (_activeClient == client)
                {
                    _activeClient = null;
                    _activeStream = null;
                }
            }
        }
    }

    private async Task HandleControlMessageAsync(string json, NetworkStream stream, CancellationToken ct)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<ControlMessage>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (msg == null) return;

            Logger.Debug("Control message received: {Type}", msg.Type);

            switch (msg.Type.ToLowerInvariant())
            {
                case "ping":
                    long ts = msg.Timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var pong = ControlMessage.CreatePong(ts);
                    await SendMessageInternalAsync(pong, stream, ct);
                    break;

                case "pause":
                    Logger.Information("Control: Stream pause requested by sender.");
                    StreamPaused?.Invoke();
                    break;

                case "resume":
                    Logger.Information("Control: Stream resume requested by sender.");
                    StreamResumed?.Invoke();
                    break;

                case "config_update":
                    Logger.Information("Control: Stream config update: {Width}x{Height} @ {Bitrate}bps", msg.Width, msg.Height, msg.Bitrate);
                    ConfigUpdated?.Invoke(msg);
                    var ack = ControlMessage.CreateConfigAck();
                    await SendMessageInternalAsync(ack, stream, ct);
                    break;

                case "disconnect":
                    Logger.Information("Control: Disconnect requested by sender: {Reason}", msg.Reason);
                    DisconnectRequested?.Invoke(msg.Reason);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to parse or process control message: {Json}", json);
        }
    }

    /// <summary>
    /// Sends an out-of-band request to the mobile sender to force an immediate IDR keyframe refresh.
    /// </summary>
    public async Task RequestIdrKeyframeAsync()
    {
        Logger.Information("Sending request_idr to mobile sender via control socket...");
        await SendMessageAsync(ControlMessage.CreateRequestIdr());
    }

    /// <summary>
    /// Sends a disconnect signal to the mobile sender.
    /// </summary>
    public async Task SendDisconnectAsync(string reason = "user_terminated")
    {
        await SendMessageAsync(ControlMessage.CreateDisconnect(reason));
    }

    /// <summary>
    /// Sends a control message to the currently connected client.
    /// </summary>
    public async Task SendMessageAsync(ControlMessage message)
    {
        NetworkStream? stream;
        lock (_lock)
        {
            stream = _activeStream;
        }

        if (stream != null && stream.CanWrite)
        {
            try
            {
                await SendMessageInternalAsync(message, stream, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to send control message: {Type}", message.Type);
            }
        }
    }

    private static async Task SendMessageInternalAsync(ControlMessage message, NetworkStream stream, CancellationToken ct)
    {
        string json = JsonSerializer.Serialize(message) + "\n";
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    /// <summary>
    /// Stops the control socket listener.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            try
            {
                _activeStream?.Close();
                _activeClient?.Close();
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error closing control socket listener");
            }
            finally
            {
                _listener = null;
                _activeClient = null;
                _activeStream = null;
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the control socket server.
    /// </summary>
    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
