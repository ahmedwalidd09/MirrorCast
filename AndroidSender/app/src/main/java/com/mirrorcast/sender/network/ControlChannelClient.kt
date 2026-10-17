package com.mirrorcast.sender.network

import com.mirrorcast.sender.model.ControlMessage
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import timber.log.Timber
import java.io.BufferedReader
import java.io.BufferedWriter
import java.io.InputStreamReader
import java.io.OutputStreamWriter
import java.net.InetSocketAddress
import java.net.Socket
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger

/**
 * Manages the secondary bidirectional control socket on Port 59101.
 * Handles heartbeats (ping/pong every 2s), dynamic config updates,
 * pause/resume signals, and timeout detection.
 */
class ControlChannelClient(
    private val host: String,
    private val port: Int,
    private val onConnectionDropped: () -> Unit,
    private val onRemotePauseResume: ((isPaused: Boolean) -> Unit)? = null,
    private val onRttUpdated: ((rttMs: Long) -> Unit)? = null
) {

    private var socket: Socket? = null
    private var reader: BufferedReader? = null
    private var writer: BufferedWriter? = null

    private val isConnected = AtomicBoolean(false)
    private val unacknowledgedPings = AtomicInteger(0)

    private var readerJob: Job? = null
    private var heartbeatJob: Job? = null

    suspend fun connect(scope: CoroutineScope) = withContext(Dispatchers.IO) {
        try {
            Timber.i("Connecting control channel to %s:%d", host, port)
            val s = Socket()
            s.tcpNoDelay = true
            s.connect(InetSocketAddress(host, port), 4000)
            socket = s

            reader = BufferedReader(InputStreamReader(s.getInputStream(), Charsets.UTF_8))
            writer = BufferedWriter(OutputStreamWriter(s.getOutputStream(), Charsets.UTF_8))

            isConnected.set(true)
            unacknowledgedPings.set(0)

            // Start incoming message reader
            readerJob = scope.launch(Dispatchers.IO) {
                readIncomingMessages()
            }

            // Start heartbeat ping loop
            heartbeatJob = scope.launch(Dispatchers.IO) {
                heartbeatLoop()
            }

            Timber.i("Control channel connected successfully to %s:%d", host, port)
        } catch (e: Exception) {
            Timber.e(e, "Failed to connect control channel to %s:%d", host, port)
            close()
            throw e
        }
    }

    private suspend fun readIncomingMessages() {
        val r = reader ?: return
        while (isConnected.get()) {
            try {
                val line = r.readLine() ?: break
                handleIncomingMessage(line)
            } catch (e: Exception) {
                if (isConnected.get()) {
                    Timber.w(e, "Control channel read error")
                }
                break
            }
        }
        if (isConnected.get()) {
            Timber.w("Control channel reader closed unexpectedly")
            notifyDropped()
        }
    }

    private fun handleIncomingMessage(line: String) {
        val msg = ControlMessage.parse(line) ?: return
        when (msg) {
            is ControlMessage.Pong -> {
                val now = System.currentTimeMillis()
                val rtt = if (msg.timestamp > 0) now - msg.timestamp else 0L
                unacknowledgedPings.set(0)
                onRttUpdated?.invoke(rtt)
                Timber.v("Received Pong (RTT: %d ms)", rtt)
            }
            is ControlMessage.Ping -> {
                // Respond immediately with Pong
                sendMessage(ControlMessage.Pong(msg.timestamp))
            }
            is ControlMessage.Pause -> {
                Timber.i("Received remote pause command")
                onRemotePauseResume?.invoke(true)
            }
            is ControlMessage.Resume -> {
                Timber.i("Received remote resume command")
                onRemotePauseResume?.invoke(false)
            }
            is ControlMessage.ConfigAck -> {
                Timber.d("Received config_ack: %s", msg.status)
            }
            is ControlMessage.Disconnect -> {
                Timber.i("Received remote disconnect: %s", msg.reason)
                notifyDropped()
            }
            else -> {}
        }
    }

    private suspend fun heartbeatLoop() {
        while (isConnected.get()) {
            delay(2000L) // 2s heartbeat interval
            if (!isConnected.get()) break

            val pending = unacknowledgedPings.incrementAndGet()
            if (pending > 3) {
                Timber.e("Control channel timeout: 3 consecutive pings dropped (6s)")
                notifyDropped()
                break
            }

            sendMessage(ControlMessage.Ping(System.currentTimeMillis()))
        }
    }

    fun sendMessage(msg: ControlMessage) {
        if (!isConnected.get()) return
        try {
            val jsonLine = msg.toJsonString() + "\n"
            synchronized(this) {
                writer?.write(jsonLine)
                writer?.flush()
            }
        } catch (e: Exception) {
            Timber.w(e, "Failed to send control message: %s", msg.type)
            if (isConnected.get()) {
                notifyDropped()
            }
        }
    }

    private fun notifyDropped() {
        if (isConnected.getAndSet(false)) {
            close()
            onConnectionDropped()
        }
    }

    fun close() {
        isConnected.set(false)
        readerJob?.cancel()
        heartbeatJob?.cancel()

        try {
            writer?.close()
        } catch (ignored: Exception) {}
        try {
            reader?.close()
        } catch (ignored: Exception) {}
        try {
            socket?.close()
        } catch (ignored: Exception) {}

        socket = null
        reader = null
        writer = null
        Timber.i("ControlChannelClient closed")
    }
}
