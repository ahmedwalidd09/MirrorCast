namespace MirrorCast.Receiver.Models;

/// <summary>
/// Defines the lifecycle states of the MirrorCast receiver application.
/// </summary>
public enum ReceiverState
{
    /// <summary>
    /// Initial startup state before discovery services and servers are initialized.
    /// </summary>
    Idle,

    /// <summary>
    /// Discovery services and TCP listeners are active, awaiting incoming connections from mobile devices.
    /// </summary>
    Listening,

    /// <summary>
    /// A mobile client has connected and completed the handshake negotiation.
    /// </summary>
    Connected,

    /// <summary>
    /// Video stream is active and frames are being decoded and rendered.
    /// </summary>
    Streaming,

    /// <summary>
    /// Stream is temporarily paused by mobile client control signal; last frame is frozen.
    /// </summary>
    Paused,

    /// <summary>
    /// Client disconnected or connection dropped; returning to listening state.
    /// </summary>
    Disconnected,

    /// <summary>
    /// An unrecoverable network, decoding, or initialization error occurred.
    /// </summary>
    Error
}
