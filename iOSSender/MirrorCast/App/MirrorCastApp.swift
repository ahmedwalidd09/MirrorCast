//
//  MirrorCastApp.swift
//  MirrorCast
//
//  Main application entry point for MirrorCast iOS.
//

import SwiftUI

@main
struct MirrorCastApp: App {
    @StateObject private var appState = AppState()
    
    init() {
        configureAppearance()
    }
    
    var body: some Scene {
        WindowGroup {
            ZStack {
                Color.mcBgBase.ignoresSafeArea()
                
                if appState.isStreaming {
                    StreamingView(appState: appState)
                        .transition(.opacity)
                } else {
                    HomeView(appState: appState)
                        .transition(.opacity)
                }
            }
            .preferredColorScheme(.dark)
        }
    }
    
    private func configureAppearance() {
        // Configure dark navigation bar appearance
        let navAppearance = UINavigationBarAppearance()
        navAppearance.configureWithOpaqueBackground()
        navAppearance.backgroundColor = UIColor(Color.mcBgBase)
        navAppearance.titleTextAttributes = [
            .foregroundColor: UIColor(Color.mcSilverBright),
            .font: UIFont.systemFont(ofSize: 18, weight: .bold)
        ]
        navAppearance.largeTitleTextAttributes = [
            .foregroundColor: UIColor(Color.mcSilverBright),
            .font: UIFont.systemFont(ofSize: 28, weight: .bold)
        ]
        navAppearance.shadowColor = UIColor(Color.mcBgHover)
        
        UINavigationBar.appearance().standardAppearance = navAppearance
        UINavigationBar.appearance().compactAppearance = navAppearance
        UINavigationBar.appearance().scrollEdgeAppearance = navAppearance
        UINavigationBar.appearance().tintColor = UIColor(Color.mcSilverBright)
    }
}
