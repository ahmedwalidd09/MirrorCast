//
//  StreamingView.swift
//  MirrorCast
//
//  Live Streaming and Diagnostics Dashboard strictly adhering to Section 6:
//  - Live indicator dot #30D158 + "Mirroring to [Receiver]"
//  - Live stats: resolution · 60 FPS · 8.2 Mbps
//  - Resolution segmented control: 720p / 1080p / Native
//  - Custom Bitrate slider (4–20 Mbps) with 20pt circular thumb
//  - Primary action button: "Pause Mirroring"
//  - Text-only "Stop Mirroring" button in #FF453A
//

import SwiftUI

public struct StreamingView: View {
    @ObservedObject var appState: AppState
    
    @State private var pulseLiveDot: Bool = false
    @State private var bitrateSliderValue: Double = 8.0
    
    public init(appState: AppState) {
        self.appState = appState
    }
    
    public var body: some View {
        ZStack {
            Color.mcBgBase.ignoresSafeArea()
            
            VStack(spacing: 24) {
                // Header: Live Indicator Dot + Receiver Name
                HStack(spacing: 10) {
                    Circle()
                        .fill(appState.isPaused ? Color.mcSilverMid : Color.mcStreamActive)
                        .frame(width: 10, height: 10)
                        .scaleEffect(pulseLiveDot && !appState.isPaused ? 1.25 : 0.9)
                        .opacity(pulseLiveDot && !appState.isPaused ? 1.0 : 0.6)
                        .onAppear {
                            withAnimation(Animation.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                                pulseLiveDot = true
                            }
                        }
                    
                    let receiverName = appState.selectedReceiver?.name ?? "Windows PC"
                    Text(appState.isPaused ? "Paused: \(receiverName)" : "Mirroring to \(receiverName)")
                        .font(.system(size: 17, weight: .semibold))
                        .foregroundColor(.mcSilverBright)
                    
                    Spacer()
                    
                    Text(appState.streamStats.formattedDuration)
                        .font(MCTextStyle.statsMonospace)
                        .foregroundColor(.mcSilverMid)
                }
                .padding(.horizontal, 20)
                .padding(.top, 16)
                
                // Live Stats Banner (e.g., "1080p · 60 FPS · 8.2 Mbps")
                VStack(spacing: 12) {
                    Text(appState.streamStats.formattedSummary)
                        .font(.system(size: 19, weight: .bold, design: .monospaced))
                        .foregroundColor(.mcSilverBright)
                    
                    HStack(spacing: 20) {
                        statPill(label: "LATENCY", value: String(format: "%.0f ms", appState.streamStats.rttMs))
                        statPill(label: "FRAMES", value: "\(appState.streamStats.packetsSent)")
                        statPill(label: "TRANSFERRED", value: formatDataTransferred(bytes: appState.streamStats.bytesSent))
                    }
                }
                .frame(maxWidth: .infinity)
                .padding(.vertical, 20)
                .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 16)
                .padding(.horizontal, 18)
                
                // Controls Section
                VStack(alignment: .leading, spacing: 20) {
                    // Resolution Control
                    VStack(alignment: .leading, spacing: 10) {
                        Text("RESOLUTION")
                            .font(MCTextStyle.caption)
                            .foregroundColor(.mcSilverDim)
                            .tracking(1.0)
                        
                        CustomSegmentedControl(
                            options: ResolutionPreset.allCases,
                            selection: Binding(
                                get: { appState.streamConfig.resolution },
                                set: { appState.updateResolution($0) }
                            )
                        )
                    }
                    
                    // Bitrate Slider Control (4-20 Mbps with 20pt circular thumb)
                    VStack(alignment: .leading, spacing: 10) {
                        HStack {
                            Text("TARGET BITRATE")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverDim)
                                .tracking(1.0)
                            
                            Spacer()
                            
                            Text(String(format: "%.1f Mbps", appState.streamConfig.bitrateMbps))
                                .font(MCTextStyle.statsMonospace)
                                .foregroundColor(.mcSilverBright)
                        }
                        
                        CustomSlider(
                            value: Binding(
                                get: { appState.streamConfig.bitrateMbps },
                                set: { appState.updateBitrate($0) }
                            ),
                            in: 4.0...20.0,
                            step: 0.5,
                            unit: "Mbps"
                        )
                        .padding(.vertical, 4)
                        
                        HStack {
                            Text("4 Mbps (Low Latency)")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverDim)
                            Spacer()
                            Text("20 Mbps (Ultra Detail)")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverDim)
                        }
                    }
                }
                .padding(20)
                .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 16)
                .padding(.horizontal, 18)
                
                Spacer()
                
                // Bottom Actions
                VStack(spacing: 16) {
                    // Primary Action: Pause Mirroring
                    Button(action: {
                        withAnimation {
                            appState.togglePause()
                        }
                    }) {
                        HStack(spacing: 8) {
                            Image(systemName: appState.isPaused ? "play.fill" : "pause.fill")
                                .font(.system(size: 16, weight: .semibold))
                            Text(appState.isPaused ? "Resume Mirroring" : "Pause Mirroring")
                                .font(.system(size: 16, weight: .semibold))
                        }
                        .foregroundColor(.black)
                        .frame(maxWidth: .infinity)
                        .frame(height: 52)
                        .background(Color.mcSilverBright)
                        .cornerRadius(12)
                    }
                    
                    // Secondary Action: Stop Mirroring (Text-only in #FF453A)
                    Button(action: {
                        appState.stopStreaming()
                    }) {
                        Text("Stop Mirroring")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(.mcError)
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 12)
                    }
                    .buttonStyle(PlainButtonStyle())
                }
                .padding(.horizontal, 20)
                .padding(.bottom, 24)
            }
        }
    }
    
    private func statPill(label: String, value: String) -> some View {
        VStack(spacing: 2) {
            Text(label)
                .font(.system(size: 9, weight: .medium))
                .foregroundColor(.mcSilverDim)
            Text(value)
                .font(MCTextStyle.statsMonospace)
                .foregroundColor(.mcSilverMid)
        }
    }
    
    private func formatDataTransferred(bytes: UInt64) -> String {
        if bytes < 1024 * 1024 {
            return String(format: "%.1f KB", Double(bytes) / 1024.0)
        } else if bytes < 1024 * 1024 * 1024 {
            return String(format: "%.1f MB", Double(bytes) / (1024.0 * 1024.0))
        } else {
            return String(format: "%.2f GB", Double(bytes) / (1024.0 * 1024.0 * 1024.0))
        }
    }
}
