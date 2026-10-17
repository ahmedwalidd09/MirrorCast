//
//  CustomSegmentedControl.swift
//  MirrorCast
//
//  A dark-themed custom segmented control conforming to MirrorCast UI tokens.
//

import SwiftUI

public struct CustomSegmentedControl<T: Hashable & CustomStringConvertible>: View {
    public let options: [T]
    @Binding public var selection: T
    
    public init(options: [T], selection: Binding<T>) {
        self.options = options
        self._selection = selection
    }
    
    public var body: some View {
        HStack(spacing: 4) {
            ForEach(options, id: \.self) { option in
                let isSelected = (selection == option)
                Button {
                    withAnimation(.spring(response: 0.25, dampingFraction: 0.75)) {
                        selection = option
                    }
                } label: {
                    Text(option.description)
                        .font(.system(size: 13, weight: isSelected ? .semibold : .medium))
                        .foregroundColor(isSelected ? .mcSilverBright : .mcSilverMid)
                        .padding(.vertical, 8)
                        .frame(maxWidth: .infinity)
                        .background(
                            ZStack {
                                if isSelected {
                                    RoundedRectangle(cornerRadius: 8)
                                        .fill(Color.mcBgHover)
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 8)
                                                .stroke(Color.mcSilverDim.opacity(0.4), lineWidth: 1)
                                        )
                                        .shadow(color: Color.black.opacity(0.4), radius: 3, x: 0, y: 1)
                                }
                            }
                        )
                }
                .buttonStyle(PlainButtonStyle())
            }
        }
        .padding(4)
        .background(Color.mcBgSurface)
        .clipShape(RoundedRectangle(cornerRadius: 11))
        .overlay(
            RoundedRectangle(cornerRadius: 11)
                .stroke(Color.mcBgElevated, lineWidth: 1)
        )
    }
}
