using System.Text.Json.Serialization;

namespace MirrorCast.Receiver.Models;

/// <summary>
/// Represents a JSON message transmitted across the secondary bidirectional control channel on port 59101.
/// </summary>
public class ControlMessage
{
    /// <summary>
    /// Gets or sets the message type (e.g. "ping", "pong", "pause", "resume", "config_update", "config_ack", "disconnect", "request_idr").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional UNIX timestamp in milliseconds for latency measurement.
    /// </summary>
    [JsonPropertyName("timestamp")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets optional requested stream width for dynamic resolution negotiation.
    /// </summary>
    [JsonPropertyName("width")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Width { get; set; }

    /// <summary>
    /// Gets or sets optional requested stream height for dynamic resolution negotiation.
    /// </summary>
    [JsonPropertyName("height")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Height { get; set; }

    /// <summary>
    /// Gets or sets optional target stream bitrate in bits per second.
    /// </summary>
    [JsonPropertyName("bitrate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Bitrate { get; set; }

    /// <summary>
    /// Gets or sets optional target frame rate.
    /// </summary>
    [JsonPropertyName("fps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Fps { get; set; }

    /// <summary>
    /// Gets or sets status string for acknowledgments (e.g. "accepted", "rejected", "ok").
    /// </summary>
    [JsonPropertyName("status")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets reason string for disconnects or errors.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; set; }

    /// <summary>
    /// Creates a pong reply matching a received ping message.
    /// </summary>
    public static ControlMessage CreatePong(long timestamp) => new()
    {
        Type = "pong",
        Timestamp = timestamp
    };

    /// <summary>
    /// Creates a request for the sender to immediately emit an IDR keyframe refresh.
    /// </summary>
    public static ControlMessage CreateRequestIdr() => new()
    {
        Type = "request_idr"
    };

    /// <summary>
    /// Creates a configuration acknowledgment message.
    /// </summary>
    public static ControlMessage CreateConfigAck(string status = "accepted") => new()
    {
        Type = "config_ack",
        Status = status
    };

    /// <summary>
    /// Creates a graceful disconnect notice.
    /// </summary>
    public static ControlMessage CreateDisconnect(string reason = "user_terminated") => new()
    {
        Type = "disconnect",
        Reason = reason
    };
}
