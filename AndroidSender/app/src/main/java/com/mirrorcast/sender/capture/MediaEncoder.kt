package com.mirrorcast.sender.capture

import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.os.Bundle
import android.view.Surface
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import timber.log.Timber
import java.nio.ByteBuffer
import java.util.concurrent.atomic.AtomicBoolean

/**
 * Encapsulates Android hardware H.264 MediaCodec encoder configured for low-latency
 * screen streaming (MIME video/avc, Baseline Profile, 60fps, 2s keyframe interval).
 */
class MediaEncoder(
    private val width: Int,
    private val height: Int,
    private var bitrateBps: Int = 8_000_000,
    private val fps: Int = 60,
    private val onPacketReady: (framedPacket: ByteArray, isKeyframe: Boolean) -> Unit
) {

    private var codec: MediaCodec? = null
    private var inputSurface: Surface? = null
    private var encodingJob: Job? = null

    private val isRunning = AtomicBoolean(false)
    private val isPaused = AtomicBoolean(false)

    // Cached SPS and PPS for retransmission on keyframes if needed
    private var cachedSps: ByteArray? = null
    private var cachedPps: ByteArray? = null

    val surface: Surface? get() = inputSurface

    fun start(scope: CoroutineScope) {
        if (isRunning.getAndSet(true)) return

        try {
            val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height).apply {
                setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
                setInteger(MediaFormat.KEY_BIT_RATE, bitrateBps)
                setInteger(MediaFormat.KEY_FRAME_RATE, fps)
                setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 2) // 2s IDR interval
                setInteger(MediaFormat.KEY_PROFILE, MediaCodecInfo.CodecProfileLevel.AVCProfileBaseline)
                setInteger(MediaFormat.KEY_LEVEL, MediaCodecInfo.CodecProfileLevel.AVCLevel4)
                setInteger(MediaFormat.KEY_BITRATE_MODE, MediaCodecInfo.EncoderCapabilities.BITRATE_MODE_VBR)
                setLong(MediaFormat.KEY_REPEAT_PREVIOUS_FRAME_AFTER, 50_000L) // 50ms keepalive
            }

            val encoder = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
            encoder.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)
            inputSurface = encoder.createInputSurface()
            encoder.start()
            codec = encoder

            Timber.i("MediaEncoder started: %dx%d @ %d bps, %d FPS", width, height, bitrateBps, fps)

            encodingJob = scope.launch(Dispatchers.IO) {
                drainEncoderLoop(this)
            }
        } catch (e: Exception) {
            Timber.e(e, "Failed to initialize MediaCodec encoder")
            stop()
            throw e
        }
    }

    private fun drainEncoderLoop(scope: CoroutineScope) {
        val encoder = codec ?: return
        val bufferInfo = MediaCodec.BufferInfo()

        while (scope.isActive && isRunning.get()) {
            try {
                val outputBufferIndex = encoder.dequeueOutputBuffer(bufferInfo, 10_000L) // 10ms timeout

                if (outputBufferIndex >= 0) {
                    val outputBuffer = encoder.getOutputBuffer(outputBufferIndex)
                    if (outputBuffer != null && bufferInfo.size > 0) {
                        handleEncodedBuffer(outputBuffer, bufferInfo)
                    }
                    encoder.releaseOutputBuffer(outputBufferIndex, false)
                } else if (outputBufferIndex == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                    val newFormat = encoder.outputFormat
                    Timber.d("Encoder format changed: %s", newFormat)
                    extractSpsPpsFromFormat(newFormat)
                }
            } catch (e: Exception) {
                if (isRunning.get()) {
                    Timber.e(e, "Error draining encoder output buffer")
                }
                break
            }
        }
        Timber.d("MediaEncoder drain loop exited")
    }

    private fun handleEncodedBuffer(buffer: ByteBuffer, bufferInfo: MediaCodec.BufferInfo) {
        val isConfig = (bufferInfo.flags and MediaCodec.BUFFER_FLAG_CODEC_CONFIG) != 0
        val isKeyFrame = (bufferInfo.flags and MediaCodec.BUFFER_FLAG_KEY_FRAME) != 0

        // Extract Annex-B NAL units
        val nalUnits = NalParser.extractRawNalUnits(buffer, bufferInfo.offset, bufferInfo.size)

        for (nal in nalUnits) {
            if (NalParser.isSps(nal)) {
                cachedSps = nal
            } else if (NalParser.isPps(nal)) {
                cachedPps = nal
            }

            // If paused, don't stream non-config frames to network
            if (isPaused.get() && !isConfig) {
                continue
            }

            val packet = NalParser.createLengthPrefixedPacket(nal)
            val isIdr = NalParser.isIdrSlice(nal) || isKeyFrame
            onPacketReady(packet, isIdr)
        }
    }

    private fun extractSpsPpsFromFormat(format: MediaFormat) {
        val spsBuf = format.getByteBuffer("csd-0")
        val ppsBuf = format.getByteBuffer("csd-1")

        if (spsBuf != null) {
            val spsNals = NalParser.extractRawNalUnits(spsBuf, 0, spsBuf.remaining())
            spsNals.firstOrNull { NalParser.isSps(it) }?.let {
                cachedSps = it
                val packet = NalParser.createLengthPrefixedPacket(it)
                onPacketReady(packet, false)
            }
        }
        if (ppsBuf != null) {
            val ppsNals = NalParser.extractRawNalUnits(ppsBuf, 0, ppsBuf.remaining())
            ppsNals.firstOrNull { NalParser.isPps(it) }?.let {
                cachedPps = it
                val packet = NalParser.createLengthPrefixedPacket(it)
                onPacketReady(packet, false)
            }
        }
    }

    fun setBitrate(newBitrateBps: Int) {
        this.bitrateBps = newBitrateBps
        val encoder = codec ?: return
        try {
            val bundle = Bundle().apply {
                putInt(MediaCodec.PARAMETER_KEY_VIDEO_BITRATE, newBitrateBps)
            }
            encoder.setParameters(bundle)
            Timber.i("Dynamic bitrate updated to %d bps", newBitrateBps)
        } catch (e: Exception) {
            Timber.w(e, "Failed to update dynamic bitrate")
        }
    }

    fun requestKeyframe() {
        val encoder = codec ?: return
        try {
            val bundle = Bundle().apply {
                putInt(MediaCodec.PARAMETER_KEY_REQUEST_SYNC_FRAME, 0)
            }
            encoder.setParameters(bundle)
            Timber.d("Requested immediate sync (IDR) frame")
        } catch (e: Exception) {
            Timber.w(e, "Failed to request sync frame")
        }
    }

    fun pause() {
        isPaused.set(true)
        Timber.i("MediaEncoder paused")
    }

    fun resume() {
        isPaused.set(false)
        requestKeyframe()
        Timber.i("MediaEncoder resumed")
    }

    fun stop() {
        if (!isRunning.getAndSet(false)) return

        encodingJob?.cancel()
        encodingJob = null

        try {
            codec?.stop()
        } catch (e: Exception) {
            Timber.w(e, "Error stopping MediaCodec")
        }

        try {
            codec?.release()
        } catch (e: Exception) {
            Timber.w(e, "Error releasing MediaCodec")
        }
        codec = null

        try {
            inputSurface?.release()
        } catch (e: Exception) {
            Timber.w(e, "Error releasing input surface")
        }
        inputSurface = null

        Timber.i("MediaEncoder stopped successfully")
    }
}
