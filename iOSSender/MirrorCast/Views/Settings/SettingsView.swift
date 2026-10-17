//
//  SettingsView.swift
//  MirrorCast
//
//  Configuration preferences, diagnostics, and about information.
//

import SwiftUI

public struct SettingsView: View {
    @Environment(\.presentationMode) var presentationMode
    @ObservedObject var appState: AppState
    
    public init(appState: AppState) {
        self.appState = appState
    }
    
    public var body: some View {
        NavigationView {
            ZStack {
                Color.mcBgBase.ignoresSafeArea()
                
                ScrollView {
                    VStack(spacing: 20) {
                        // Encoder Presets
                        VStack(alignment: .leading, spacing: 14) {
                            Text("DEFAULT ENCODER PRESETS")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverMid)
                                .tracking(1.0)
                            
                            VStack(spacing: 16) {
                                HStack {
                                    Text("Resolution")
                                        .font(MCTextStyle.body)
                                        .foregroundColor(.mcSilverBright)
                                    Spacer()
                                    Picker("Resolution", selection: $appState.streamConfig.resolution) {
                                        ForEach(ResolutionPreset.allCases, id: \.self) { preset in
                                            Text(preset.rawValue).tag(preset)
                                        }
                                    }
                                    .pickerStyle(MenuPickerStyle())
                                    .accentColor(.mcSilverBright)
                                }
                                
                                Divider().background(Color.mcBgHover)
                                
                                HStack {
                                    Text("Target Frame Rate")
                                        .font(MCTextStyle.body)
                                        .foregroundColor(.mcSilverBright)
                                    Spacer()
                                    Picker("FPS", selection: $appState.streamConfig.fps) {
                                        Text("60 FPS (Ultra Smooth)").tag(60)
                                        Text("30 FPS (Power Saver)").tag(30)
                                    }
                                    .pickerStyle(MenuPickerStyle())
                                    .accentColor(.mcSilverBright)
                                }
                                
                                Divider().background(Color.mcBgHover)
                                
                                Toggle(isOn: $appState.streamConfig.audioEnabled) {
                                    VStack(alignment: .leading, spacing: 2) {
                                        Text("Stream Device Audio")
                                            .font(MCTextStyle.body)
                                            .foregroundColor(.mcSilverBright)
                                        Text("Forwards internal app audio buffer (AAC-LC)")
                                            .font(MCTextStyle.footnote)
                                            .foregroundColor(.mcSilverMid)
                                    }
                                }
                                .toggleStyle(SwitchToggleStyle(tint: Color.mcSilverBright))
                            }
                            .padding(16)
                            .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 14)
                        }
                        
                        // Diagnostics & Infrastructure
                        VStack(alignment: .leading, spacing: 14) {
                            Text("DIAGNOSTICS & SYSTEM")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverMid)
                                .tracking(1.0)
                            
                            VStack(spacing: 12) {
                                diagnosticRow(label: "App Group", value: SharedConfig.appGroupIdentifier)
                                Divider().background(Color.mcBgHover)
                                diagnosticRow(label: "Broadcast Extension ID", value: "com.mirrorcast.app.broadcast")
                                Divider().background(Color.mcBgHover)
                                diagnosticRow(label: "Service Type", value: "_screenmirror._tcp")
                                Divider().background(Color.mcBgHover)
                                diagnosticRow(label: "Primary TCP Port", value: "59100")
                                Divider().background(Color.mcBgHover)
                                diagnosticRow(label: "Control TCP Port", value: "59101")
                            }
                            .padding(16)
                            .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 14)
                        }
                        
                        // About
                        VStack(alignment: .leading, spacing: 14) {
                            Text("ABOUT")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverMid)
                                .tracking(1.0)
                            
                            VStack(alignment: .leading, spacing: 8) {
                                Text("MirrorCast for iOS")
                                    .font(MCTextStyle.headline)
                                    .foregroundColor(.mcSilverBright)
                                
                                Text("Version 1.0 (Build 100) · Protocol v1.0")
                                    .font(MCTextStyle.footnote)
                                    .foregroundColor(.mcSilverMid)
                                
                                Text("Low-latency screen mirroring to Windows receivers over Wi-Fi and Wi-Fi Direct Soft-AP with hardware VideoToolbox H.264 acceleration.")
                                    .font(MCTextStyle.footnote)
                                    .foregroundColor(.mcSilverMid)
                                    .padding(.top, 4)
                            }
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(16)
                            .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 14)
                        }
                    }
                    .padding(18)
                }
            }
            .navigationTitle("Settings")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarTrailing) {
                    Button("Done") {
                        presentationMode.wrappedValue.dismiss()
                    }
                    .foregroundColor(.mcSilverBright)
                }
            }
        }
    }
    
    private func diagnosticRow(label: String, value: String) -> some View {
        HStack {
            Text(label)
                .font(MCTextStyle.body)
                .foregroundColor(.mcSilverMid)
            Spacer()
            Text(value)
                .font(MCTextStyle.statsMonospace)
                .foregroundColor(.mcSilverBright)
                .lineLimit(1)
        }
    }
}
