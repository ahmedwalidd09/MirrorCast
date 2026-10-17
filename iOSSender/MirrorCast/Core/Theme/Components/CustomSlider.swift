//
//  CustomSlider.swift
//  MirrorCast
//
//  Custom dark-styled slider with a 20pt circular thumb conforming to Section 6.
//

import SwiftUI

public struct CustomSlider: View {
    @Binding public var value: Double
    public let range: ClosedRange<Double>
    public let step: Double
    public let unit: String
    
    @State private var isDragging: Bool = false
    
    public init(
        value: Binding<Double>,
        in range: ClosedRange<Double> = 4.0...20.0,
        step: Double = 0.5,
        unit: String = "Mbps"
    ) {
        self._value = value
        self.range = range
        self.step = step
        self.unit = unit
    }
    
    public var body: some View {
        GeometryReader { geometry in
            let totalWidth = max(geometry.size.width - 20, 1)
            let percentage = CGFloat((value - range.lowerBound) / (range.upperBound - range.lowerBound))
            let clampedPercentage = min(max(percentage, 0), 1)
            let currentX = clampedPercentage * totalWidth
            
            ZStack(alignment: .leading) {
                // Background Track
                RoundedRectangle(cornerRadius: 3)
                    .fill(Color.mcSilverDim.opacity(0.35))
                    .frame(height: 6)
                    .padding(.horizontal, 10)
                
                // Active Track
                RoundedRectangle(cornerRadius: 3)
                    .fill(
                        LinearGradient(
                            gradient: Gradient(colors: [Color.mcSilverMid, Color.mcSilverBright]),
                            startPoint: .leading,
                            endPoint: .trailing
                        )
                    )
                    .frame(width: max(currentX, 6), height: 6)
                    .padding(.leading, 10)
                
                // 20pt Circular Thumb
                Circle()
                    .fill(Color.mcSilverBright)
                    .frame(width: 20, height: 20)
                    .overlay(
                        Circle()
                            .stroke(Color.white.opacity(0.8), lineWidth: 1.5)
                    )
                    .shadow(color: Color.black.opacity(0.5), radius: 4, x: 0, y: 2)
                    .offset(x: currentX)
                    .gesture(
                        DragGesture(minimumDistance: 0)
                            .onChanged { gesture in
                                isDragging = true
                                let touchX = gesture.location.x - 10
                                let fraction = max(0, min(touchX / totalWidth, 1.0))
                                let rawValue = range.lowerBound + Double(fraction) * (range.upperBound - range.lowerBound)
                                let steppedValue = round(rawValue / step) * step
                                value = min(max(steppedValue, range.lowerBound), range.upperBound)
                            }
                            .onEnded { _ in
                                isDragging = false
                            }
                    )
            }
            .frame(height: 24)
        }
        .frame(height: 24)
    }
}
