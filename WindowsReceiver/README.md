# MirrorCast Windows Receiver

High-performance, zero-configuration .NET 8 WPF receiver desktop client for MirrorCast. Receives real-time wireless screen streams from iOS and Android devices over local Wi-Fi or Wi-Fi Direct Soft-AP with sub-10ms latency and 60 FPS playback.

---

## Architecture Overview

```
                      +---------------------------------------+
                      |       Mobile Sender (iOS / Android)   |
                      +-------------------+-------------------+
                                          |
                        Port 59100 (TCP)  |  Port 59101 (TCP)
                     Raw NAL Video Stream |  Bidirectional Control
                                          v
+---------------------------------------------------------------------------------+
| MirrorCast Windows Receiver (.NET 8 WPF)                                         |
|                                                                                 |
|  [Network & Discovery]                                                          |
|   ├── MdnsAdvertiser: Broadcasts _screenmirror._tcp on 224.0.0.251:5353          |
|   ├── WiFiDirectService: WinRT Autonomous Group Owner Soft-AP publisher         |
|   ├── TcpStreamServer: Port 59100 handshake validator & AVCC framing stream     |
|   └── ControlSocketServer: Port 59101 ping/pong, pause/resume, IDR keyframe req |
|                                                                                 |
|  [Video Pipeline]                                                               |
|   ├── NalUnitParser: 4-byte BE length-prefixed & Annex B parsing + desync guard  |
|   ├── H264Decoder: Windows Media Foundation (MFT) hardware decoder & fallback   |
|   ├── FrameRenderer: Direct WriteableBitmap BackBuffer Lock/AddDirtyRect at 60fps|
|   └── FpsTracker: Rolling-window frame rate and latency analytics               |
|                                                                                 |
|  [UI & Design System (Strict Section 6 Compliance)]                            |
|   ├── Startup View: Same Wi-Fi vs Wi-Fi Direct tabs, pulsing dot, QRCoder QR     |
|   ├── Connected View: Zero-padding letterboxed video feed, bilinear scaling     |
|   ├── Floating Glass HUD: Translucent pill (rgba(13,13,13,0.72)), 3s auto-hide   |
|   ├── Custom Chrome: WindowStyle="None", 28px draggable bar, circle buttons    |
|   └── TrayIconManager: System tray integration with Show, Disconnect, Exit      |
+---------------------------------------------------------------------------------+
```

---

## Section 6 UI Design Tokens

| Token | Hex / Value | Usage |
|---|---|---|
| `--color-bg-base` | `#000000` | Deep black window canvas |
| `--color-bg-surface` | `#0D0D0D` | Cards, panels, titlebar |
| `--color-bg-elevated` | `#161616` | Pill buttons, inner badges |
| `--color-bg-hover` | `#1F1F1F` | Hover state background |
| `--color-bg-pressed` | `#2A2A2A` | Active click state |
| `--color-silver-bright`| `#C8C8CC` | Primary typography & logotype |
| `--color-silver-mid` | `#8E8E93` | Secondary text, labels |
| `--color-silver-dim` | `#48484A` | Dividers, subtle borders |
| `--color-accent` | `#E8E8ED` | Emphasized white highlights |
| `--color-stream-active`| `#30D158` | Live dot, FPS >= 55 |
| `--color-warn` | `#FF9F0A` | Paused status, FPS 30-54 |
| `--color-error` | `#FF453A` | Disconnect, close, FPS < 30 |
| `--color-transparent-glass`| `rgba(13,13,13,0.72)` | Floating HUD glass backdrop |

---

## Directory Structure

```
WindowsReceiver/
├── WindowsReceiver.csproj         # Project file targeting net8.0-windows10.0.22621.0
├── app.manifest                   # PerMonitorV2 DPI awareness & Windows 10/11 compatibility
├── App.xaml                       # App resources and color token dictionary
├── App.xaml.cs                    # Serilog logger setup and unhandled exception safety
├── MainWindow.xaml                # Custom chrome, startup tabs, video viewport, glass HUD
├── MainWindow.xaml.cs             # Pipeline coordination, auto-hide HUD, fullscreen, toast
├── Models/
│   ├── ConnectionMode.cs          # Enum for Same Wi-Fi vs Wi-Fi Direct
│   ├── ControlMessage.cs          # JSON model for secondary control signaling (port 59101)
│   ├── DeviceInfo.cs              # Handshake model for client metadata (port 59100)
│   ├── NalUnit.cs                 # H.264 NAL unit model and type classifier
│   └── ReceiverState.cs           # Lifecycle states of the receiver
├── Network/
│   ├── ControlSocketServer.cs     # Port 59101 ping/pong, pause/resume, IDR request server
│   ├── MdnsAdvertiser.cs          # Makaretu mDNS / DNS-SD advertiser for _screenmirror._tcp
│   ├── TcpStreamServer.cs         # Port 59100 handshake and raw video TCP server
│   └── WiFiDirectService.cs       # WinRT WiFiDirectAdvertisementPublisher Soft-AP service
├── Video/
│   ├── FpsTracker.cs              # Rolling-window frame rate tracker
│   ├── FrameRenderer.cs           # WriteableBitmap Lock/AddDirtyRect back-buffer renderer
│   ├── H264Decoder.cs             # Media Foundation MFT hardware decoder & NV12 conversion
│   ├── MediaFoundationNative.cs   # COM interop definitions for IMFTransform & IMFSample
│   └── NalUnitParser.cs           # 4-byte BE length prefix & Annex B NAL parser
├── UI/
│   ├── Controls/
│   │   ├── GlassToolbar.xaml      # Floating HUD with device info, FPS, screenshot, fullscreen
│   │   ├── GlassToolbar.xaml.cs   # HUD code-behind and auto-color transitions
│   │   ├── PulsingDot.xaml        # 8px pulsing status dot with 2000ms breathing animation
│   │   └── PulsingDot.xaml.cs     # PulsingDot code-behind
│   ├── Converters/
│   │   ├── BooleanToVisibilityConverter.cs
│   │   └── FpsToBrushConverter.cs # FPS color mapping (Green >=55, Yellow 30-54, Red <30)
│   └── TrayIconManager.cs         # Windows Taskbar Notification Tray integration
├── Utils/
│   ├── NetworkHelper.cs           # Primary IPv4 address discovery
│   ├── QrCodeGenerator.cs         # High-contrast QR codes rendered as frozen BitmapSource
│   └── ScreenshotHelper.cs        # Timestamped desktop PNG frame capture
└── Tests/
    └── NalUnitParserTests.cs      # Unit tests for AVCC framing, Annex B, and error resilience
```

---

## Building and Running

### Prerequisites
- Windows 10 (version 1809+) or Windows 11
- .NET 8 SDK (`net8.0-windows10.0.22621.0`)

### Build Release
```powershell
dotnet build WindowsReceiver.csproj -c Release
```

### Publish Single-File Executable
```powershell
dotnet publish WindowsReceiver.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Or execute the master build script from the root repository:
```powershell
..\build-windows.ps1 -Configuration Release
```
