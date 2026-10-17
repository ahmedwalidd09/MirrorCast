//
//  StreamStats.swift
//  MirrorCast
//
//  Live telemetry model for mirroring stream diagnostics.
//

import Foundation

public struct StreamStats: Codable, Equatable {
    public var resolutionText: String
    public var fps: Double
    public var bitrateMbps: Double
    public var packetsSent: UInt64
    public var bytesSent: UInt64
    public var duration: TimeInterval
    public var isPaused: Bool
    public var rttMs: Double
    
    public init(
        resolutionText: String = "1080p",
        fps: Double = 60.0,
        bitrateMbps: Double = 8.2,
        packetsSent: UInt64 = 0,
        bytesSent: UInt64 = 0,
        duration: TimeInterval = 0,
        isPaused: Bool = false,
        rttMs: Double = 12.0
    ) {
        self.resolutionText = resolutionText
        self.fps = fps
        self.bitrateMbps = bitrateMbps
        self.packetsSent = packetsSent
        self.bytesSent = bytesSent
        self.duration = duration
        self.isPaused = isPaused
        self.rttMs = rttMs
    }
    
    /// Formatted status string complying with Section 6: "resolution · 60 FPS · 8.2 Mbps"
    public var formattedSummary: String {
        let fpsInt = Int(round(fps))
        let bitrateStr = String(format: "%.1f Mbps", bitrateMbps)
        return "\(resolutionText) · \(fpsInt) FPS · \(bitrateStr)"
    }
    
    public var formattedDuration: String {
        let totalSeconds = Int(duration)
        let minutes = totalSeconds / 60
        let seconds = totalSeconds % 60
        return String(format: "%02d:%02d", minutes, seconds)
    }
}
