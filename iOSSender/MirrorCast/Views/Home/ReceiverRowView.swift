//
//  ReceiverRowView.swift
//  MirrorCast
//
//  Receiver item row conforming to Section 6 specifications:
//  #0D0D0D background, rounded corners, custom arrow chevron in #8E8E93.
//

import SwiftUI

public struct ReceiverRowView: View {
    public let receiver: DiscoveredReceiver
    public let isSelected: Bool
    public let onSelect: () -> Void
    
    public init(receiver: DiscoveredReceiver, isSelected: Bool, onSelect: @escaping () -> Void) {
        self.receiver = receiver
        self.isSelected = isSelected
        self.onSelect = onSelect
    }
    
    public var body: some View {
        Button(action: onSelect) {
            HStack(spacing: 14) {
                // Monitor / Display Icon
                ZStack {
                    RoundedRectangle(cornerRadius: 10)
                        .fill(isSelected ? Color.mcBgHover : Color.mcBgElevated)
                        .frame(width: 44, height: 44)
                        .overlay(
                            RoundedRectangle(cornerRadius: 10)
                                .stroke(isSelected ? Color.mcSilverBright.opacity(0.4) : Color.clear, lineWidth: 1)
                        )
                    
                    Image(systemName: receiver.mode == .wifiDirect ? "wifi.circle" : "display")
                        .font(.system(size: 20, weight: .regular))
                        .foregroundColor(isSelected ? .mcSilverBright : .mcSilverMid)
                }
                
                // Receiver Details
                VStack(alignment: .leading, spacing: 3) {
                    HStack(spacing: 6) {
                        Text(receiver.name)
                            .font(.system(size: 16, weight: .semibold))
                            .foregroundColor(.mcSilverBright)
                            .lineLimit(1)
                        
                        if receiver.isManual {
                            Text("MANUAL")
                                .font(MCTextStyle.badgeMonospace)
                                .foregroundColor(.mcSilverMid)
                                .padding(.horizontal, 5)
                                .padding(.vertical, 2)
                                .background(Color.mcBgElevated)
                                .cornerRadius(4)
                        }
                    }
                    
                    Text("\(receiver.host):\(receiver.port)")
                        .font(MCTextStyle.statsMonospace)
                        .foregroundColor(.mcSilverMid)
                        .font(.system(size: 13))
                }
                
                Spacer()
                
                // Custom arrow chevron in #8E8E93
                Image(systemName: "chevron.right")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundColor(isSelected ? .mcSilverBright : .mcSilverMid)
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 12)
            .background(Color.mcBgSurface)
            .cornerRadius(12)
            .overlay(
                RoundedRectangle(cornerRadius: 12)
                    .stroke(isSelected ? Color.mcSilverBright.opacity(0.5) : Color.mcBgHover, lineWidth: 1)
            )
        }
        .buttonStyle(PlainButtonStyle())
    }
}
