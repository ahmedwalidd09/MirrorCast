//
//  StreamConfig.swift
//  MirrorCast
//
//  Encoder configuration parameters for video and network streaming.
//

import UIKit

public enum ResolutionPreset: String, Codable, CaseIterable, CustomStringConvertible {
    case p720 = "720p"
    case p1080 = "1080p"
    case native = "Native"
    
    public var description: String { rawValue }
    
    /// Computes target pixel dimensions matching device screen aspect ratio
    public var targetDimensions: (width: Int, height: Int) {
        let screen = UIScreen.main
        let nativeBounds = screen.nativeBounds
        let nativeWidth = Int(nativeBounds.width)
        let nativeHeight = Int(nativeBounds.height)
        let aspectRatio = Double(nativeHeight) / Double(nativeWidth)
        
        switch self {
        case .p720:
            let w = 720
            let h = Int(round(Double(w) * aspectRatio / 2.0) * 2.0) // even integer
            return (w, h)
        case .p1080:
            let w = 1080
            let h = Int(round(Double(w) * aspectRatio / 2.0) * 2.0)
            return (w, h)
        case .native:
            // Round to multiples of 16 for hardware encoder efficiency
            let w = (nativeWidth / 16) * 16
            let h = (nativeHeight / 16) * 16
            return (w, h)
        }
    }
}

public struct StreamConfig: Codable, Equatable {
    public var resolution: ResolutionPreset
    public var bitrateMbps: Double // 4.0 - 20.0
    public var fps: Int // 60 default
    public var keyFrameIntervalSeconds: Double // 2.0
    public var audioEnabled: Bool
    
    public init(
        resolution: ResolutionPreset = .p1080,
        bitrateMbps: Double = 8.0,
        fps: Int = 60,
        keyFrameIntervalSeconds: Double = 2.0,
        audioEnabled: Bool = true
    ) {
        self.resolution = resolution
        self.bitrateMbps = bitrateMbps
        self.fps = fps
        self.keyFrameIntervalSeconds = keyFrameIntervalSeconds
        self.audioEnabled = audioEnabled
    }
    
    public var bitrateBps: Int {
        Int(bitrateMbps * 1_000_000)
    }
}
