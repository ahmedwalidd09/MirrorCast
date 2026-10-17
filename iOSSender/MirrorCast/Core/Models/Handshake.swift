//
//  Handshake.swift
//  MirrorCast
//
//  JSON Handshake and Control Channel schemas strictly matching PROTOCOL.md v1.0.
//

import Foundation

public struct HandshakeRequest: Codable {
    public let device: String
    public let os: String
    public let width: Int
    public let height: Int
    public let fps: Int
    public let mode: String
    
    public init(
        device: String,
        os: String = "ios",
        width: Int,
        height: Int,
        fps: Int = 60,
        mode: String = "wifi"
    ) {
        self.device = device
        self.os = os
        self.width = width
        self.height = height
        self.fps = fps
        self.mode = mode
    }
}

public struct HandshakeResponse: Codable {
    public let status: String
    public let protocolVersion: Int?
    public let server: String?
    public let controlPort: Int?
    public let message: String?
    
    public var isSuccess: Bool {
        status.lowercased() == "ok"
    }
}

public struct ControlMessage: Codable {
    public let type: String
    public let timestamp: Int64?
    public let width: Int?
    public let height: Int?
    public let bitrate: Int?
    public let fps: Int?
    public let status: String?
    public let reason: String?
    
    public init(
        type: String,
        timestamp: Int64? = nil,
        width: Int? = nil,
        height: Int? = nil,
        bitrate: Int? = nil,
        fps: Int? = nil,
        status: String? = nil,
        reason: String? = nil
    ) {
        self.type = type
        self.timestamp = timestamp
        self.width = width
        self.height = height
        self.bitrate = bitrate
        self.fps = fps
        self.status = status
        self.reason = reason
    }
    
    public static func ping() -> ControlMessage {
        ControlMessage(type: "ping", timestamp: Int64(Date().timeIntervalSince1970 * 1000))
    }
    
    public static func pong(timestamp: Int64) -> ControlMessage {
        ControlMessage(type: "pong", timestamp: timestamp)
    }
    
    public static func pause() -> ControlMessage {
        ControlMessage(type: "pause")
    }
    
    public static func resume() -> ControlMessage {
        ControlMessage(type: "resume")
    }
    
    public static func configUpdate(width: Int, height: Int, bitrate: Int, fps: Int) -> ControlMessage {
        ControlMessage(type: "config_update", width: width, height: height, bitrate: bitrate, fps: fps)
    }
    
    public static func disconnect(reason: String = "user_terminated") -> ControlMessage {
        ControlMessage(type: "disconnect", reason: reason)
    }
}
