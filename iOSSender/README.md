# MirrorCast iOS Sender

Production-grade iOS Screen Mirroring sender application built in Swift and SwiftUI, featuring a ReplayKit Broadcast Upload Extension with hardware-accelerated H.264 VideoToolbox encoding.

---

## Architecture Overview

```
                      +---------------------------------------+
                      |         MirrorCast Main App           |
                      |  - SwiftUI Dark UI (Section 6 tokens) |
                      |  - mDNS / Bonjour Discovery (_screen) |
                      |  - NEHotspotConfiguration (Direct P2P)|
                      |  - AVCapture QR Scanner               |
                      |  - Control Channel Client (Port 59101)|
                      +-------------------+-------------------+
                                          |
                        App Group: group.com.mirrorcast.shared
                        Darwin Notifications IPC
                                          |
                                          v
                      +---------------------------------------+
                      |     MirrorCastBroadcast Extension     |
                      |  - RPBroadcastSampleHandler           |
                      |  - VideoToolbox H.264 Encoder (60fps) |
                      |  - AVCC 4-byte BE Length NAL Units    |
                      |  - Direct NWConnection TCP (Port 59100|
                      |  - Memory < 15MB (Strict 50MB limit)  |
                      +-------------------+-------------------+
                                          |
                                          | TCP Stream
                                          v
                      +---------------------------------------+
                      |      MirrorCast Windows Receiver      |
                      |  - Port 59100: H.264 Video Stream     |
                      |  - Port 59101: Bidirectional Control  |
                      +---------------------------------------+
```

---

## Directory Structure

```
iOSSender/
├── MirrorCast.xcodeproj/
│   └── project.pbxproj                     # Configured for MirrorCast & MirrorCastBroadcast
├── MirrorCast/
│   ├── App/
│   │   ├── MirrorCastApp.swift             # App entry point with dark navigation theme
│   │   └── AppState.swift                  # Central state, Darwin notification coordination
│   ├── Core/
│   │   ├── Models/
│   │   │   ├── DiscoveredReceiver.swift    # Receiver model (host, port, mode, status)
│   │   │   ├── StreamConfig.swift          # Encoder settings (720p/1080p/Native, bitrate)
│   │   │   ├── StreamStats.swift           # Live telemetry (resolution, fps, bitrate, rtt)
│   │   │   └── Handshake.swift             # Wire protocol v1.0 JSON handshake and control schemas
│   │   ├── Networking/
│   │   │   ├── SharedConfig.swift          # App Group UserDefaults and Darwin Notifications
│   │   │   ├── BonjourDiscoveryManager.swift # NWBrowser for _screenmirror._tcp services
│   │   │   ├── HotspotManager.swift        # Programmatic Wi-Fi Direct connection
│   │   │   └── StreamingClient.swift       # Preflight handshake & secondary control channel
│   │   └── Theme/
│   │       ├── ColorTheme.swift            # Section 6 design tokens (#000000, #0D0D0D, #30D158, etc.)
│   │       ├── Typography.swift            # Fonts & card styling modifiers
│   │       └── Components/
│   │           ├── CustomSegmentedControl.swift # Dark segmented switcher
│   │           ├── CustomSlider.swift      # Bitrate slider with 20pt circular thumb
│   │           └── BroadcastPickerRepresentable.swift # RPSystemBroadcastPickerView wrapper
│   ├── Views/
│   │   ├── Home/
│   │   │   ├── HomeView.swift              # Discovery, receiver rows, broadcast trigger
│   │   │   ├── ReceiverRowView.swift       # #0D0D0D row with custom chevron
│   │   │   └── ManualConnectSheet.swift    # Manual IP:port connect dialog
│   │   ├── QR/
│   │   │   └── QRCodeScannerView.swift     # Camera QR scanner for Wi-Fi Direct credentials
│   │   ├── Streaming/
│   │   │   └── StreamingView.swift         # Live dashboard: pulsing green dot, stats, pause/stop
│   │   └── Settings/
│   │       └── SettingsView.swift          # Presets, framerate, audio toggle, diagnostics
│   └── Resources/
│       ├── Info.plist                      # Camera, Local Network permissions, Bonjour services
│       ├── MirrorCast.entitlements         # App Group & HotspotConfiguration
│       └── Assets.xcassets/                # AccentColor & AppIcon catalogs
├── MirrorCastBroadcast/
│   ├── SampleHandler.swift                 # Subclass of RPBroadcastSampleHandler
│   ├── VideoEncoder.swift                  # Hardware VTCompressionSession H.264 compressor
│   ├── AudioEncoder.swift                  # AAC-LC audio compression forwarder
│   ├── BroadcastStreamClient.swift         # Direct TCP transport to Port 59100
│   ├── Info.plist                          # Broadcast upload extension configuration
│   └── MirrorCastBroadcast.entitlements    # App Group entitlement
├── XCODE_SETUP.md                          # Detailed macOS/Xcode setup, signing, & deployment instructions
└── README.md                               # Architecture and documentation
```

---

## Wire Protocol Compliance

This implementation strictly implements the **MirrorCast Wire Protocol Specification (v1.0)**:
1. **Initial Handshake (Port 59100):**
   ```json
   {
     "device": "iPhone 15 Pro",
     "os": "ios",
     "width": 1080,
     "height": 1920,
     "fps": 60,
     "mode": "wifi"
   }
   ```
2. **Video Framing:**
   - Pre-pends 4-byte big-endian unsigned integer length prefix to each NAL unit.
   - Extracts and transmits SPS and PPS before each IDR keyframe.
   - Forces IDR keyframes every 2.0 seconds and immediately upon resume.
3. **Control Channel (Port 59101):**
   - Keep-alive `ping` / `pong` emitted every 2 seconds with timestamp for real-time RTT computation.
   - Real-time `pause`, `resume`, `config_update`, and `disconnect` control signaling.
