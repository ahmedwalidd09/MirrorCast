//
//  AppState.swift
//  MirrorCast
//
//  Central application state manager and coordinator.
//

import SwiftUI
import Combine

public final class AppState: ObservableObject {
    @Published public var selectedMode: ConnectionMode = .wifi {
        didSet {
            if selectedMode == .wifi {
                bonjourManager.startBrowsing()
            } else {
                bonjourManager.stopBrowsing()
            }
        }
    }
    
    @Published public var selectedReceiver: DiscoveredReceiver?
    @Published public var streamConfig: StreamConfig = StreamConfig()
    @Published public var streamStats: StreamStats = StreamStats()
    
    @Published public var isStreaming: Bool = false
    @Published public var isConnecting: Bool = false
    @Published public var isPaused: Bool = false
    
    // Sheets
    @Published public var showSettingsSheet: Bool = false
    @Published public var showQRScannerSheet: Bool = false
    @Published public var showManualConnectSheet: Bool = false
    @Published public var alertMessage: String?
    
    public let bonjourManager = BonjourDiscoveryManager()
    public let hotspotManager = HotspotManager.shared
    public let controlClient = StreamingClient()
    
    private var statsTimer: AnyCancellable?
    private var cancellables = Set<AnyCancellable>()
    
    public init() {
        setupDarwinObservers()
        setupControlDelegate()
        
        // Start Bonjour discovery by default
        bonjourManager.startBrowsing()
    }
    
    deinit {
        bonjourManager.stopBrowsing()
        statsTimer?.cancel()
    }
    
    // MARK: - Preparation & Launch
    
    public func selectReceiver(_ receiver: DiscoveredReceiver) {
        self.selectedReceiver = receiver
        
        let dimensions = streamConfig.resolution.targetDimensions
        SharedConfig.shared.receiverHost = receiver.host
        SharedConfig.shared.receiverPort = receiver.port
        SharedConfig.shared.receiverControlPort = receiver.controlPort
        SharedConfig.shared.receiverName = receiver.name
        SharedConfig.shared.connectionMode = receiver.mode.rawValue
        SharedConfig.shared.streamWidth = dimensions.width
        SharedConfig.shared.streamHeight = dimensions.height
        SharedConfig.shared.streamFps = streamConfig.fps
        SharedConfig.shared.streamBitrateBps = streamConfig.bitrateBps
        SharedConfig.shared.isPaused = false
    }
    
    public func updateBitrate(_ bitrateMbps: Double) {
        streamConfig.bitrateMbps = bitrateMbps
        SharedConfig.shared.streamBitrateBps = streamConfig.bitrateBps
        
        if isStreaming, let receiver = selectedReceiver {
            let dimensions = streamConfig.resolution.targetDimensions
            controlClient.sendConfigUpdate(
                width: dimensions.width,
                height: dimensions.height,
                bitrate: streamConfig.bitrateBps,
                fps: streamConfig.fps
            )
        }
    }
    
    public func updateResolution(_ preset: ResolutionPreset) {
        streamConfig.resolution = preset
        let dimensions = preset.targetDimensions
        SharedConfig.shared.streamWidth = dimensions.width
        SharedConfig.shared.streamHeight = dimensions.height
        
        if isStreaming {
            controlClient.sendConfigUpdate(
                width: dimensions.width,
                height: dimensions.height,
                bitrate: streamConfig.bitrateBps,
                fps: streamConfig.fps
            )
        }
    }
    
    public func togglePause() {
        isPaused.toggle()
        SharedConfig.shared.isPaused = isPaused
        if isPaused {
            controlClient.sendPause()
        } else {
            controlClient.sendResume()
        }
    }
    
    public func stopStreaming() {
        SharedConfig.shared.postDarwinNotification(SharedConfig.notificationStopRequest)
        controlClient.disconnect()
        withAnimation {
            self.isStreaming = false
            self.isPaused = false
        }
        stopStatsPolling()
    }
    
    // MARK: - Hotspot Connection
    
    public func connectToHotspot(credentials: WiFiCredentials) {
        isConnecting = true
        hotspotManager.connect(credentials: credentials) { [weak self] result in
            guard let self = self else { return }
            self.isConnecting = false
            switch result {
            case .success(let ssid):
                // Wi-Fi Direct Autonomous Group Owner standard IP is 192.168.49.1
                let receiver = DiscoveredReceiver(
                    name: ssid,
                    host: "192.168.49.1",
                    port: 59100,
                    controlPort: 59101,
                    mode: .wifiDirect,
                    isManual: false
                )
                self.selectReceiver(receiver)
            case .failure(let error):
                self.alertMessage = "Wi-Fi Direct Connection Failed: \(error.localizedDescription)"
            }
        }
    }
    
    // MARK: - Manual Connection
    
    public func addManualReceiver(name: String, host: String, port: Int) {
        let receiver = DiscoveredReceiver(
            name: name.isEmpty ? host : name,
            host: host,
            port: port,
            controlPort: port + 1,
            mode: selectedMode,
            isManual: true
        )
        selectReceiver(receiver)
    }
    
    // MARK: - Darwin Notifications Setup
    
    private func setupDarwinObservers() {
        _ = SharedConfig.shared.observeDarwinNotification(SharedConfig.notificationBroadcastStarted) { [weak self] in
            DispatchQueue.main.async {
                guard let self = self else { return }
                withAnimation {
                    self.isStreaming = true
                    self.isPaused = false
                }
                if let receiver = self.selectedReceiver {
                    self.controlClient.connectControlChannel(host: receiver.host, port: receiver.controlPort)
                }
                self.startStatsPolling()
            }
        }
        
        _ = SharedConfig.shared.observeDarwinNotification(SharedConfig.notificationBroadcastStopped) { [weak self] in
            DispatchQueue.main.async {
                guard let self = self else { return }
                withAnimation {
                    self.isStreaming = false
                    self.isPaused = false
                }
                self.stopStatsPolling()
                if let error = SharedConfig.shared.lastError {
                    self.alertMessage = error
                    SharedConfig.shared.lastError = nil
                }
            }
        }
        
        _ = SharedConfig.shared.observeDarwinNotification(SharedConfig.notificationBroadcastStats) { [weak self] in
            DispatchQueue.main.async {
                guard let self = self else { return }
                self.streamStats = SharedConfig.shared.readStats()
            }
        }
    }
    
    private func setupControlDelegate() {
        controlClient.delegate = self
    }
    
    private func startStatsPolling() {
        statsTimer?.cancel()
        statsTimer = Timer.publish(every: 1.0, on: .main, in: .common)
            .autoconnect()
            .sink { [weak self] _ in
                guard let self = self else { return }
                var updated = SharedConfig.shared.readStats()
                updated.rttMs = self.streamStats.rttMs
                self.streamStats = updated
            }
    }
    
    private func stopStatsPolling() {
        statsTimer?.cancel()
        statsTimer = nil
    }
}

extension AppState: StreamingClientDelegate {
    public func streamingClientDidConnect(_ client: StreamingClient) {
        print("[AppState] Control channel connected successfully.")
    }
    
    public func streamingClient(_ client: StreamingClient, didUpdateRTT rttMs: Double) {
        self.streamStats.rttMs = rttMs
    }
    
    public func streamingClient(_ client: StreamingClient, didFailWithError error: Error) {
        print("[AppState] Control channel error: \(error.localizedDescription)")
    }
    
    public func streamingClientDidDisconnect(_ client: StreamingClient) {
        print("[AppState] Control channel disconnected.")
    }
}
