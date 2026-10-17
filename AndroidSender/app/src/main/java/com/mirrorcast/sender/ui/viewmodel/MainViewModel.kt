package com.mirrorcast.sender.ui.viewmodel

import android.app.Application
import android.content.Context
import android.content.Intent
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.mirrorcast.sender.capture.ScreenCaptureService
import com.mirrorcast.sender.discovery.NsdDiscoveryManager
import com.mirrorcast.sender.discovery.WifiDirectManager
import com.mirrorcast.sender.model.ConnectionMode
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.model.ProtocolConstants
import com.mirrorcast.sender.model.StreamQuality
import com.mirrorcast.sender.model.StreamState
import com.mirrorcast.sender.model.StreamStats
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import timber.log.Timber

class MainViewModel(application: Application) : AndroidViewModel(application) {

    private val nsdDiscovery = NsdDiscoveryManager(application)
    private val wifiDirect = WifiDirectManager(application)

    private val _connectionMode = MutableStateFlow(ConnectionMode.WIFI)
    val connectionMode: StateFlow<ConnectionMode> = _connectionMode.asStateFlow()

    private val _currentQuality = MutableStateFlow(StreamQuality.FHD_1080P)
    val currentQuality: StateFlow<StreamQuality> = _currentQuality.asStateFlow()

    private val _manualIpInput = MutableStateFlow("")
    val manualIpInput: StateFlow<String> = _manualIpInput.asStateFlow()

    private val _showSettings = MutableStateFlow(false)
    val showSettings: StateFlow<Boolean> = _showSettings.asStateFlow()

    private val _isSearching = MutableStateFlow(true)
    val isSearching: StateFlow<Boolean> = _isSearching.asStateFlow()

    // Global stream state from foreground service
    val streamState: StateFlow<StreamState> = ScreenCaptureService.serviceState
    val streamStats: StateFlow<StreamStats> = ScreenCaptureService.serviceStats

    // Discovered receivers combined based on active mode
    val discoveredReceivers: StateFlow<List<DiscoveredReceiver>> = combine(
        _connectionMode,
        nsdDiscovery.discoveredReceivers,
        wifiDirect.discoveredPeers
    ) { mode, nsdList, p2pList ->
        when (mode) {
            ConnectionMode.WIFI -> nsdList
            ConnectionMode.WIFI_DIRECT -> p2pList
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), emptyList())

    // Pending receiver selected for streaming, waiting for MediaProjection permission
    var pendingReceiver: DiscoveredReceiver? = null
        private set

    init {
        startDiscovery()
    }

    fun setConnectionMode(mode: ConnectionMode) {
        if (_connectionMode.value == mode) return
        _connectionMode.value = mode
        Timber.i("Switched connection mode to: %s", mode.label)
        startDiscovery()
    }

    fun setManualIp(ip: String) {
        _manualIpInput.value = ip
    }

    fun setQuality(quality: StreamQuality, context: Context) {
        _currentQuality.value = quality
        // If already streaming, inform the service directly
        val serviceIntent = Intent(context, ScreenCaptureService::class.java).apply {
            putExtra(ScreenCaptureService.EXTRA_QUALITY, quality.name)
        }
        // Will be picked up or applied dynamically
    }

    fun setShowSettings(show: Boolean) {
        _showSettings.value = show
    }

    fun startDiscovery() {
        _isSearching.value = true
        when (_connectionMode.value) {
            ConnectionMode.WIFI -> {
                wifiDirect.cleanup()
                nsdDiscovery.startDiscovery()
            }
            ConnectionMode.WIFI_DIRECT -> {
                nsdDiscovery.stopDiscovery()
                wifiDirect.startDiscovery()
            }
        }
    }

    fun stopDiscovery() {
        _isSearching.value = false
        nsdDiscovery.stopDiscovery()
        wifiDirect.cleanup()
    }

    fun selectReceiverForStreaming(
        receiver: DiscoveredReceiver,
        onMediaProjectionPermissionNeeded: () -> Unit
    ) {
        if (receiver.mode == ConnectionMode.WIFI_DIRECT) {
            // Initiate P2P connection to obtain group owner IP
            wifiDirect.connectToPeer(
                deviceAddress = receiver.hostAddress,
                onConnected = { groupOwnerIp ->
                    val resolved = receiver.copy(hostAddress = groupOwnerIp)
                    pendingReceiver = resolved
                    onMediaProjectionPermissionNeeded()
                },
                onError = { error ->
                    Timber.e("Wi-Fi Direct connect error: %s", error)
                }
            )
        } else {
            pendingReceiver = receiver
            onMediaProjectionPermissionNeeded()
        }
    }

    fun connectManualIp(
        onMediaProjectionPermissionNeeded: () -> Unit
    ) {
        val input = _manualIpInput.value.trim()
        if (input.isBlank()) return

        val parts = input.split(":")
        val host = parts[0]
        val port = if (parts.size > 1) parts[1].toIntOrNull() ?: ProtocolConstants.DEFAULT_STREAM_PORT else ProtocolConstants.DEFAULT_STREAM_PORT

        val receiver = DiscoveredReceiver(
            id = "manual_$input",
            name = "Receiver ($host)",
            hostAddress = host,
            port = port,
            mode = _connectionMode.value
        )
        pendingReceiver = receiver
        onMediaProjectionPermissionNeeded()
    }

    fun startServiceStreaming(
        context: Context,
        resultCode: Int,
        resultData: Intent
    ) {
        val receiver = pendingReceiver ?: run {
            Timber.e("Cannot start streaming: pendingReceiver is null")
            return
        }

        val serviceIntent = Intent(context, ScreenCaptureService::class.java).apply {
            action = ScreenCaptureService.ACTION_START_STREAM
            putExtra(ScreenCaptureService.EXTRA_RESULT_CODE, resultCode)
            putExtra(ScreenCaptureService.EXTRA_RESULT_DATA, resultData)
            putExtra(ScreenCaptureService.EXTRA_HOST, receiver.hostAddress)
            putExtra(ScreenCaptureService.EXTRA_PORT, receiver.port)
            putExtra(ScreenCaptureService.EXTRA_NAME, receiver.name)
            putExtra(ScreenCaptureService.EXTRA_MODE, receiver.mode.wireValue)
            putExtra(ScreenCaptureService.EXTRA_QUALITY, _currentQuality.value.name)
        }

        context.startForegroundService(serviceIntent)
        stopDiscovery()
    }

    fun pauseStream(context: Context) {
        val intent = Intent(context, ScreenCaptureService::class.java).apply {
            action = ScreenCaptureService.ACTION_PAUSE_STREAM
        }
        context.startService(intent)
    }

    fun resumeStream(context: Context) {
        val intent = Intent(context, ScreenCaptureService::class.java).apply {
            action = ScreenCaptureService.ACTION_RESUME_STREAM
        }
        context.startService(intent)
    }

    fun stopStream(context: Context) {
        val intent = Intent(context, ScreenCaptureService::class.java).apply {
            action = ScreenCaptureService.ACTION_STOP_STREAM
        }
        context.startService(intent)
        pendingReceiver = null
        startDiscovery()
    }

    override fun onCleared() {
        super.onCleared()
        stopDiscovery()
    }
}
