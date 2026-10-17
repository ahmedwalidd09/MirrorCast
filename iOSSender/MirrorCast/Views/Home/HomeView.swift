//
//  HomeView.swift
//  MirrorCast
//
//  Main discovery and connection screen strictly adhering to Section 6.
//

import SwiftUI
import ReplayKit

public struct HomeView: View {
    @ObservedObject var appState: AppState
    
    @State private var isPulsing: Bool = false
    
    public init(appState: AppState) {
        self.appState = appState
    }
    
    public var body: some View {
        NavigationView {
            ZStack {
                Color.mcBgBase.ignoresSafeArea()
                
                VStack(spacing: 0) {
                    ScrollView {
                        VStack(spacing: 20) {
                            // Mode Segmented Switcher (Same Wi-Fi vs Wi-Fi Direct)
                            CustomSegmentedControl(
                                options: ConnectionMode.allCases,
                                selection: $appState.selectedMode
                            )
                            .padding(.top, 12)
                            
                            // Mode 1: Infrastructure Wi-Fi Content
                            if appState.selectedMode == .wifi {
                                wifiDiscoverySection
                            } else {
                                // Mode 2: Wi-Fi Direct Soft-AP Content
                                wifiDirectSection
                            }
                            
                            // Selected Receiver & Broadcast Launcher Card
                            if let selected = appState.selectedReceiver {
                                selectedReceiverSection(receiver: selected)
                            }
                        }
                        .padding(.horizontal, 18)
                        .padding(.bottom, 24)
                    }
                }
            }
            .navigationTitle("MirrorCast")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button(action: { appState.showSettingsSheet = true }) {
                        Image(systemName: "gearshape")
                            .font(.system(size: 18, weight: .regular))
                            .foregroundColor(.mcSilverBright)
                    }
                }
            }
            .sheet(isPresented: $appState.showSettingsSheet) {
                SettingsView(appState: appState)
            }
            .sheet(isPresented: $appState.showQRScannerSheet) {
                QRCodeScannerView(appState: appState)
            }
            .sheet(isPresented: $appState.showManualConnectSheet) {
                ManualConnectSheet(appState: appState)
            }
            .alert(isPresented: Binding(
                get: { appState.alertMessage != nil },
                set: { if !$0 { appState.alertMessage = nil } }
            )) {
                Alert(
                    title: Text("Notice"),
                    message: Text(appState.alertMessage ?? ""),
                    dismissButton: .default(Text("OK"))
                )
            }
        }
        .navigationViewStyle(StackNavigationViewStyle())
    }
    
    // MARK: - Mode 1: Wi-Fi Discovery Section
    
    private var wifiDiscoverySection: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("DISCOVERED RECEIVERS")
                    .font(MCTextStyle.caption)
                    .foregroundColor(.mcSilverMid)
                    .tracking(1.0)
                
                Spacer()
                
                if appState.bonjourManager.isSearching {
                    HStack(spacing: 6) {
                        Circle()
                            .fill(Color.mcStreamActive)
                            .frame(width: 7, height: 7)
                            .scaleEffect(isPulsing ? 1.3 : 0.9)
                            .opacity(isPulsing ? 1.0 : 0.5)
                            .onAppear {
                                withAnimation(Animation.easeInOut(duration: 1.0).repeatForever(autoreverses: true)) {
                                    isPulsing = true
                                }
                            }
                        
                        Text("Searching...")
                            .font(MCTextStyle.footnote)
                            .foregroundColor(.mcSilverMid)
                    }
                }
            }
            
            if appState.bonjourManager.discoveredReceivers.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "network")
                        .font(.system(size: 38))
                        .foregroundColor(.mcSilverDim)
                    
                    Text("Looking for MirrorCast Receivers")
                        .font(MCTextStyle.headline)
                        .foregroundColor(.mcSilverBright)
                    
                    Text("Ensure Windows receiver is running on the same local network.")
                        .font(MCTextStyle.footnote)
                        .foregroundColor(.mcSilverMid)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal, 20)
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 32)
                .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 14)
            } else {
                VStack(spacing: 10) {
                    ForEach(appState.bonjourManager.discoveredReceivers) { receiver in
                        ReceiverRowView(
                            receiver: receiver,
                            isSelected: appState.selectedReceiver?.host == receiver.host,
                            onSelect: {
                                withAnimation(.spring()) {
                                    appState.selectReceiver(receiver)
                                }
                            }
                        )
                    }
                }
            }
            
            // Manual Connect Row
            Button(action: { appState.showManualConnectSheet = true }) {
                HStack(spacing: 10) {
                    Image(systemName: "plus.circle")
                        .font(.system(size: 16, weight: .medium))
                    Text("Manual IP Address Connect")
                        .font(MCTextStyle.subheadline)
                    Spacer()
                    Image(systemName: "chevron.right")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(.mcSilverDim)
                }
                .foregroundColor(.mcSilverBright)
                .padding(14)
                .mcCard(backgroundColor: Color.mcBgElevated, borderColor: Color.mcBgHover, cornerRadius: 12)
            }
            .buttonStyle(PlainButtonStyle())
        }
    }
    
    // MARK: - Mode 2: Wi-Fi Direct Section
    
    private var wifiDirectSection: some View {
        VStack(alignment: .leading, spacing: 14) {
            Text("DIRECT P2P CONNECTION")
                .font(MCTextStyle.caption)
                .foregroundColor(.mcSilverMid)
                .tracking(1.0)
            
            VStack(spacing: 16) {
                HStack(spacing: 14) {
                    Image(systemName: "qrcode.viewfinder")
                        .font(.system(size: 32))
                        .foregroundColor(.mcSilverBright)
                    
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Connect via Windows QR Code")
                            .font(MCTextStyle.headline)
                            .foregroundColor(.mcSilverBright)
                        
                        Text("Scan the soft-AP QR code displayed on the Windows receiver screen.")
                            .font(MCTextStyle.footnote)
                            .foregroundColor(.mcSilverMid)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                
                Button(action: { appState.showQRScannerSheet = true }) {
                    HStack(spacing: 8) {
                        Image(systemName: "camera.fill")
                            .font(.system(size: 15, weight: .semibold))
                        Text("Connect via QR Code")
                            .font(.system(size: 15, weight: .semibold))
                    }
                    .foregroundColor(.black)
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 13)
                    .background(Color.mcSilverBright)
                    .cornerRadius(10)
                }
            }
            .padding(18)
            .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 14)
            
            if let ssid = appState.hotspotManager.connectedSSID {
                HStack(spacing: 10) {
                    Circle()
                        .fill(Color.mcStreamActive)
                        .frame(width: 8, height: 8)
                    Text("Joined Soft-AP: \(ssid)")
                        .font(MCTextStyle.statsMonospace)
                        .foregroundColor(.mcSilverBright)
                    Spacer()
                }
                .padding(12)
                .mcCard(backgroundColor: Color.mcBgElevated, borderColor: Color.mcBgHover, cornerRadius: 10)
            }
        }
    }
    
    // MARK: - Selected Receiver Card & Broadcast Trigger
    
    private func selectedReceiverSection(receiver: DiscoveredReceiver) -> some View {
        VStack(spacing: 14) {
            VStack(alignment: .leading, spacing: 12) {
                HStack {
                    VStack(alignment: .leading, spacing: 3) {
                        Text("TARGET RECEIVER")
                            .font(MCTextStyle.caption)
                            .foregroundColor(.mcSilverDim)
                            .tracking(1.0)
                        
                        Text(receiver.name)
                            .font(.system(size: 18, weight: .bold))
                            .foregroundColor(.mcSilverBright)
                    }
                    
                    Spacer()
                    
                    VStack(alignment: .trailing, spacing: 3) {
                        Text(receiver.host)
                            .font(MCTextStyle.statsMonospace)
                            .foregroundColor(.mcSilverMid)
                        
                        Text("Port \(receiver.port)")
                            .font(MCTextStyle.footnote)
                            .foregroundColor(.mcSilverDim)
                    }
                }
                
                Divider()
                    .background(Color.mcBgHover)
                
                HStack {
                    HStack(spacing: 6) {
                        Image(systemName: "film")
                            .font(.system(size: 12))
                            .foregroundColor(.mcSilverMid)
                        Text(appState.streamConfig.resolution.rawValue)
                            .font(MCTextStyle.badgeMonospace)
                            .foregroundColor(.mcSilverBright)
                    }
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.mcBgElevated)
                    .cornerRadius(6)
                    
                    HStack(spacing: 6) {
                        Image(systemName: "speedometer")
                            .font(.system(size: 12))
                            .foregroundColor(.mcSilverMid)
                        Text(String(format: "%.1f Mbps", appState.streamConfig.bitrateMbps))
                            .font(MCTextStyle.badgeMonospace)
                            .foregroundColor(.mcSilverBright)
                    }
                    .padding(.horizontal, 8)
                    .padding(.vertical, 4)
                    .background(Color.mcBgElevated)
                    .cornerRadius(6)
                    
                    Spacer()
                }
            }
            .padding(16)
            .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcSilverDim.opacity(0.4), cornerRadius: 14)
            
            // Broadcast Launch Control
            ZStack {
                // Invisible ReplayKit System Broadcast Picker
                BroadcastPickerRepresentable(
                    preferredExtensionBundleId: "com.mirrorcast.app.broadcast",
                    showsMicrophoneButton: appState.streamConfig.audioEnabled
                )
                .frame(height: 52)
                .opacity(0.015)
                
                // Stylized Overlay Button that users perceive
                HStack(spacing: 10) {
                    Image(systemName: "arrow.up.to.line.circle.fill")
                        .font(.system(size: 20, weight: .semibold))
                    Text("Start Screen Mirroring")
                        .font(.system(size: 16, weight: .semibold))
                }
                .foregroundColor(.black)
                .frame(maxWidth: .infinity)
                .frame(height: 52)
                .background(Color.mcSilverBright)
                .cornerRadius(12)
                .allowsHitTesting(false)
            }
        }
        .padding(.top, 6)
    }
}
