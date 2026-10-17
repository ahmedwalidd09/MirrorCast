//
//  SharedConfig.swift
//  MirrorCast
//
//  Inter-process state coordination via App Group UserDefaults and Darwin Notifications.
//

import Foundation

public final class SharedConfig {
    public static let shared = SharedConfig()
    
    public static let appGroupIdentifier = "group.com.mirrorcast.shared"
    
    // Darwin notification identifiers
    public static let notificationBroadcastStarted = "com.mirrorcast.broadcast.started"
    public static let notificationBroadcastStopped = "com.mirrorcast.broadcast.stopped"
    public static let notificationBroadcastStats = "com.mirrorcast.broadcast.stats"
    public static let notificationStopRequest = "com.mirrorcast.broadcast.stopRequest"
    public static let notificationPauseToggled = "com.mirrorcast.broadcast.pauseToggled"
    
    private let userDefaults: UserDefaults?
    
    private init() {
        self.userDefaults = UserDefaults(suiteName: SharedConfig.appGroupIdentifier)
    }
    
    // MARK: - Receiver Configuration
    
    public var receiverHost: String {
        get { userDefaults?.string(forKey: "receiver_host") ?? "192.168.1.100" }
        set { userDefaults?.set(newValue, forKey: "receiver_host") }
    }
    
    public var receiverPort: Int {
        get {
            let port = userDefaults?.integer(forKey: "receiver_port") ?? 0
            return port > 0 ? port : 59100
        }
        set { userDefaults?.set(newValue, forKey: "receiver_port") }
    }
    
    public var receiverControlPort: Int {
        get {
            let port = userDefaults?.integer(forKey: "receiver_control_port") ?? 0
            return port > 0 ? port : 59101
        }
        set { userDefaults?.set(newValue, forKey: "receiver_control_port") }
    }
    
    public var receiverName: String {
        get { userDefaults?.string(forKey: "receiver_name") ?? "Windows Receiver" }
        set { userDefaults?.set(newValue, forKey: "receiver_name") }
    }
    
    public var connectionMode: String {
        get { userDefaults?.string(forKey: "connection_mode") ?? "wifi" }
        set { userDefaults?.set(newValue, forKey: "connection_mode") }
    }
    
    // MARK: - Stream Parameters
    
    public var streamWidth: Int {
        get {
            let w = userDefaults?.integer(forKey: "stream_width") ?? 0
            return w > 0 ? w : 1080
        }
        set { userDefaults?.set(newValue, forKey: "stream_width") }
    }
    
    public var streamHeight: Int {
        get {
            let h = userDefaults?.integer(forKey: "stream_height") ?? 0
            return h > 0 ? h : 1920
        }
        set { userDefaults?.set(newValue, forKey: "stream_height") }
    }
    
    public var streamFps: Int {
        get {
            let f = userDefaults?.integer(forKey: "stream_fps") ?? 0
            return f > 0 ? f : 60
        }
        set { userDefaults?.set(newValue, forKey: "stream_fps") }
    }
    
    public var streamBitrateBps: Int {
        get {
            let b = userDefaults?.integer(forKey: "stream_bitrate_bps") ?? 0
            return b > 0 ? b : 8_000_000
        }
        set { userDefaults?.set(newValue, forKey: "stream_bitrate_bps") }
    }
    
    public var isPaused: Bool {
        get { userDefaults?.bool(forKey: "is_paused") ?? false }
        set {
            userDefaults?.set(newValue, forKey: "is_paused")
            postDarwinNotification(SharedConfig.notificationPauseToggled)
        }
    }
    
    public var isBroadcasting: Bool {
        get { userDefaults?.bool(forKey: "is_broadcasting") ?? false }
        set { userDefaults?.set(newValue, forKey: "is_broadcasting") }
    }
    
    public var lastError: String? {
        get { userDefaults?.string(forKey: "last_error") }
        set { userDefaults?.set(newValue, forKey: "last_error") }
    }
    
    // MARK: - Live Stats
    
    public func updateStats(resolution: String, fps: Double, bitrateMbps: Double, packets: UInt64, bytes: UInt64, duration: TimeInterval) {
        userDefaults?.set(resolution, forKey: "stats_resolution")
        userDefaults?.set(fps, forKey: "stats_fps")
        userDefaults?.set(bitrateMbps, forKey: "stats_bitrate_mbps")
        userDefaults?.set(Int64(packets), forKey: "stats_packets")
        userDefaults?.set(Int64(bytes), forKey: "stats_bytes")
        userDefaults?.set(duration, forKey: "stats_duration")
        postDarwinNotification(SharedConfig.notificationBroadcastStats)
    }
    
    public func readStats() -> StreamStats {
        let res = userDefaults?.string(forKey: "stats_resolution") ?? "1080p"
        let fps = userDefaults?.double(forKey: "stats_fps") ?? 60.0
        let bitrate = userDefaults?.double(forKey: "stats_bitrate_mbps") ?? 8.2
        let packets = UInt64(max(0, userDefaults?.object(forKey: "stats_packets") as? Int64 ?? 0))
        let bytes = UInt64(max(0, userDefaults?.object(forKey: "stats_bytes") as? Int64 ?? 0))
        let duration = userDefaults?.double(forKey: "stats_duration") ?? 0
        let paused = isPaused
        return StreamStats(
            resolutionText: res,
            fps: fps,
            bitrateMbps: bitrate,
            packetsSent: packets,
            bytesSent: bytes,
            duration: duration,
            isPaused: paused
        )
    }
    
    // MARK: - Darwin Notifications
    
    public func postDarwinNotification(_ name: String) {
        let center = CFNotificationCenterGetDarwinNotifyCenter()
        CFNotificationCenterPostNotification(
            center,
            CFNotificationName(name as CFString),
            nil,
            nil,
            true
        )
    }
    
    public func observeDarwinNotification(_ name: String, callback: @escaping () -> Void) -> NSObjectProtocol {
        let center = CFNotificationCenterGetDarwinNotifyCenter()
        let observer = UnsafeRawPointer(Unmanaged.passUnretained(self).toOpaque())
        
        // Wrap notification registration
        NotificationCenter.default.addObserver(
            forName: NSNotification.Name(name),
            object: nil,
            queue: .main
        ) { _ in
            callback()
        }
        
        let cfCallback: CFNotificationCallback = { _, observer, cfName, _, _ in
            guard let cfName = cfName else { return }
            let nameStr = cfName.rawValue as String
            DispatchQueue.main.async {
                NotificationCenter.default.post(name: NSNotification.Name(nameStr), object: nil)
            }
        }
        
        CFNotificationCenterAddObserver(
            center,
            observer,
            cfCallback,
            name as CFString,
            nil,
            .deliverImmediately
        )
        
        return NSObject()
    }
}
