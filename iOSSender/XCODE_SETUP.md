# MirrorCast iOS Sender — Xcode Setup & Deployment Guide

This document provides complete, step-by-step instructions for opening, configuring, signing, and deploying the **MirrorCast iOS Sender** application and its **Broadcast Upload Extension** (`MirrorCastBroadcast`) using Xcode on macOS.

---

## 1. Prerequisites

- **Host Machine:** macOS Ventura (13.5+), Sonoma (14.0+), or Sequoia (15.0+)
- **IDE:** Xcode 15.0 or Xcode 16.0+ (with iOS 16.0+ SDK)
- **Target Device:** Physical iPhone or iPad running **iOS 16.0 or later**  
  *(Note: ReplayKit screen broadcast and camera QR scanning require physical hardware; iOS Simulator does not support system-wide screen capture or hardware camera input).*
- **Apple Developer Account:** Free personal team or Paid Apple Developer Program account.

---

## 2. Opening the Project

1. Copy or transfer the `iOSSender` folder to your macOS development machine.
2. Open Terminal and navigate into the folder:
   ```bash
   cd path/to/ScreenMirror/iOSSender
   ```
3. Open the Xcode project:
   ```bash
   open MirrorCast.xcodeproj
   ```
4. Verify that Xcode displays two native targets in the project navigator:
   - **`MirrorCast`** (Main iOS Application)
   - **`MirrorCastBroadcast`** (ReplayKit Broadcast Upload Extension)

---

## 3. Code Signing & Team Configuration

Because iOS requires extensions to be signed with the same developer team and matching provisioning profiles as the parent app, both targets must be signed.

### Step 3.1: Configure the Main App (`MirrorCast`)
1. In Xcode's project navigator, select the blue **`MirrorCast`** root project.
2. Select the **`MirrorCast`** target under **TARGETS**.
3. Go to the **Signing & Capabilities** tab.
4. Check **Automatically manage signing**.
5. In the **Team** dropdown, select your personal or organization Apple Developer Account.
6. If your team does not own `com.mirrorcast.app`, change the **Bundle Identifier** to a unique prefix, for example:
   ```
   com.<your-name-or-company>.mirrorcast
   ```

### Step 3.2: Configure the Broadcast Extension (`MirrorCastBroadcast`)
1. Still under **TARGETS**, select **`MirrorCastBroadcast`**.
2. Go to the **Signing & Capabilities** tab.
3. Check **Automatically manage signing**.
4. In the **Team** dropdown, select the **exact same team** chosen in Step 3.1.
5. If you customized the main bundle identifier, update the extension bundle identifier so that it is a direct child of the main app:
   ```
   com.<your-name-or-company>.mirrorcast.broadcast
   ```
   *(Note: If you change this bundle identifier, update `preferredExtensionBundleId` in `BroadcastPickerRepresentable.swift` and `AppState.swift` to match).*

---

## 4. Configuring App Groups & Capabilities

ReplayKit Broadcast Upload Extensions run in an independent sandbox process from the host app. They share configuration (target receiver IP, resolution, bitrate, connection mode) and live telemetry through an **App Group**.

### Step 4.1: Main App Capabilities (`MirrorCast`)
1. Select the **`MirrorCast`** target → **Signing & Capabilities**.
2. Verify **App Groups** is listed.
   - Click the **`+`** button under App Groups if needed and ensure:
     ```
     group.com.mirrorcast.shared
     ```
     is checked (or `group.com.<your-company>.mirrorcast.shared` if using custom identifiers).
   - If you modify the App Group identifier, update `SharedConfig.appGroupIdentifier` in `SharedConfig.swift`.
3. Verify **Hotspot Configuration** capability:
   - Click **`+ Capability`** at the top left of Signing & Capabilities if not already present.
   - Add **Hotspot Configuration** (this enables `NEHotspotConfigurationManager` to programmatically join Windows Wi-Fi Direct Soft-APs).

### Step 4.2: Broadcast Extension Capabilities (`MirrorCastBroadcast`)
1. Select the **`MirrorCastBroadcast`** target → **Signing & Capabilities**.
2. Verify **App Groups** is listed.
3. Ensure the exact same App Group (`group.com.mirrorcast.shared`) is checked with a blue checkmark.

---

## 5. Privacy & Permissions Verification

The project includes an explicit `Info.plist` with required privacy descriptions:

| Key | Value / Purpose |
|---|---|
| `NSCameraUsageDescription` | *"MirrorCast requires camera access to scan Wi-Fi Direct QR codes displayed on your Windows receiver."* |
| `NSLocalNetworkUsageDescription` | *"MirrorCast requires access to your local network to discover and stream your screen to Windows receivers."* |
| `NSBonjourServices` | Array containing `_screenmirror._tcp` for mDNS discovery. |

When launched for the first time, iOS will automatically prompt the user to approve:
1. **Local Network Access** (required for Bonjour and direct TCP streaming).
2. **Camera Access** (when tapping "Connect via QR Code").

---

## 6. Physical Device Deployment

1. Connect your physical iPhone or iPad to your Mac via USB or Wi-Fi sync.
2. In the Xcode scheme/destination toolbar at the top:
   - Select the **`MirrorCast`** scheme.
   - Select your connected physical iOS device as the run destination.
3. Click the **Run** button (or press `Cmd + R`).

### Enabling Developer Mode (iOS 16+)
If you haven't run developer apps on this device before:
1. On your iPhone/iPad, go to:  
   **Settings → Privacy & Security → Developer Mode**.
2. Toggle **Developer Mode** to **ON**.
3. Follow the prompt to restart your device and enter your passcode.

### Trusting Your Developer Profile (Free Accounts)
If Xcode builds and installs the app but warns that the developer is untrusted:
1. On your iPhone/iPad, go to:  
   **Settings → General → VPN & Device Management**.
2. Under **Developer App**, select your Apple ID email.
3. Tap **Trust "[Your Name/Email]"** and confirm.

---

## 7. Verifying Screen Mirroring

### Mode 1: Infrastructure Wi-Fi (Same Network)
1. Start the Windows receiver app on a PC connected to the same Wi-Fi router.
2. Open `MirrorCast` on your iPhone.
3. Ensure the top picker is set to **Same Wi-Fi**.
4. The Windows receiver will automatically appear in the list with a desktop display icon and its IP:port (`59100`).
5. Tap the receiver to select it.
6. Tap the **Start Screen Mirroring** button.
7. The ReplayKit system broadcast picker modal will appear with **MirrorCast Broadcast** selected.
8. Tap **Start Broadcast**. A 3-second countdown will tick, and screen mirroring will begin!
9. The app will transition to the live streaming dashboard showing:
   - Pulsing green live indicator dot (`#30D158`)
   - "Mirroring to [Receiver Name]"
   - Live telemetry: `1080p · 60 FPS · 8.2 Mbps`
   - Real-time latency (RTT), frames sent, and data transferred.

### Mode 2: Wi-Fi Direct (Routerless P2P Soft-AP)
1. In the Windows receiver app, switch to **Wi-Fi Direct** mode and click **Show QR Code**.
2. On your iPhone, select **Wi-Fi Direct** in the top segmented control.
3. Tap **Connect via QR Code**.
4. Point your camera at the Windows screen.
5. The app detects the `WIFI:S:DIRECT-MC-...;T:WPA;P:...;;` code and prompts:  
   *"MirrorCast wants to join Wi-Fi Network 'DIRECT-MC-...'?"*
6. Tap **Join**.
7. Once associated, the receiver is automatically selected. Tap **Start Screen Mirroring** to begin streaming directly over the high-speed P2P link!

---

## 8. Troubleshooting & Architecture Notes

### Memory Footprint in ReplayKit Broadcast Extension
Apple enforces an operating system memory limit of **50 MB** on Broadcast Upload Extensions. If an extension allocates more than 50 MB, iOS terminates the extension immediately (`EXC_RESOURCE RESOURCE_TYPE_MEMORY`).
- **Our Architecture:**  
  MirrorCast uses direct hardware zero-copy encoding with `VTCompressionSession` (H.264 Baseline Profile). `CMSampleBuffer` frames are encoded directly from GPU surface memory into Annex B / AVCC NAL units and streamed immediately via TCP without large in-memory buffers. Memory footprint consistently remains below **15 MB**.

### Windows Firewall
If the iOS app discovers the receiver but the connection times out:
- Ensure Windows Firewall allows inbound TCP connections on ports **59100** (video) and **59101** (control channel).
- In Windows PowerShell (Admin):
  ```powershell
  New-NetFirewallRule -DisplayName "MirrorCast Ports" -Direction Inbound -Protocol TCP -LocalPort 59100,59101 -Action Allow
  ```
