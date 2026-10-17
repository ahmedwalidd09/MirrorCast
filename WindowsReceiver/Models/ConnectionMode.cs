namespace MirrorCast.Receiver.Models;

/// <summary>
/// Specifies the active wireless connection mode used to connect the mobile sender to the Windows receiver.
/// </summary>
public enum ConnectionMode
{
    /// <summary>
    /// Both devices are connected to the same infrastructure Wi-Fi router / local network. Discovery via mDNS.
    /// </summary>
    SameWiFi,

    /// <summary>
    /// Direct peer-to-peer connection without an external router. Receiver acts as Wi-Fi Direct Autonomous Group Owner.
    /// </summary>
    WiFiDirect
}
