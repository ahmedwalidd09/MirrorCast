//
//  BonjourDiscoveryManager.swift
//  MirrorCast
//
//  mDNS / DNS-SD Service Discovery for _screenmirror._tcp using Apple Network framework NWBrowser.
//

import Foundation
import Network
import Combine

public final class BonjourDiscoveryManager: ObservableObject {
    @Published public var discoveredReceivers: [DiscoveredReceiver] = []
    @Published public var isSearching: Bool = false
    @Published public var errorMessage: String?
    
    private var browser: NWBrowser?
    private let queue = DispatchQueue(label: "com.mirrorcast.bonjour.queue", qos: .userInitiated)
    private var resolvingConnections: [String: NWConnection] = [:]
    
    public init() {}
    
    public func startBrowsing() {
        stopBrowsing()
        
        let descriptor = NWBrowser.Descriptor.bonjour(type: "_screenmirror._tcp", domain: "local.")
        let parameters = NWParameters()
        parameters.includePeerToPeer = true
        
        let browser = NWBrowser(for: descriptor, using: parameters)
        self.browser = browser
        
        browser.stateUpdateHandler = { [weak self] state in
            DispatchQueue.main.async {
                guard let self = self else { return }
                switch state {
                case .ready:
                    self.isSearching = true
                    self.errorMessage = nil
                case .failed(let error):
                    self.isSearching = false
                    self.errorMessage = "Discovery error: \(error.localizedDescription)"
                case .cancelled:
                    self.isSearching = false
                default:
                    break
                }
            }
        }
        
        browser.browseResultsChangedHandler = { [weak self] results, changes in
            guard let self = self else { return }
            self.handleBrowseResults(results)
        }
        
        browser.start(queue: queue)
        isSearching = true
    }
    
    public func stopBrowsing() {
        browser?.cancel()
        browser = nil
        resolvingConnections.values.forEach { $0.cancel() }
        resolvingConnections.removeAll()
        isSearching = false
    }
    
    private func handleBrowseResults(_ results: Set<NWBrowser.Result>) {
        for result in results {
            switch result.endpoint {
            case .service(let name, let type, let domain, let interface):
                var receiverName = name
                var directMode = false
                
                // Parse TXT records if available
                if case .bonjour(let txtRecord) = result.metadata {
                    if let displayName = txtRecord.dictionary["name"] {
                        receiverName = displayName
                    }
                    if let directFlag = txtRecord.dictionary["direct"] {
                        directMode = (directFlag.lowercased() == "true")
                    }
                }
                
                resolveEndpoint(endpoint: result.endpoint, name: receiverName, isDirect: directMode)
                
            default:
                break
            }
        }
    }
    
    private func resolveEndpoint(endpoint: NWEndpoint, name: String, isDirect: Bool) {
        let key = endpoint.debugDescription
        guard resolvingConnections[key] == nil else { return }
        
        let tcpParameters = NWParameters.tcp
        tcpParameters.preferNoProxies = true
        let connection = NWConnection(to: endpoint, using: tcpParameters)
        resolvingConnections[key] = connection
        
        connection.stateUpdateHandler = { [weak self, weak connection] state in
            guard let self = self, let connection = connection else { return }
            
            switch state {
            case .ready, .preparing:
                if let remote = connection.currentPath?.remoteEndpoint {
                    var hostString = ""
                    var portInt = 59100
                    
                    switch remote {
                    case .hostPort(let host, let port):
                        switch host {
                        case .ipv4(let ip4):
                            hostString = "\(ip4)"
                        case .ipv6(let ip6):
                            hostString = "\(ip6)"
                        case .name(let hostName, _):
                            hostString = hostName
                        @unknown default:
                            hostString = host.debugDescription
                        }
                        portInt = Int(port.rawValue)
                    default:
                        break
                    }
                    
                    if !hostString.isEmpty {
                        self.addOrUpdateReceiver(
                            name: name,
                            host: hostString,
                            port: portInt,
                            mode: isDirect ? .wifiDirect : .wifi
                        )
                        connection.cancel()
                        self.resolvingConnections.removeValue(forKey: key)
                    }
                }
                
            case .failed, .cancelled:
                self.resolvingConnections.removeValue(forKey: key)
            default:
                break
            }
        }
        
        connection.start(queue: queue)
        
        // Timeout resolution after 4 seconds
        queue.asyncAfter(deadline: .now() + 4.0) { [weak self, weak connection] in
            if let connection = connection {
                connection.cancel()
                self?.resolvingConnections.removeValue(forKey: key)
            }
        }
    }
    
    private func addOrUpdateReceiver(name: String, host: String, port: Int, mode: ConnectionMode) {
        DispatchQueue.main.async {
            if let index = self.discoveredReceivers.firstIndex(where: { $0.host == host && $0.port == port }) {
                self.discoveredReceivers[index].name = name
                self.discoveredReceivers[index].lastSeen = Date()
                self.discoveredReceivers[index].mode = mode
            } else {
                let receiver = DiscoveredReceiver(
                    name: name,
                    host: host,
                    port: port,
                    controlPort: port + 1,
                    mode: mode,
                    isManual: false,
                    lastSeen: Date()
                )
                self.discoveredReceivers.append(receiver)
            }
        }
    }
}
