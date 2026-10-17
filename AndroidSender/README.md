# MirrorCast — Android Sender

A high-performance, low-latency Android screen mirroring sender built with Kotlin and Jetpack Compose. Adheres strictly to the MirrorCast v1.0 Wire Protocol Specification and the Section 6 Design System.

---

## 1. System Architecture

```
                                  [ DisplayManager / MediaProjection ]
                                                   │
                                                   ▼
                                         [ VirtualDisplay ]
                                                   │
                                                   ▼ (Surface)
                                        [ Hardware MediaCodec ]
                                        (video/avc, Baseline)
                                                   │
                                                   ▼ (Annex B NAL Units)
                                          [ NalParser ]
                                  (4-byte Big-Endian Length Prefix)
                                                   │
                                ┌──────────────────┴──────────────────┐
                                ▼                                     ▼
                      [ Primary Stream: 59100 ]            [ Control Socket: 59101 ]
                     (TCP Handshake & NAL Frames)          (Keepalive, Pause, Dynamic Config)
```

### Core Components

1. **Screen Capture & Encoding (`com.mirrorcast.sender.capture`)**:
   - `ScreenCaptureService.kt`: Foreground service declaring `FOREGROUND_SERVICE_MEDIA_PROJECTION` with persistent notification controls (Pause, Resume, Stop).
   - `MediaEncoder.kt`: Hardware H.264 (`video/avc`) encoder configured for 60 FPS, 8 Mbps default bitrate, 2.0s keyframe interval, with dynamic bitrate and runtime sync-frame requests.
   - `NalParser.kt`: Splits Annex B byte streams into individual NAL units and frames them with 4-byte big-endian length headers per Section 3.2 of the protocol.
   - `RotationWatcher.kt`: Detects orientation changes dynamically and notifies the receiver via `config_update` control messages without terminating the TCP session.

2. **Wireless Discovery & Networking (`com.mirrorcast.sender.discovery`, `com.mirrorcast.sender.network`)**:
   - `NsdDiscoveryManager.kt`: Wi-Fi LAN discovery using native Android `NsdManager` for `_screenmirror._tcp` services.
   - `WifiDirectManager.kt`: Wi-Fi Direct (P2P) discovery and connection to Windows Autonomous Group Owners.
   - `StreamingClient.kt`: Primary video TCP client on port 59100 with handshake negotiation, telemetry tracking, and exponential backoff reconnection (1s, 2s, 4s).
   - `ControlChannelClient.kt`: Bidirectional control socket on port 59101 with 2s keepalive ping/pong, RTT tracking, pause/resume signaling, and timeout recovery.
   - `NetworkUtils.kt`: Acquires high-performance Wi-Fi locks, multicast locks, and partial wake locks to prevent battery saver throttling.

3. **Jetpack Compose UI (`com.mirrorcast.sender.ui`)**:
   - Built to strict Section 6 specifications:
     - Color tokens: `#000000`, `#0D0D0D`, `#161616`, `#1F1F1F`, `#C8C8CC`, `#8E8E93`, `#48484A`, `#30D158`, `#FF453A`.
     - Custom animated segmented pill controls (no standard tabs).
     - Custom `ReceiverCard` items with press-state scale animation (no Material ripple).
     - Custom Canvas pulsing dot searching indicator.
     - Live statistics row in monospaced caps (`1080×2400 · 60 FPS · 8.2 MBPS`).
     - Resolution quality scaling (720p, 1080p, Native).
     - Manual IP:port entry fallback.

---

## 2. Prerequisites & Build

- **Android Studio Ladybug (2024.2.1+)** or command-line Gradle with JDK 21.
- **Android SDK**: `compileSdk 35`, `targetSdk 35`, `minSdk 29` (Android 10+).

### Building APK

```bash
# Debug build
./gradlew assembleDebug

# Release build
./gradlew assembleRelease
```

---

## 3. Wire Protocol Alignment

| Feature | Android Sender Implementation | Protocol Specification |
|---|---|---|
| Primary Stream Port | `59100` | Port 59100 TCP |
| Secondary Control Port | `59101` | Port 59101 TCP |
| Handshake | JSON line `{"device":..., "os":"android", ...}\n` | Section 3.1 Handshake |
| Video Framing | 4-byte BE length prefix + raw NAL payload | Section 3.2 Video Stream Framing |
| Heartbeat | Ping/Pong every 2s, 6s timeout | Section 4.1 Keep-Alive |
| Dynamic Resolution | `config_update` message on rotation | Section 4.3 Config Update |
| Pause / Resume | Encoder freeze + `pause` / `resume` control message | Section 4.2 Stream Pause/Resume |
