# MirrorCast Wire Protocol Specification (v1.0)

This document defines the network transport, discovery mechanisms, framing formats, and control signaling for the **MirrorCast** screen mirroring system.

---

## 1. Network Architecture & Ports

MirrorCast operates entirely over local wireless networks (Standard Wi-Fi LAN or Wi-Fi Direct Soft-AP). No internet connection or cloud relay is ever utilized.

| Port | Protocol | Purpose | Direction |
|---|---|---|---|
| **59100** | TCP | Primary Video & Handshake Stream | Sender → Receiver |
| **59101** | TCP | Secondary Bidirectional Control Channel | Sender ⇄ Receiver |
| **5353** | UDP | mDNS / DNS-SD Multicast Discovery | Multicast (224.0.0.251 / FF02::FB) |

---

## 2. Discovery Mechanisms

### 2.1 Mode 1: Infrastructure Wi-Fi (mDNS / Zeroconf)
- **Service Type:** `_screenmirror._tcp.local`
- **Port:** `59100`
- **Domain:** `local.`
- **TXT Records:**
  - `app`: `MirrorCast`
  - `v`: `1`
  - `name`: Human-readable receiver name (e.g., `DESKTOP-WIN11`)
  - `fps`: `60`
  - `direct`: `false`

### 2.2 Mode 2: Wi-Fi Direct (P2P Soft-AP)
When no router is available, the Windows receiver establishes a Wi-Fi Direct group acting as the Autonomous Group Owner (Soft-AP).
- **Default SSID Format:** `DIRECT-MC-[ComputerName]` (e.g. `DIRECT-MC-DESKTOP`)
- **Security:** WPA2-PSK (AES)
- **Credentials Sharing:**
  - Android: `WifiP2pManager` discovers the P2P device and performs WPS-PBC / PIN pairing.
  - iOS: Displays a QR code encoding the standard Wi-Fi configuration format:
    ```
    WIFI:S:DIRECT-MC-DESKTOP;T:WPA;P:<Passphrase>;;
    ```
  - Receiver IP: Typically `192.168.49.1` or the IP assigned to the Wi-Fi Direct virtual adapter.

---

## 3. Primary Stream (Port 59100)

### 3.1 Connection & Handshake Phase
Immediately upon establishing the TCP socket on port `59100`, the sender MUST send a UTF-8 JSON handshake object terminated by a newline (`\n`):

#### Client Handshake Request:
```json
{
  "device": "Pixel 9 Pro",
  "os": "android",
  "width": 1080,
  "height": 2400,
  "fps": 60,
  "mode": "wifi"
}
```

#### Supported Fields:
- `device` *(string, required)*: Device model name.
- `os` *(string, required)*: `"android"` or `"ios"`.
- `width` *(integer, required)*: Initial stream pixel width (e.g., `1080`).
- `height` *(integer, required)*: Initial stream pixel height (e.g., `2400`).
- `fps` *(integer, required)*: Target frame rate (e.g., `60`).
- `mode` *(string, required)*: `"wifi"` (LAN) or `"wifi-direct"`.

#### Receiver Handshake Response:
The receiver validates the payload and responds with a JSON line:
```json
{
  "status": "ok",
  "protocolVersion": 1,
  "server": "MirrorCast-Windows",
  "controlPort": 59101
}
```
If rejected or incompatible, the receiver responds with:
```json
{
  "status": "error",
  "message": "Unsupported resolution or protocol version"
}
```
and terminates the socket.

---

### 3.2 Video Stream Framing
Once the handshake completes with `status: "ok"`, the stream switches immediately to raw **H.264 NAL Unit Framing**.

Each NAL (Network Abstraction Layer) unit is prepended with a **4-byte big-endian unsigned integer** specifying the payload length in bytes:

```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                 Length of NAL Unit (32-bit BE)                |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                                                               |
|                   H.264 NAL Unit Payload                      |
|                     (Length bytes)                            |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

#### Stream Sequence Rules:
1. **Initial SPS/PPS:** The stream MUST begin with Sequence Parameter Set (SPS, NAL type 7) and Picture Parameter Set (PPS, NAL type 8). These may be packed into separate length-prefixed frames or combined.
2. **Keyframes (IDR):** IDR slice (NAL type 5) must be generated at an interval no greater than 2.0 seconds.
3. **Non-IDR Frames:** Non-IDR slices (NAL type 1) follow for predictive frames.
4. **Annex B vs AVCC:** The payload contains the raw NAL unit (forbidden_zero_bit, nal_ref_idc, nal_unit_type followed by RBSP). The 4-byte length prefix replaces the 3/4-byte `0x000001` / `0x00000001` Annex B start codes, ensuring zero-copy parsing.

---

## 4. Control Channel (Port 59101)

The control channel is a bidirectional JSON-line socket used for real-time signaling without blocking the high-throughput video pipeline.

### 4.1 Keep-Alive / Heartbeat
Every 2 seconds, either endpoint may emit:
```json
{ "type": "ping", "timestamp": 1729200000123 }
```
The remote endpoint responds immediately with:
```json
{ "type": "pong", "timestamp": 1729200000123 }
```
If 3 consecutive pings fail without response (timeout 6s), the connection is declared dropped.

### 4.2 Stream Pause / Resume
When the user pauses mirroring on mobile:
```json
{ "type": "pause" }
```
The receiver freezes the last rendered frame and displays a subtle paused indicator. When resumed:
```json
{ "type": "resume" }
```
The sender forces an immediate IDR keyframe refresh.

### 4.3 Dynamic Quality / Resolution Negotiation
Mobile senders can negotiate bitrate and resolution on the fly:
```json
{
  "type": "config_update",
  "width": 1280,
  "height": 720,
  "bitrate": 4000000,
  "fps": 60
}
```
Receiver responds:
```json
{ "type": "config_ack", "status": "accepted" }
```

### 4.4 Disconnect Signal
Graceful disconnect notification:
```json
{ "type": "disconnect", "reason": "user_terminated" }
```

---

## 5. Error Handling & Recovery

1. **Packet Corruption:** If a length prefix exceeds `16,777,216` bytes (16 MB), it is treated as a stream desync. The receiver requests an immediate IDR keyframe via the control channel.
2. **TCP Reconnection:** If the socket drops unexpectedly, mobile senders attempt 3 reconnection attempts with exponential backoff (1s, 2s, 4s).
