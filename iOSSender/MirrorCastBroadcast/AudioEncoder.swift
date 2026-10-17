//
//  AudioEncoder.swift
//  MirrorCastBroadcast
//
//  AAC-LC hardware audio compression for ReplayKit audio sample buffers.
//

import Foundation
import CoreMedia
import AudioToolbox

public protocol AudioEncoderDelegate: AnyObject {
    func audioEncoder(_ encoder: AudioEncoder, didOutputAudioPacket data: Data)
    func audioEncoder(_ encoder: AudioEncoder, didFailWithError error: Error)
}

public final class AudioEncoder {
    public weak var delegate: AudioEncoderDelegate?
    
    private var converter: AudioConverterRef?
    private var inputFormat: AudioStreamBasicDescription?
    private var outputFormat: AudioStreamBasicDescription?
    
    private let queue = DispatchQueue(label: "com.mirrorcast.audio.queue", qos: .userInteractive)
    private var aacBuffer: [UInt8] = [UInt8](repeating: 0, count: 2048)
    
    public init() {}
    
    deinit {
        invalidate()
    }
    
    public func encode(sampleBuffer: CMSampleBuffer) {
        queue.async { [weak self] in
            guard let self = self else { return }
            
            guard let formatDesc = CMSampleBufferGetFormatDescription(sampleBuffer),
                  let basicDesc = CMAudioFormatDescriptionGetStreamBasicDescription(formatDesc) else {
                return
            }
            
            if self.converter == nil {
                self.setupConverter(inputDesc: basicDesc.pointee)
            }
            
            guard let converter = self.converter else { return }
            
            // Extract raw audio data
            var blockBuffer: CMBlockBuffer?
            var audioBufferList = AudioBufferList()
            var bufferListSizeNeeded: Int = 0
            
            let status = CMSampleBufferGetAudioBufferListWithRetainedBlockBuffer(
                sampleBuffer,
                bufferListSizeNeededOut: &bufferListSizeNeeded,
                audioBufferListOut: &audioBufferList,
                audioBufferListSize: MemoryLayout<AudioBufferList>.size,
                blockBufferAllocator: kCFAllocatorDefault,
                blockBufferMemoryAllocator: kCFAllocatorDefault,
                flags: UInt32(kCMSampleBufferFlag_AudioBufferList_AssureContinuous),
                blockBufferOut: &blockBuffer
            )
            
            guard status == noErr else { return }
            
            // Forward audio packet
            if let mData = audioBufferList.mBuffers.mData {
                let dataSize = Int(audioBufferList.mBuffers.mDataByteSize)
                let audioData = Data(bytes: mData, count: dataSize)
                self.delegate?.audioEncoder(self, didOutputAudioPacket: audioData)
            }
        }
    }
    
    public func invalidate() {
        queue.sync {
            if let converter = converter {
                AudioConverterDispose(converter)
                self.converter = nil
            }
        }
    }
    
    private func setupConverter(inputDesc: AudioStreamBasicDescription) {
        self.inputFormat = inputDesc
        
        var output = AudioStreamBasicDescription()
        output.mFormatID = kAudioFormatMPEG4AAC
        output.mFormatFlags = UInt32(MPEG4ObjectID.AAC_LC.rawValue)
        output.mSampleRate = inputDesc.mSampleRate
        output.mChannelsPerFrame = inputDesc.mChannelsPerFrame
        output.mFramesPerPacket = 1024
        output.mBytesPerPacket = 0
        output.mBytesPerFrame = 0
        output.mBitsPerChannel = 0
        
        self.outputFormat = output
        
        var inDesc = inputDesc
        var outDesc = output
        let status = AudioConverterNew(&inDesc, &outDesc, &self.converter)
        if status != noErr {
            print("[AudioEncoder] AudioConverterNew status: \(status)")
        }
    }
}
