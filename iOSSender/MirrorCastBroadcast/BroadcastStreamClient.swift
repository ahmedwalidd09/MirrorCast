//
//  BroadcastStreamClient.swift
//  MirrorCastBroadcast
//
//  Direct TCP transport client over Network.framework (NWConnection) to Port 59100.
//  Executes protocol handshake and streams 4-byte BE length-prefixed NAL units.
//

import Foundation
import Network
import UIKit

public protocol BroadcastStreamClientDelegate: AnyObject {
    func broadcastStreamClientDidConnect(_ client: BroadcastStreamClient)
    func broadcastStreamClient(_ client: BroadcastStreamClient, didFailWithError error: Error)
    func broadcastStreamClientDidDisconnect(_ client: BroadcastStreamClient)
}

public final class BroadcastStreamClient {
    public weak var delegate: BroadcastStreamClientDelegate?
    
    private var connection: NWConnection?
    private let queue = DispatchQueue(label: "com.mirrorcast.broadcast.network", qos: .userInteractive)
    
    private var isConnected: Bool = false
    private var isHandshakeComplete: Bool = false
    private var startTime: Date?
    
    // Telemetry
    private var packetsSent: UInt64 = 0
    private var bytesSent: UInt64 = 0
    private var lastStatsUpdate: Date = Date()
    private var windowBytes: UInt64 = 0
    private var windowFrames: UInt64 = 0
    
    // Backpressure queue limit
    private var pendingSendCount: Int = 0
    private let maxPendingSends = 12
    
    public init() {}
    
    deinit {
        disconnect()
    }
    
    public func connect(
        host: String,
        port: Int = 59100,
        deviceName: String,
        width: Int,
        height: Int,
        fps: Int,
        mode: String
    ) {
        disconnect()
        
        let endpoint = NWEndpoint.hostPort(
            host: NWEndpoint.Host(host),
            port: NWEndpoint.Port(rawValue: UInt16(port)) ?? 59100
        )
        
        let tcpOptions = NWProtocolTCP.Options()
        tcpOptions.noDelay = true
        tcpOptions.enableKeepalive = true
        tcpOptions.keepaliveIdle = 2
        
        let params = NWParameters(tls: nil, tcp: tcpOptions)
        params.preferNoProxies = true
        
        let conn = NWConnection(to: endpoint, using: params)
        self.connection = conn
        
        conn.stateUpdateHandler = { [weak self, weak conn] state in
            guard let self = self, let conn = conn else { return }
            
            switch state {
            case .ready:
                self.isConnected = true
                self.startTime = Date()
                self.sendHandshake(
                    connection: conn,
                    deviceName: deviceName,
                    width: width,
                    height: height,
                    fps: fps,
                    mode: mode
                )
                
            case .failed(let error):
                self.handleDisconnection(error: error)
                
            case .cancelled:
                self.handleDisconnection(error: nil)
                
            default:
                break
            }
        }
        
        conn.start(queue: queue)
    }
    
    public func sendNALU(packet: Data, isKeyFrame: Bool) {
        guard isConnected && isHandshakeComplete, let conn = connection else { return }
        
        // Congestion backpressure: if network buffer is backlogged, drop non-keyframes to maintain ultra-low latency
        if pendingSendCount > maxPendingSends && !isKeyFrame {
            return
        }
        
        pendingSendCount += 1
        
        conn.send(content: packet, completion: .contentProcessed { [weak self] error in
            guard let self = self else { return }
            self.queue.async {
                self.pendingSendCount = max(0, self.pendingSendCount - 1)
                
                if let error = error {
                    self.handleDisconnection(error: error)
                    return
                }
                
                self.packetsSent += 1
                self.bytesSent += UInt64(packet.count)
                self.windowBytes += UInt64(packet.count)
                self.windowFrames += 1
                
                self.checkStatsUpdate()
            }
        })
    }
    
    public func disconnect() {
        queue.async { [weak self] in
            guard let self = self else { return }
            self.connection?.cancel()
            self.connection = nil
            self.isConnected = false
            self.isHandshakeComplete = false
            self.pendingSendCount = 0
        }
    }
    
    // MARK: - Handshake Execution
    
    private func sendHandshake(
        connection: NWConnection,
        deviceName: String,
        width: Int,
        height: Int,
        fps: Int,
        mode: String
    ) {
        let handshakeObj: [String: Any] = [
            "device": deviceName,
            "os": "ios",
            "width": width,
            "height": height,
            "fps": fps,
            "mode": mode
        ]
        
        guard let jsonData = try? JSONSerialization.data(withJSONObject: handshakeObj),
              var jsonString = String(data: jsonData, encoding: .utf8) else {
            handleDisconnection(error: NSError(domain: "BroadcastClient", code: -1, userInfo: [NSLocalizedDescriptionKey: "Handshake JSON serialization failed"]))
            return
        }
        jsonString.append("\n")
        
        let payload = jsonString.data(using: .utf8) ?? Data()
        connection.send(content: payload, completion: .contentProcessed { [weak self] error in
            guard let self = self else { return }
            if let error = error {
                self.handleDisconnection(error: error)
                return
            }
            
            // Await Handshake Response
            connection.receive(minimumIncompleteLength: 1, maximumLength: 2048) { [weak self] data, _, _, recvError in
                guard let self = self else { return }
                
                if let recvError = recvError {
                    self.handleDisconnection(error: recvError)
                    return
                }
                
                guard let data = data,
                      let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any],
                      let status = json["status"] as? String,
                      status.lowercased() == "ok" else {
                    let err = NSError(domain: "BroadcastClient", code: -2, userInfo: [NSLocalizedDescriptionKey: "Handshake rejected or malformed"])
                    self.handleDisconnection(error: err)
                    return
                }
                
                self.isHandshakeComplete = true
                self.delegate?.broadcastStreamClientDidConnect(self)
            }
        })
    }
    
    private func handleDisconnection(error: Error?) {
        isConnected = false
        isHandshakeComplete = false
        connection?.cancel()
        connection = nil
        
        if let error = error {
            delegate?.broadcastStreamClient(self, didFailWithError: error)
        } else {
            delegate?.broadcastStreamClientDidDisconnect(self)
        }
    }
    
    private func checkStatsUpdate() {
        let now = Date()
        let elapsed = now.timeIntervalSince(lastStatsUpdate)
        guard elapsed >= 1.0 else { return }
        
        let fps = Double(windowFrames) / elapsed
        let bitrateMbps = (Double(windowBytes) * 8.0) / (elapsed * 1_000_000.0)
        let totalDuration = now.timeIntervalSince(startTime ?? now)
        
        let resText = "\(SharedConfig.shared.streamWidth)x\(SharedConfig.shared.streamHeight)"
        SharedConfig.shared.updateStats(
            resolution: resText,
            fps: fps,
            bitrateMbps: bitrateMbps,
            packets: packetsSent,
            bytes: bytesSent,
            duration: totalDuration
        )
        
        windowBytes = 0
        windowFrames = 0
        lastStatsUpdate = now
    }
}
