package com.mirrorcast.sender.network

import android.os.Build
import com.mirrorcast.sender.model.ControlMessage
import com.mirrorcast.sender.model.HandshakeRequest
import com.mirrorcast.sender.model.HandshakeResponse
import com.mirrorcast.sender.model.ProtocolConstants
import com.mirrorcast.sender.model.StreamStats
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import timber.log.Timber
import java.io.BufferedOutputStream
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.OutputStream
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicLong

/**
 * Handles the primary streaming TCP connection (Port 59100) to the MirrorCast receiver.
 * Executes JSON handshake, streams 4-byte length prefixed NAL packets,
 * manages the secondary control channel (Port 59101), tracks streaming metrics,
 * and performs automatic exponential backoff reconnection (1s, 2s, 4s).
 */
class StreamingClient(
    private val host: String,
    private val streamPort: Int = ProtocolConstants.DEFAULT_STREAM_PORT,
    private val mode: String = "wifi", // "wifi" or "wifi-direct"
    private val width: Int,
    private val height: Int,
    private val targetFps: Int = 60,
    private val onConnectionLost: (canRetry: Boolean) -> Unit,
    private val onKeyframeRequested: () -> Unit
) {

    private var socket: Socket? = null
    private var outputStream: OutputStream? = null
    private var controlChannel: ControlChannelClient? = null

    private val isConnected = AtomicBoolean(false)
    private val isPaused = AtomicBoolean(false)
    private val writeMutex = Mutex()

    // Metrics counters
    private val packetsCounter = AtomicLong(0)
    private val bytesCounter = AtomicLong(0)
    private var lastBytesCount = 0L
    private var lastFramesCount = 0L
    private var lastMetricTimestamp = System.currentTimeMillis()
    private var currentRtt = 0L

    private val _streamStats = MutableStateFlow(StreamStats(width = width, height = height, fps = targetFps.toDouble()))
    val streamStats: StateFlow<StreamStats> = _streamStats.asStateFlow()

    private var metricsJob: Job? = null
    private val reconnectDelays = listOf(1000L, 2000L, 4000L) // 1s, 2s, 4s exponential backoff

    suspend fun connect(scope: CoroutineScope): Boolean = withContext(Dispatchers.IO) {
        var attempt = 0
        while (attempt <= reconnectDelays.size) {
            try {
                attemptConnection(scope)
                return@withContext true
            } catch (e: Exception) {
                Timber.w(e, "Connection attempt %d failed to %s:%d", attempt + 1, host, streamPort)
                if (attempt < reconnectDelays.size) {
                    val delayMs = reconnectDelays[attempt]
                    Timber.i("Backing off for %d ms before reconnecting...", delayMs)
                    delay(delayMs)
                    attempt++
                } else {
                    Timber.e("Exhausted all %d reconnection attempts to %s", reconnectDelays.size + 1, host)
                    onConnectionLost(false)
                    return@withContext false
                }
            }
        }
        return@withContext false
    }

    private suspend fun attemptConnection(scope: CoroutineScope) {
        closeSockets()

        Timber.i("Opening primary streaming socket to %s:%d (mode: %s)", host, streamPort, mode)
        val s = Socket()
        s.tcpNoDelay = true
        s.sendBufferSize = 512 * 1024 // 512 KB socket buffer for smooth video transmission
        s.connect(InetSocketAddress(host, streamPort), 5000)
        socket = s

        val out = BufferedOutputStream(s.getOutputStream(), 128 * 1024)
        outputStream = out

        val inReader = BufferedReader(InputStreamReader(s.getInputStream(), Charsets.UTF_8))

        // 1. Send JSON handshake
        val handshakeReq = HandshakeRequest(
            device = "${Build.MANUFACTURER} ${Build.MODEL}",
            os = "android",
            width = width,
            height = height,
            fps = targetFps,
            mode = mode
        )
        val reqJson = handshakeReq.toJsonString() + "\n"
        Timber.i("Sending Handshake: %s", reqJson.trim())
        out.write(reqJson.toByteArray(Charsets.UTF_8))
        out.flush()

        // 2. Await Handshake response
        val responseLine = inReader.readLine() ?: throw IllegalStateException("Empty response from receiver")
        Timber.i("Received Handshake response: %s", responseLine)
        val handshakeResp = HandshakeResponse.fromJson(responseLine)

        if (!handshakeResp.isOk) {
            throw IllegalStateException("Handshake rejected: ${handshakeResp.errorMessage ?: "Status not ok"}")
        }

        // 3. Connect secondary control channel
        val controlPort = handshakeResp.controlPort
        val ctrl = ControlChannelClient(
            host = host,
            port = controlPort,
            onConnectionDropped = {
                Timber.w("Control channel dropped")
                handleSocketDrop(scope)
            },
            onRemotePauseResume = { remotePaused ->
                if (remotePaused) pause() else resume()
            },
            onRttUpdated = { rtt ->
                currentRtt = rtt
            }
        )
        ctrl.connect(scope)
        controlChannel = ctrl

        isConnected.set(true)
        startMetricsTracker(scope)
        onKeyframeRequested()
    }

    private fun startMetricsTracker(scope: CoroutineScope) {
        metricsJob?.cancel()
        metricsJob = scope.launch(Dispatchers.Default) {
            while (isActive && isConnected.get()) {
                delay(1000L)
                val now = System.currentTimeMillis()
                val dt = (now - lastMetricTimestamp) / 1000.0
                if (dt > 0.5) {
                    val currentBytes = bytesCounter.get()
                    val currentPackets = packetsCounter.get()

                    val bytesDelta = currentBytes - lastBytesCount
                    val packetsDelta = currentPackets - lastFramesCount

                    val bitrateMbps = (bytesDelta * 8.0) / (dt * 1_000_000.0)
                    val fps = packetsDelta / dt

                    lastBytesCount = currentBytes
                    lastFramesCount = currentPackets
                    lastMetricTimestamp = now

                    _streamStats.value = StreamStats(
                        width = width,
                        height = height,
                        fps = fps,
                        bitrateMbps = bitrateMbps,
                        packetsSent = currentPackets,
                        bytesSent = currentBytes,
                        roundTripTimeMs = currentRtt
                    )
                }
            }
        }
    }

    suspend fun sendFramedPacket(packet: ByteArray) {
        if (!isConnected.get() || isPaused.get()) return

        try {
            writeMutex.withLock {
                val out = outputStream ?: return
                out.write(packet)
                out.flush()
            }
            packetsCounter.incrementAndGet()
            bytesCounter.addAndGet(packet.size.toLong())
        } catch (e: Exception) {
            Timber.w(e, "Error sending video packet")
            isConnected.set(false)
            onConnectionLost(true)
        }
    }

    private fun handleSocketDrop(scope: CoroutineScope) {
        if (isConnected.getAndSet(false)) {
            scope.launch {
                Timber.w("Streaming connection dropped, attempting reconnection...")
                val reconnected = connect(scope)
                if (!reconnected) {
                    onConnectionLost(false)
                }
            }
        }
    }

    fun pause() {
        if (isPaused.getAndSet(true)) return
        controlChannel?.sendMessage(ControlMessage.Pause)
        Timber.i("StreamingClient paused")
    }

    fun resume() {
        if (!isPaused.getAndSet(false)) return
        controlChannel?.sendMessage(ControlMessage.Resume)
        onKeyframeRequested()
        Timber.i("StreamingClient resumed")
    }

    fun sendConfigUpdate(newWidth: Int, newHeight: Int, newBitrate: Int) {
        controlChannel?.sendMessage(
            ControlMessage.ConfigUpdate(
                width = newWidth,
                height = newHeight,
                bitrate = newBitrate,
                fps = targetFps
            )
        )
    }

    private fun closeSockets() {
        try {
            outputStream?.close()
        } catch (ignored: Exception) {}
        try {
            socket?.close()
        } catch (ignored: Exception) {}
        socket = null
        outputStream = null

        controlChannel?.close()
        controlChannel = null
    }

    fun disconnect() {
        if (!isConnected.getAndSet(false)) return

        metricsJob?.cancel()
        metricsJob = null

        // Send graceful disconnect signal
        try {
            controlChannel?.sendMessage(ControlMessage.Disconnect("user_terminated"))
        } catch (e: Exception) {
            Timber.w(e, "Error sending disconnect message")
        }

        closeSockets()
        Timber.i("StreamingClient disconnected")
    }
}
