//
//  BroadcastPickerRepresentable.swift
//  MirrorCast
//
//  Wraps RPSystemBroadcastPickerView for seamless broadcast initiation.
//

import SwiftUI
import ReplayKit

public struct BroadcastPickerRepresentable: UIViewRepresentable {
    public let preferredExtensionBundleId: String
    public var showsMicrophoneButton: Bool = false
    
    public init(
        preferredExtensionBundleId: String = "com.mirrorcast.app.broadcast",
        showsMicrophoneButton: Bool = false
    ) {
        self.preferredExtensionBundleId = preferredExtensionBundleId
        self.showsMicrophoneButton = showsMicrophoneButton
    }
    
    public func makeUIView(context: Context) -> RPSystemBroadcastPickerView {
        let picker = RPSystemBroadcastPickerView(frame: CGRect(x: 0, y: 0, width: 64, height: 64))
        picker.preferredExtension = preferredExtensionBundleId
        picker.showsMicrophoneButton = showsMicrophoneButton
        
        // Find internal button to style its tint
        for subview in picker.subviews {
            if let button = subview as? UIButton {
                button.tintColor = UIColor(Color.mcSilverBright)
                button.imageView?.contentMode = .scaleAspectFit
            }
        }
        
        return picker
    }
    
    public func updateUIView(_ uiView: RPSystemBroadcastPickerView, context: Context) {
        uiView.preferredExtension = preferredExtensionBundleId
        uiView.showsMicrophoneButton = showsMicrophoneButton
    }
}

/// Helper to programmatically trigger the ReplayKit Broadcast Picker
public enum BroadcastLauncher {
    public static func triggerPicker(preferredExtensionBundleId: String = "com.mirrorcast.app.broadcast") {
        let picker = RPSystemBroadcastPickerView()
        picker.preferredExtension = preferredExtensionBundleId
        picker.showsMicrophoneButton = false
        
        for subview in picker.subviews {
            if let button = subview as? UIButton {
                button.sendActions(for: .allTouchEvents)
                return
            }
        }
    }
}
