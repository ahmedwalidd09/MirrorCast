//
//  ColorTheme.swift
//  MirrorCast
//
//  Design System Color Tokens adhering to Section 6 specifications.
//

import SwiftUI

public extension Color {
    /// Pure pitch black base background (#000000)
    static let mcBgBase = Color(hex: 0x000000)
    
    /// Deep dark surface card background (#0D0D0D)
    static let mcBgSurface = Color(hex: 0x0D0D0D)
    
    /// Elevated cards, sheets, and popovers (#161616)
    static let mcBgElevated = Color(hex: 0x161616)
    
    /// Interactive hover and pressed background (#1F1F1F)
    static let mcBgHover = Color(hex: 0x1F1F1F)
    
    /// High-contrast primary foreground text and icons (#C8C8CC)
    static let mcSilverBright = Color(hex: 0xC8C8CC)
    
    /// Secondary supporting text and subtle UI elements (#8E8E93)
    static let mcSilverMid = Color(hex: 0x8E8E93)
    
    /// Subtle borders, dividers, and disabled track states (#48484A)
    static let mcSilverDim = Color(hex: 0x48484A)
    
    /// Live streaming active indicator and positive status (#30D158)
    static let mcStreamActive = Color(hex: 0x30D158)
    
    /// Destructive actions, warnings, and errors (#FF453A)
    static let mcError = Color(hex: 0xFF453A)
    
    /// Convenience initializer for 24-bit hexadecimal RGB colors.
    init(hex: UInt32, alpha: Double = 1.0) {
        let red = Double((hex >> 16) & 0xFF) / 255.0
        let green = Double((hex >> 8) & 0xFF) / 255.0
        let blue = Double(hex & 0xFF) / 255.0
        self.init(.sRGB, red: red, green: green, blue: blue, opacity: alpha)
    }
}
