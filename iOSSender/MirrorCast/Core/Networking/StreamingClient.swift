//
//  StreamingClient.swift
//  MirrorCast
//
//  Handshake and Secondary Bidirectional Control Channel (Port 59101) client.
//

import Foundation
import Network
import UIKit

public protocol StreamingClientDelegate: AnyObject {
    func streamingClientDidConnect(_ client: StreamingClient)
    func streamingClient(_ client: StreamingClient, didUpdateRTT rttMs: Double)
    func streamingClient(_ client: StreamingClient, didFailWithError error: Error)
    func streamingClientDidDisconnect(_ client: StreamingClient)
}

public final class StreamingClient {
    public weak var delegate: StreamingClientDelegate?
    
    private var controlConnection: NWConnection?
    private let queue = DispatchQueue(label: "com.mirrorcast.control.queue", qos: .userInitiated)
    private var pingTimer: DispatchSourceTimer?
    private var isConnected: Bool = false
    
    public init() {}
    
    deinit {
        disconnect()
    }
    
    // MARK: - Handshake Verification (Primary Video Port 59100)
    
    /// Performs the initial handshake validation on port 59100 before initiating the broadcast
    public func performPreflightHandshake(
        host: String,
        port: Int,
        config: StreamConfig,
        mode: ConnectionMode,
        completion: @escaping (Result<HandshakeResponse, Error>) -> Void
    ) {
        let endpoint = NWEndpoint.hostPort(
            host: NWEndpoint.Host(host),
            port: NWEndpoint.Port(rawValue: UInt16(port)) ?? 59100
        )
        let tcpOptions = NWProtocolTCP.Options()
        tcpOptions.noDelay = true
        let params = NWParameters(tls: nil, tcp: tcpOptions)
        params.preferNoProxies = true
        
        let connection = NWConnection(to: endpoint, using: params)
        var hasCompleted = false
        
        connection.stateUpdateHandler = { [weak connection] state in
            guard let connection = connection, !hasCompleted else { return }
            
            switch state {
            case .ready:
                let dimensions = config.resolution.targetDimensions
                let request = HandshakeRequest(
                    device: UIDevice.current.name,
                    os: "ios",
                    width: dimensions.width,
                    height: dimensions.height,
                    fps: config.fps,
                    mode: mode.rawValue
                )
                
                guard let jsonData = try? JSONEncoder().encode(request),
                      var payload = String(data: jsonData, encoding: .utf8) else {
                    hasCompleted = true
                    connection.cancel()
                    completion(.failure(NSError(domain: "MirrorCast", code: -1, userInfo: [NSLocalizedDescriptionKey: "Encoding failed"])))
                    return
                }
                payload.append("\n")
                
                let sendData = payload.data(using: .utf8) ?? Data()
                connection.send(content: sendData, completion: .contentProcessed { error in
                    if let error = error {
                        hasCompleted = true
                        connection.cancel()
                        completion(.failure(error))
                        return
                    }
                    
                    // Receive handshake response line
                    connection.receive(minimumIncompleteLength: 1, maximumLength: 4096) { data, _, _, recvError in
                        guard !hasCompleted else { return }
                        hasCompleted = true
                        connection.cancel()
                        
                        if let recvError = recvError {
                            completion(.failure(recvError))
                            return
                        }
                        
                        guard let data = data,
                              let response = try? JSONDecoder().decode(HandshakeResponse.self, from: data) else {
                            completion(.failure(NSError(domain: "MirrorCast", code: -2, userInfo: [NSLocalizedDescriptionKey: "Invalid handshake response format"])))
                            return
                        }
                        
                        if response.isSuccess {
                            completion(.success(response))
                        } else {
                            let msg = response.message ?? "Handshake rejected by receiver."
                            completion(.failure(NSError(domain: "MirrorCast", code: -3, userInfo: [NSLocalizedDescriptionKey: msg])))
                        }
                    }
                })
                
            case .failed(let error):
                hasCompleted = true
                connection.cancel()
                completion(.failure(error))
                
            default:
                break
            }
        }
        
        connection.start(queue: queue)
        
        // Timeout preflight check after 5 seconds
        queue.asyncAfter(deadline: .now() + 5.0) { [weak connection] in
            if !hasCompleted {
                hasCompleted = true
                connection?.cancel()
                completion(.failure(NSError(domain: "MirrorCast", code: -4, userInfo: [NSLocalizedDescriptionKey: "Connection timed out"])))
            }
        }
    }
    
    // MARK: - Control Channel (Port 59101)
    
    public func connectControlChannel(host: String, port: Int = 59101) {
        disconnect()
        
        let endpoint = NWEndpoint.hostPort(
            host: NWEndpoint.Host(host),
            port: NWEndpoint.Port(rawValue: UInt16(port)) ?? 59101
        )
        let tcpOptions = NWProtocolTCP.Options()
        tcpOptions.noDelay = true
        let params = NWParameters(tls: nil, tcp: tcpOptions)
        
        let connection = NWConnection(to: endpoint, using: params)
        self.controlConnection = connection
        
        connection.stateUpdateHandler = { [weak self] state in
            guard let self = self else { return }
            switch state {
            case .ready:
                self.isConnected = true
                self.delegate?.streamingClientDidConnect(self)
                self.startPingLoop()
                self.receiveNextControlMessage()
            case .failed(let error):
                self.isConnected = false
                self.delegate?.streamingClient(self, didFailWithError: error)
            case .cancelled:
                self.isConnected = false
                self.delegate?.streamingClientDidDisconnect(self)
            default:
                break
            }
        }
        
        connection.start(queue: queue)
    }
    
    public func sendControlMessage(_ message: ControlMessage) {
        guard isConnected, let connection = controlConnection else { return }
        guard let jsonData = try? JSONEncoder().encode(message),
              var payload = String(data: jsonData, encoding: .utf8) else { return }
        payload.append("\n")
        
        let data = payload.data(using: .utf8) ?? Data()
        connection.send(content: data, completion: .idempotent)
    }
    
    public func sendPause() {
        sendControlMessage(ControlMessage.pause())
    }
    
    public func sendResume() {
        sendControlMessage(ControlMessage.resume())
    }
    
    public func sendConfigUpdate(width: Int, height: Int, bitrate: Int, fps: Int) {
        sendControlMessage(ControlMessage.configUpdate(width: width, height: height, bitrate: bitrate, fps: fps))
    }
    
    public func disconnect() {
        sendControlMessage(ControlMessage.disconnect())
        stopPingLoop()
        controlConnection?.cancel()
        controlConnection = nil
        isConnected = false
    }
    
    // MARK: - Ping / Pong Heartbeat
    
    private func startPingLoop() {
        stopPingLoop()
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now() + 2.0, repeating: 2.0)
        timer.setEventHandler { [weak self] in
            guard let self = self, self.isConnected else { return }
            self.sendControlMessage(ControlMessage.ping())
        }
        timer.resume()
        self.pingTimer = timer
    }
    
    private func stopPingLoop() {
        pingTimer?.cancel()
        pingTimer = nil
    }
    
    private func receiveNextControlMessage() {
        guard let connection = controlConnection, isConnected else { return }
        
        connection.receive(minimumIncompleteLength: 1, maximumLength: 8192) { [weak self] data, _, _, error in
            guard let self = self else { return }
            
            if let data = data, !data.isEmpty,
               let text = String(data: data, encoding: .utf8) {
                let lines = text.components(separatedBy: "\n").filter { !$0.trimmingCharacters(in: .whitespaces).isEmpty }
                for line in lines {
                    if let lineData = line.data(using: .utf8),
                       let msg = try? JSONDecoder().decode(ControlMessage.self, from: lineData) {
                        self.handleIncomingControlMessage(msg)
                    }
                }
            }
            
            if error == nil && self.isConnected {
                self.receiveNextControlMessage()
            }
        }
    }
    
    private func handleIncomingControlMessage(_ message: ControlMessage) {
        if message.type == "pong", let sentTime = message.timestamp {
            let nowMs = Int64(Date().timeIntervalSince1970 * 1000)
            let rtt = max(Double(nowMs - sentTime), 1.0)
            DispatchQueue.main.async {
                self.delegate?.streamingClient(self, didUpdateRTT: rtt)
            }
        }
    }
}
