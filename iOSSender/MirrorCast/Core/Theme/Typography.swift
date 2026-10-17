//
//  Typography.swift
//  MirrorCast
//
//  Typography styles conforming to the MirrorCast dark aesthetic.
//

import SwiftUI

public struct MCTextStyle {
    public static let largeTitle = Font.system(size: 28, weight: .bold, design: .default)
    public static let title = Font.system(size: 22, weight: .semibold, design: .default)
    public static let headline = Font.system(size: 17, weight: .semibold, design: .default)
    public static let body = Font.system(size: 15, weight: .regular, design: .default)
    public static let subheadline = Font.system(size: 13, weight: .medium, design: .default)
    public static let footnote = Font.system(size: 12, weight: .regular, design: .default)
    public static let caption = Font.system(size: 11, weight: .regular, design: .default)
    
    /// Monospaced numeric font for high-precision streaming telemetry
    public static let statsMonospace = Font.system(size: 14, weight: .medium, design: .monospaced)
    public static let badgeMonospace = Font.system(size: 11, weight: .semibold, design: .monospaced)
}

public struct CardModifier: ViewModifier {
    var backgroundColor: Color = .mcBgSurface
    var borderColor: Color = .mcBgHover
    var cornerRadius: CGFloat = 14
    
    public func body(content: Content) -> some View {
        content
            .background(backgroundColor)
            .cornerRadius(cornerRadius)
            .overlay(
                RoundedRectangle(cornerRadius: cornerRadius)
                    .stroke(borderColor, lineWidth: 1)
            )
    }
}

public extension View {
    func mcCard(backgroundColor: Color = .mcBgSurface, borderColor: Color = .mcBgHover, cornerRadius: CGFloat = 14) -> some View {
        modifier(CardModifier(backgroundColor: backgroundColor, borderColor: borderColor, cornerRadius: cornerRadius))
    }
}
