using System.Text.Json.Serialization;

namespace MirrorCast.Receiver.Models;

/// <summary>
/// Represents device and stream metadata received during the initial client handshake on port 59100.
/// </summary>
public class DeviceInfo
{
    /// <summary>
    /// Gets or sets the human-readable device model name (e.g. "Pixel 9 Pro", "iPhone 15 Pro").
    /// </summary>
    [JsonPropertyName("device")]
    public string Device { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operating system identifier ("android" or "ios").
    /// </summary>
    [JsonPropertyName("os")]
    public string Os { get; set; } = "android";

    /// <summary>
    /// Gets or sets the stream pixel width.
    /// </summary>
    [JsonPropertyName("width")]
    public int Width { get; set; } = 1920;

    /// <summary>
    /// Gets or sets the stream pixel height.
    /// </summary>
    [JsonPropertyName("height")]
    public int Height { get; set; } = 1080;

    /// <summary>
    /// Gets or sets the target streaming frame rate (typically 60).
    /// </summary>
    [JsonPropertyName("fps")]
    public int Fps { get; set; } = 60;

    /// <summary>
    /// Gets or sets the connection mode string ("wifi" or "wifi-direct").
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "wifi";

    /// <summary>
    /// Returns true if the connecting device is running iOS.
    /// </summary>
    [JsonIgnore]
    public bool IsIos => string.Equals(Os, "ios", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if the connecting device is running Android.
    /// </summary>
    [JsonIgnore]
    public bool IsAndroid => string.Equals(Os, "android", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns a formatted display name and resolution summary.
    /// </summary>
    public override string ToString() => $"{Device} ({Os.ToUpperInvariant()}) - {Width}x{Height} @ {Fps}fps [{Mode}]";
}
