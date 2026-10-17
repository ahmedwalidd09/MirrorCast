//
//  ManualConnectSheet.swift
//  MirrorCast
//
//  Sheet for manually entering receiver IP address and port.
//

import SwiftUI

public struct ManualConnectSheet: View {
    @Environment(\.presentationMode) var presentationMode
    @ObservedObject var appState: AppState
    
    @State private var receiverName: String = "Windows PC"
    @State private var hostAddress: String = ""
    @State private var portString: String = "59100"
    @State private var validationError: String?
    
    public init(appState: AppState) {
        self.appState = appState
    }
    
    public var body: some View {
        NavigationView {
            ZStack {
                Color.mcBgBase.ignoresSafeArea()
                
                VStack(spacing: 20) {
                    VStack(alignment: .leading, spacing: 14) {
                        Text("Connect to Specific Address")
                            .font(MCTextStyle.subheadline)
                            .foregroundColor(.mcSilverMid)
                            .textCase(.uppercase)
                        
                        // Receiver Name Field
                        VStack(alignment: .leading, spacing: 6) {
                            Text("DEVICE NAME")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverDim)
                            
                            TextField("e.g. Living Room PC", text: $receiverName)
                                .font(MCTextStyle.body)
                                .foregroundColor(.mcSilverBright)
                                .padding(12)
                                .background(Color.mcBgSurface)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(Color.mcBgHover, lineWidth: 1)
                                )
                        }
                        
                        // Host IP Address Field
                        VStack(alignment: .leading, spacing: 6) {
                            Text("IP ADDRESS")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverDim)
                            
                            TextField("192.168.1.100", text: $hostAddress)
                                .font(MCTextStyle.statsMonospace)
                                .foregroundColor(.mcSilverBright)
                                .keyboardType(.numbersAndPunctuation)
                                .autocapitalization(.none)
                                .disableAutocorrection(true)
                                .padding(12)
                                .background(Color.mcBgSurface)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(Color.mcBgHover, lineWidth: 1)
                                )
                        }
                        
                        // Port Field
                        VStack(alignment: .leading, spacing: 6) {
                            Text("PORT (DEFAULT 59100)")
                                .font(MCTextStyle.caption)
                                .foregroundColor(.mcSilverDim)
                            
                            TextField("59100", text: $portString)
                                .font(MCTextStyle.statsMonospace)
                                .foregroundColor(.mcSilverBright)
                                .keyboardType(.numberPad)
                                .padding(12)
                                .background(Color.mcBgSurface)
                                .cornerRadius(10)
                                .overlay(
                                    RoundedRectangle(cornerRadius: 10)
                                        .stroke(Color.mcBgHover, lineWidth: 1)
                                )
                        }
                        
                        if let error = validationError {
                            Text(error)
                                .font(MCTextStyle.footnote)
                                .foregroundColor(.mcError)
                        }
                    }
                    .padding(18)
                    .mcCard(backgroundColor: Color.mcBgSurface, borderColor: Color.mcBgHover, cornerRadius: 16)
                    
                    Button(action: saveAndConnect) {
                        Text("Connect Receiver")
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(.black)
                            .frame(maxWidth: .infinity)
                            .padding(.vertical, 14)
                            .background(Color.mcSilverBright)
                            .cornerRadius(12)
                    }
                    
                    Spacer()
                }
                .padding(20)
            }
            .navigationTitle("Manual Connect")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .navigationBarLeading) {
                    Button("Cancel") {
                        presentationMode.wrappedValue.dismiss()
                    }
                    .foregroundColor(.mcSilverMid)
                }
            }
        }
    }
    
    private func saveAndConnect() {
        let trimmedHost = hostAddress.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedHost.isEmpty else {
            validationError = "Please enter a valid IP address."
            return
        }
        
        let portInt = Int(portString) ?? 59100
        guard portInt > 1024 && portInt <= 65535 else {
            validationError = "Port must be between 1025 and 65535."
            return
        }
        
        appState.addManualReceiver(name: receiverName, host: trimmedHost, port: portInt)
        presentationMode.wrappedValue.dismiss()
    }
}
