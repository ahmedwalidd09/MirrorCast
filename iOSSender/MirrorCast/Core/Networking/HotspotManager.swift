//
//  HotspotManager.swift
//  MirrorCast
//
//  Programmatic Wi-Fi Direct Soft-AP connection via NEHotspotConfigurationManager.
//

import Foundation
import NetworkExtension
import Combine

public struct WiFiCredentials {
    public let ssid: String
    public let password: String
    public let securityType: String
    
    public init(ssid: String, password: String, securityType: String = "WPA") {
        self.ssid = ssid
        self.password = password
        self.securityType = securityType
    }
}

public final class HotspotManager: ObservableObject {
    @Published public var isConnecting: Bool = false
    @Published public var connectedSSID: String?
    @Published public var lastError: String?
    
    public static let shared = HotspotManager()
    
    public init() {}
    
    /// Parses standard Wi-Fi QR code strings: "WIFI:S:DIRECT-MC-NAME;T:WPA;P:secret;;"
    public static func parseWiFiQRCode(_ rawString: String) -> WiFiCredentials? {
        guard rawString.hasPrefix("WIFI:") else { return nil }
        
        var ssid: String?
        var password: String?
        var type: String = "WPA"
        
        // Strip prefix
        let payload = String(rawString.dropFirst(5))
        let tokens = payload.components(separatedBy: ";")
        
        for token in tokens {
            if token.hasPrefix("S:") {
                ssid = String(token.dropFirst(2))
            } else if token.hasPrefix("P:") {
                password = String(token.dropFirst(2))
            } else if token.hasPrefix("T:") {
                type = String(token.dropFirst(2))
            }
        }
        
        guard let validSSID = ssid, !validSSID.isEmpty else { return nil }
        return WiFiCredentials(ssid: validSSID, password: password ?? "", securityType: type)
    }
    
    /// Connects to a Wi-Fi Direct soft-AP SSID and passphrase
    public func connect(credentials: WiFiCredentials, completion: @escaping (Result<String, Error>) -> Void) {
        DispatchQueue.main.async {
            self.isConnecting = true
            self.lastError = nil
        }
        
        let config: NEHotspotConfiguration
        if credentials.password.isEmpty {
            config = NEHotspotConfiguration(ssid: credentials.ssid)
        } else {
            config = NEHotspotConfiguration(ssid: credentials.ssid, passphrase: credentials.password, isWEP: credentials.securityType.uppercased() == "WEP")
        }
        
        // Join once without persisting across reboots
        config.joinOnce = false
        
        NEHotspotConfigurationManager.shared.apply(config) { [weak self] error in
            DispatchQueue.main.async {
                guard let self = self else { return }
                self.isConnecting = false
                
                if let error = error {
                    let nsError = error as NSError
                    if nsError.domain == NEHotspotConfigurationErrorDomain {
                        switch NEHotspotConfigurationError(rawValue: nsError.code) {
                        case .alreadyAssociated:
                            self.connectedSSID = credentials.ssid
                            completion(.success(credentials.ssid))
                            return
                        case .userCancelled:
                            self.lastError = "Connection prompt cancelled."
                        default:
                            self.lastError = error.localizedDescription
                        }
                    } else {
                        self.lastError = error.localizedDescription
                    }
                    completion(.failure(error))
                } else {
                    self.connectedSSID = credentials.ssid
                    completion(.success(credentials.ssid))
                }
            }
        }
    }
    
    /// Disconnects from specified SSID
    public func disconnect(ssid: String) {
        NEHotspotConfigurationManager.shared.removeConfiguration(forSSID: ssid)
        DispatchQueue.main.async {
            if self.connectedSSID == ssid {
                self.connectedSSID = nil
            }
        }
    }
}
