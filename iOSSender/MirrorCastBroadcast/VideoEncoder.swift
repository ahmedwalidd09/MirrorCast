//
//  VideoEncoder.swift
//  MirrorCastBroadcast
//
//  Hardware-accelerated H.264 video compression using VideoToolbox.
//  Configures Baseline Profile, 60 FPS, 2.0s keyframe interval, and AVCC NAL framing.
//

import Foundation
import VideoToolbox
import CoreMedia

public protocol VideoEncoderDelegate: AnyObject {
    func videoEncoder(_ encoder: VideoEncoder, didOutputNALU data: Data, isKeyFrame: Bool)
    func videoEncoder(_ encoder: VideoEncoder, didFailWithError error: Error)
}

public final class VideoEncoder {
    public weak var delegate: VideoEncoderDelegate?
    
    private var session: VTCompressionSession?
    private var width: Int32 = 0
    private var height: Int32 = 0
    private var bitrateBps: Int32 = 8_000_000
    private var fps: Int32 = 60
    private var keyframeIntervalDuration: Double = 2.0
    
    private var isConfigured = false
    private let queue = DispatchQueue(label: "com.mirrorcast.encoder.queue", qos: .userInteractive)
    
    // Header caching
    private var cachedSPS: Data?
    private var cachedPPS: Data?
    
    public init(width: Int = 1080, height: Int = 1920, bitrateBps: Int = 8_000_000, fps: Int = 60) {
        self.width = Int32(width)
        self.height = Int32(height)
        self.bitrateBps = Int32(bitrateBps)
        self.fps = Int32(fps)
    }
    
    deinit {
        invalidate()
    }
    
    public func updateBitrate(_ newBitrateBps: Int) {
        queue.async { [weak self] in
            guard let self = self, let session = self.session else { return }
            self.bitrateBps = Int32(newBitrateBps)
            let bitrateNumber = CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &self.bitrateBps)
            VTSessionSetProperty(session, key: kVTCompressionPropertyKey_AverageBitRate, value: bitrateNumber)
            
            // Adjust data rate limits (1.2x average per second)
            let bytesPerSec = Int32(Double(self.bitrateBps) / 8.0 * 1.2)
            let oneSec: Int32 = 1
            let limitArray: [CFNumber] = [
                CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &bytesPerSec)!,
                CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &oneSec)!
            ]
            let limits = CFArrayCreate(kCFAllocatorDefault, Unmanaged.passUnretained(limitArray).toOpaque() as? UnsafeMutablePointer<UnsafeRawPointer?>, 2, nil)
            VTSessionSetProperty(session, key: kVTCompressionPropertyKey_DataRateLimits, value: limits)
        }
    }
    
    public func forceKeyframe() {
        queue.async { [weak self] in
            guard let self = self, let session = self.session else { return }
            let frameProperties: [CFString: Any] = [
                kVTEncodeFrameOptionKey_ForceKeyFrame: true
            ]
            // The next encoded frame will be an IDR keyframe
            VTSessionSetProperty(session, key: kVTCompressionPropertyKey_MaxKeyFrameInterval, value: 1 as CFNumber)
        }
    }
    
    public func encode(sampleBuffer: CMSampleBuffer) {
        queue.async { [weak self] in
            guard let self = self else { return }
            
            guard let imageBuffer = CMSampleBufferGetImageBuffer(sampleBuffer) else { return }
            let frameWidth = Int32(CVPixelBufferGetWidth(imageBuffer))
            let frameHeight = Int32(CVPixelBufferGetHeight(imageBuffer))
            
            // Initialize or reconfigure if resolution changed
            if self.session == nil || frameWidth != self.width || frameHeight != self.height {
                self.width = frameWidth
                self.height = frameHeight
                self.setupCompressionSession()
            }
            
            guard let session = self.session else { return }
            
            let pts = CMSampleBufferGetPresentationTimeStamp(sampleBuffer)
            let duration = CMSampleBufferGetDuration(sampleBuffer)
            
            var flags: VTEncodeInfoFlags = []
            let status = VTCompressionSessionEncodeFrame(
                session,
                imageBuffer: imageBuffer,
                presentationTimeStamp: pts,
                duration: duration,
                frameProperties: nil,
                infoFlagsOut: &flags,
                outputHandler: { [weak self] status, infoFlags, outputSampleBuffer in
                    guard let self = self else { return }
                    if status != noErr {
                        let error = NSError(domain: "VideoEncoder", code: Int(status), userInfo: [NSLocalizedDescriptionKey: "Encode frame failed with code \(status)"])
                        self.delegate?.videoEncoder(self, didFailWithError: error)
                        return
                    }
                    guard let outputSampleBuffer = outputSampleBuffer else { return }
                    self.handleEncodedFrame(outputSampleBuffer)
                }
            )
            
            if status != noErr {
                print("[VideoEncoder] VTCompressionSessionEncodeFrame failed: \(status)")
            }
        }
    }
    
    public func invalidate() {
        queue.sync {
            if let session = session {
                VTCompressionSessionInvalidate(session)
                self.session = nil
            }
            cachedSPS = nil
            cachedPPS = nil
        }
    }
    
    // MARK: - Compression Session Setup
    
    private func setupCompressionSession() {
        if let existing = session {
            VTCompressionSessionInvalidate(existing)
            session = nil
        }
        
        var newSession: VTCompressionSession?
        let status = VTCompressionSessionCreate(
            allocator: kCFAllocatorDefault,
            width: width,
            height: height,
            codecType: kCMVideoCodecType_H264,
            encoderSpecification: nil,
            imageBufferAttributes: nil,
            compressedDataAllocator: nil,
            outputCallback: nil,
            refcon: nil,
            compressionSessionOut: &newSession
        )
        
        guard status == noErr, let session = newSession else {
            let error = NSError(domain: "VideoEncoder", code: Int(status), userInfo: [NSLocalizedDescriptionKey: "Failed to create VTCompressionSession: \(status)"])
            delegate?.videoEncoder(self, didFailWithError: error)
            return
        }
        
        self.session = session
        
        // 1. H.264 Baseline Profile Auto Level
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_ProfileLevel, value: kVTProfileLevel_H264_Baseline_AutoLevel)
        
        // 2. Real-time encoding for minimal screen mirroring delay
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_RealTime, value: kCFBooleanTrue)
        
        // 3. Disable frame reordering (No B-Frames) to eliminate display buffer latency
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_AllowFrameReordering, value: kCFBooleanFalse)
        
        // 4. Bitrate configuration
        var bitrate = bitrateBps
        let bitrateNumber = CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &bitrate)
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_AverageBitRate, value: bitrateNumber)
        
        // Data rate limit (1.2x limit)
        var limitBytes = Int32(Double(bitrateBps) / 8.0 * 1.2)
        var oneSecond: Int32 = 1
        let limitArray: [CFNumber] = [
            CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &limitBytes)!,
            CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &oneSecond)!
        ]
        let limits = CFArrayCreate(kCFAllocatorDefault, Unmanaged.passUnretained(limitArray).toOpaque() as? UnsafeMutablePointer<UnsafeRawPointer?>, 2, nil)
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_DataRateLimits, value: limits)
        
        // 5. Expected Frame Rate
        var targetFps = fps
        let fpsNumber = CFNumberCreate(kCFAllocatorDefault, .sInt32Type, &targetFps)
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_ExpectedFrameRate, value: fpsNumber)
        
        // 6. Max Keyframe Interval (2.0s as specified in PROTOCOL.md)
        var intervalSeconds = keyframeIntervalDuration
        let intervalNumber = CFNumberCreate(kCFAllocatorDefault, .sFloat64Type, &intervalSeconds)
        VTSessionSetProperty(session, key: kVTCompressionPropertyKey_MaxKeyFrameIntervalDuration, value: intervalNumber)
        
        VTCompressionSessionPrepareToEncodeFrames(session)
    }
    
    // MARK: - Frame Parsing & Length-Prefixed NAL Extraction
    
    private func handleEncodedFrame(_ sampleBuffer: CMSampleBuffer) {
        guard let formatDesc = CMSampleBufferGetFormatDescription(sampleBuffer) else { return }
        
        // Check if sample buffer contains an IDR Keyframe
        let isKeyFrame: Bool
        if let attachments = CMSampleBufferGetSampleAttachmentsArray(sampleBuffer, createIfNecessary: false) as? [[CFString: Any]],
           let first = attachments.first {
            let notSync = (first[kCMSampleAttachmentKey_NotSync] as? Bool) ?? false
            isKeyFrame = !notSync
        } else {
            isKeyFrame = false
        }
        
        // For Keyframes: Extract and emit SPS and PPS parameter sets with 4-byte BE length headers
        if isKeyFrame {
            extractAndSendParameterSets(formatDesc)
        }
        
        // Extract elementary stream NAL units from CMBlockBuffer
        guard let blockBuffer = CMSampleBufferGetDataBuffer(sampleBuffer) else { return }
        
        var totalLength: Int = 0
        var dataPointer: UnsafeMutablePointer<Int8>?
        let blockStatus = CMBlockBufferGetDataPointer(
            blockBuffer,
            atOffset: 0,
            lengthAtOffsetOut: nil,
            totalLengthOut: &totalLength,
            dataPointerOut: &dataPointer
        )
        
        guard blockStatus == noErr, let bufferPtr = dataPointer else { return }
        
        // VideoToolbox produces AVCC format: [4-byte Big-Endian Length][NAL Payload]...
        var bufferOffset = 0
        let avccHeaderLength = 4
        
        while bufferOffset < (totalLength - avccHeaderLength) {
            // Read 4-byte big-endian NAL length
            var naluLength32: UInt32 = 0
            memcpy(&naluLength32, bufferPtr.advanced(by: bufferOffset), avccHeaderLength)
            let naluLength = Int(CFSwapInt32BigToHost(naluLength32))
            
            if naluLength <= 0 || (bufferOffset + avccHeaderLength + naluLength) > totalLength {
                // Malformed packet or corrupted buffer boundary
                break
            }
            
            // Extract the NAL unit payload
            let naluPayloadPtr = bufferPtr.advanced(by: bufferOffset + avccHeaderLength)
            let naluPayloadData = Data(bytes: naluPayloadPtr, count: naluLength)
            
            // Build the PROTOCOL.md frame: [4-byte BE length][NAL unit payload]
            var framedPacket = Data(capacity: 4 + naluLength)
            var beLength = CFSwapInt32HostToBig(UInt32(naluLength))
            framedPacket.append(UnsafeBufferPointer(start: &beLength, count: 1))
            framedPacket.append(naluPayloadData)
            
            delegate?.videoEncoder(self, didOutputNALU: framedPacket, isKeyFrame: isKeyFrame)
            
            bufferOffset += avccHeaderLength + naluLength
        }
    }
    
    private func extractAndSendParameterSets(_ formatDesc: CMVideoFormatDescription) {
        var spsCount: Int = 0
        CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
            formatDesc,
            parameterSetIndex: 0,
            parameterSetPointerOut: nil,
            parameterSetSizeOut: nil,
            parameterSetCountOut: &spsCount,
            nalUnitHeaderLengthOut: nil
        )
        
        // Extract SPS (index 0)
        var spsPointer: UnsafePointer<UInt8>?
        var spsSize: Int = 0
        let spsStatus = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
            formatDesc,
            parameterSetIndex: 0,
            parameterSetPointerOut: &spsPointer,
            parameterSetSizeOut: &spsSize,
            parameterSetCountOut: nil,
            nalUnitHeaderLengthOut: nil
        )
        
        if spsStatus == noErr, let spsPtr = spsPointer, spsSize > 0 {
            let spsData = Data(bytes: spsPtr, count: spsSize)
            self.cachedSPS = spsData
            
            // Prepend 4-byte big-endian length
            var framedSPS = Data(capacity: 4 + spsSize)
            var beLength = CFSwapInt32HostToBig(UInt32(spsSize))
            framedSPS.append(UnsafeBufferPointer(start: &beLength, count: 1))
            framedSPS.append(spsData)
            
            delegate?.videoEncoder(self, didOutputNALU: framedSPS, isKeyFrame: true)
        }
        
        // Extract PPS (index 1)
        var ppsPointer: UnsafePointer<UInt8>?
        var ppsSize: Int = 0
        let ppsStatus = CMVideoFormatDescriptionGetH264ParameterSetAtIndex(
            formatDesc,
            parameterSetIndex: 1,
            parameterSetPointerOut: &ppsPointer,
            parameterSetSizeOut: &ppsSize,
            parameterSetCountOut: nil,
            nalUnitHeaderLengthOut: nil
        )
        
        if ppsStatus == noErr, let ppsPtr = ppsPointer, ppsSize > 0 {
            let ppsData = Data(bytes: ppsPtr, count: ppsSize)
            self.cachedPPS = ppsData
            
            // Prepend 4-byte big-endian length
            var framedPPS = Data(capacity: 4 + ppsSize)
            var beLength = CFSwapInt32HostToBig(UInt32(ppsSize))
            framedPPS.append(UnsafeBufferPointer(start: &beLength, count: 1))
            framedPPS.append(ppsData)
            
            delegate?.videoEncoder(self, didOutputNALU: framedPPS, isKeyFrame: true)
        }
    }
}
