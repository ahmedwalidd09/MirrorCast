//
//  DiscoveredReceiver.swift
//  MirrorCast
//
//  Model representing a discovered or manually configured Windows receiver.
//

import Foundation

public enum ConnectionMode: String, Codable, CaseIterable, CustomStringConvertible {
    case wifi = "wifi"
    case wifiDirect = "wifi-direct"
    
    public var description: String {
        switch self {
        case .wifi: return "Same Wi-Fi"
        case .wifiDirect: return "Wi-Fi Direct"
        }
    }
}

public struct DiscoveredReceiver: Identifiable, Codable, Equatable, Hashable {
    public let id: UUID
    public var name: String
    public var host: String
    public var port: Int
    public var controlPort: Int
    public var mode: ConnectionMode
    public var isManual: Bool
    public var lastSeen: Date
    
    public init(
        id: UUID = UUID(),
        name: String,
        host: String,
        port: Int = 59100,
        controlPort: Int = 59101,
        mode: ConnectionMode = .wifi,
        isManual: Bool = false,
        lastSeen: Date = Date()
    ) {
        self.id = id
        self.name = name
        self.host = host
        self.port = port
        self.controlPort = controlPort
        self.mode = mode
        self.isManual = isManual
        self.lastSeen = lastSeen
    }
}
