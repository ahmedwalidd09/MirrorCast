//
//  SampleHandler.swift
//  MirrorCastBroadcast
//
//  ReplayKit Broadcast Upload Extension Handler for MirrorCast.
//

import ReplayKit
import VideoToolbox

public class SampleHandler: RPBroadcastSampleHandler {
    private var videoEncoder: VideoEncoder?
    private var audioEncoder: AudioEncoder?
    private var streamClient: BroadcastStreamClient?
    
    private var isPaused: Bool = false
    private var stopObserver: NSObjectProtocol?
    private var pauseObserver: NSObjectProtocol?
    
    // MARK: - Lifecycle
    
    override public func broadcastStarted(withSetupInfo setupInfo: [String : NSObject]?) {
        // Read target connection and encoding parameters from shared App Group
        let shared = SharedConfig.shared
        let host = shared.receiverHost
        let port = shared.receiverPort
        let width = shared.streamWidth
        let height = shared.streamHeight
        let fps = shared.streamFps
        let bitrateBps = shared.streamBitrateBps
        let mode = shared.connectionMode
        
        shared.isBroadcasting = true
        shared.isPaused = false
        
        // 1. Initialize Network Streaming Client
        let client = BroadcastStreamClient()
        client.delegate = self
        self.streamClient = client
        
        // 2. Initialize Hardware Video Encoder
        let encoder = VideoEncoder(
            width: width,
            height: height,
            bitrateBps: bitrateBps,
            fps: fps
        )
        encoder.delegate = self
        self.videoEncoder = encoder
        
        // 3. Initialize Audio Encoder
        let aEncoder = AudioEncoder()
        aEncoder.delegate = self
        self.audioEncoder = aEncoder
        
        // Connect to Receiver
        client.connect(
            host: host,
            port: port,
            deviceName: UIDevice.current.name,
            width: width,
            height: height,
            fps: fps,
            mode: mode
        )
        
        // Setup Darwin Notification Observers for external main app controls
        setupDarwinObservers()
        
        // Notify main app that broadcast has started
        shared.postDarwinNotification(SharedConfig.notificationBroadcastStarted)
    }
    
    override public func broadcastPaused() {
        isPaused = true
        SharedConfig.shared.isPaused = true
    }
    
    override public func broadcastResumed() {
        isPaused = false
        SharedConfig.shared.isPaused = false
        // Force keyframe on resume to immediately recover receiver display
        videoEncoder?.forceKeyframe()
    }
    
    override public func broadcastFinished() {
        tearDown()
        SharedConfig.shared.isBroadcasting = false
        SharedConfig.shared.postDarwinNotification(SharedConfig.notificationBroadcastStopped)
    }
    
    // MARK: - Sample Buffer Processing
    
    override public func processSampleBuffer(_ sampleBuffer: CMSampleBuffer, with sampleBufferType: RPSampleBufferType) {
        guard !isPaused else { return }
        
        switch sampleBufferType {
        case .video:
            videoEncoder?.encode(sampleBuffer: sampleBuffer)
            
        case .audioApp:
            audioEncoder?.encode(sampleBuffer: sampleBuffer)
            
        case .audioMic:
            // Optional microphone forwarding
            break
            
        @unknown default:
            break
        }
    }
    
    // MARK: - Private Helpers
    
    private func setupDarwinObservers() {
        stopObserver = SharedConfig.shared.observeDarwinNotification(SharedConfig.notificationStopRequest) { [weak self] in
            guard let self = self else { return }
            self.finishBroadcastWithError(NSError(domain: "MirrorCast", code: 0, userInfo: [NSLocalizedDescriptionKey: "Mirroring stopped by user."]))
        }
        
        pauseObserver = SharedConfig.shared.observeDarwinNotification(SharedConfig.notificationPauseToggled) { [weak self] in
            guard let self = self else { return }
            self.isPaused = SharedConfig.shared.isPaused
            if !self.isPaused {
                self.videoEncoder?.forceKeyframe()
            }
        }
    }
    
    private func tearDown() {
        videoEncoder?.invalidate()
        videoEncoder = nil
        
        audioEncoder?.invalidate()
        audioEncoder = nil
        
        streamClient?.disconnect()
        streamClient = nil
    }
}

// MARK: - VideoEncoderDelegate

extension SampleHandler: VideoEncoderDelegate {
    public func videoEncoder(_ encoder: VideoEncoder, didOutputNALU data: Data, isKeyFrame: Bool) {
        streamClient?.sendNALU(packet: data, isKeyFrame: isKeyFrame)
    }
    
    public func videoEncoder(_ encoder: VideoEncoder, didFailWithError error: Error) {
        print("[SampleHandler] VideoEncoder failed: \(error.localizedDescription)")
    }
}

// MARK: - AudioEncoderDelegate

extension SampleHandler: AudioEncoderDelegate {
    public func audioEncoder(_ encoder: AudioEncoder, didOutputAudioPacket data: Data) {
        // Stream audio packets if needed
    }
    
    public func audioEncoder(_ encoder: AudioEncoder, didFailWithError error: Error) {
        print("[SampleHandler] AudioEncoder failed: \(error.localizedDescription)")
    }
}

// MARK: - BroadcastStreamClientDelegate

extension SampleHandler: BroadcastStreamClientDelegate {
    public func broadcastStreamClientDidConnect(_ client: BroadcastStreamClient) {
        print("[SampleHandler] Connected and handshaked with receiver.")
        // Force initial IDR keyframe with SPS/PPS immediately after handshake
        videoEncoder?.forceKeyframe()
    }
    
    public func broadcastStreamClient(_ client: BroadcastStreamClient, didFailWithError error: Error) {
        SharedConfig.shared.lastError = "Connection lost: \(error.localizedDescription)"
        finishBroadcastWithError(error)
    }
    
    public func broadcastStreamClientDidDisconnect(_ client: BroadcastStreamClient) {
        finishBroadcastWithError(NSError(domain: "MirrorCast", code: -1, userInfo: [NSLocalizedDescriptionKey: "Receiver closed connection"]))
    }
}
