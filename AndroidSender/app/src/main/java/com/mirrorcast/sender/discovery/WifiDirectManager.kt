package com.mirrorcast.sender.discovery

import android.annotation.SuppressLint
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.net.NetworkInfo
import android.net.wifi.p2p.WifiP2pConfig
import android.net.wifi.p2p.WifiP2pDevice
import android.net.wifi.p2p.WifiP2pDeviceList
import android.net.wifi.p2p.WifiP2pInfo
import android.net.wifi.p2p.WifiP2pManager
import android.os.Build
import android.os.Looper
import com.mirrorcast.sender.model.ConnectionMode
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.model.ProtocolConstants
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import timber.log.Timber

/**
 * Manages Wi-Fi Direct (P2P) peer discovery and group connection
 * to autonomous Windows Group Owner receivers.
 */
class WifiDirectManager(private val context: Context) {

    private val p2pManager: WifiP2pManager? by lazy {
        context.getSystemService(Context.WIFI_P2P_SERVICE) as? WifiP2pManager
    }

    private var channel: WifiP2pManager.Channel? = null
    private var isReceiverRegistered = false

    private val _discoveredPeers = MutableStateFlow<List<DiscoveredReceiver>>(emptyList())
    val discoveredPeers: StateFlow<List<DiscoveredReceiver>> = _discoveredPeers.asStateFlow()

    private val _isWifiP2pEnabled = MutableStateFlow(false)
    val isWifiP2pEnabled: StateFlow<Boolean> = _isWifiP2pEnabled.asStateFlow()

    private var onConnectedCallback: ((groupOwnerAddress: String) -> Unit)? = null
    private var onErrorCallback: ((String) -> Unit)? = null

    private val intentFilter = IntentFilter().apply {
        addAction(WifiP2pManager.WIFI_P2P_STATE_CHANGED_ACTION)
        addAction(WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION)
        addAction(WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION)
        addAction(WifiP2pManager.WIFI_P2P_THIS_DEVICE_CHANGED_ACTION)
    }

    private val peerListListener = WifiP2pManager.PeerListListener { peerList: WifiP2pDeviceList ->
        val peers = peerList.deviceList.map { device ->
            val cleanName = if (device.deviceName.isNullOrBlank()) "Direct-${device.deviceAddress}" else device.deviceName
            DiscoveredReceiver(
                id = "p2p_${device.deviceAddress}",
                name = cleanName,
                // Host address will be group owner address once connected, or temporary device MAC
                hostAddress = device.deviceAddress,
                port = ProtocolConstants.DEFAULT_STREAM_PORT,
                controlPort = ProtocolConstants.DEFAULT_CONTROL_PORT,
                mode = ConnectionMode.WIFI_DIRECT,
                isDirect = true
            )
        }
        _discoveredPeers.value = peers
        Timber.d("Wi-Fi Direct discovered %d peers", peers.size)
    }

    private val connectionInfoListener = WifiP2pManager.ConnectionInfoListener { info: WifiP2pInfo? ->
        if (info == null) return@ConnectionInfoListener
        if (info.groupFormed) {
            val groupOwner = info.groupOwnerAddress?.hostAddress
            Timber.i("Wi-Fi Direct group formed! Group owner IP: %s, isGroupOwner=%b", groupOwner, info.isGroupOwner)
            if (!groupOwner.isNullOrEmpty()) {
                onConnectedCallback?.invoke(groupOwner)
                onConnectedCallback = null
            }
        } else {
            Timber.d("Wi-Fi Direct group not formed or disconnected")
        }
    }

    private val p2pReceiver = object : BroadcastReceiver() {
        @Suppress("DEPRECATION")
        override fun onReceive(ctx: Context?, intent: Intent?) {
            when (intent?.action) {
                WifiP2pManager.WIFI_P2P_STATE_CHANGED_ACTION -> {
                    val state = intent.getIntExtra(WifiP2pManager.EXTRA_WIFI_STATE, -1)
                    val isEnabled = state == WifiP2pManager.WIFI_P2P_STATE_ENABLED
                    _isWifiP2pEnabled.value = isEnabled
                    Timber.d("P2P State changed: enabled=%b", isEnabled)
                }

                WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION -> {
                    requestPeers()
                }

                WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION -> {
                    val networkInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        intent.getParcelableExtra(WifiP2pManager.EXTRA_NETWORK_INFO, NetworkInfo::class.java)
                    } else {
                        intent.getParcelableExtra(WifiP2pManager.EXTRA_NETWORK_INFO)
                    }

                    if (networkInfo?.isConnected == true) {
                        requestConnectionInfo()
                    }
                }

                WifiP2pManager.WIFI_P2P_THIS_DEVICE_CHANGED_ACTION -> {
                    val device = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                        intent.getParcelableExtra(WifiP2pManager.EXTRA_WIFI_P2P_DEVICE, WifiP2pDevice::class.java)
                    } else {
                        intent.getParcelableExtra(WifiP2pManager.EXTRA_WIFI_P2P_DEVICE)
                    }
                    Timber.d("This P2P device: %s, status=%d", device?.deviceName, device?.status ?: -1)
                }
            }
        }
    }

    fun initialize() {
        val manager = p2pManager ?: return
        if (channel == null) {
            channel = manager.initialize(context, Looper.getMainLooper(), null)
        }
        if (!isReceiverRegistered) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                context.registerReceiver(p2pReceiver, intentFilter, Context.RECEIVER_NOT_EXPORTED)
            } else {
                context.registerReceiver(p2pReceiver, intentFilter)
            }
            isReceiverRegistered = true
        }
    }

    @SuppressLint("MissingPermission")
    fun startDiscovery() {
        initialize()
        val manager = p2pManager ?: return
        val ch = channel ?: return

        manager.discoverPeers(ch, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                Timber.i("Wi-Fi Direct discoverPeers initiated successfully")
            }

            override fun onFailure(reasonCode: Int) {
                Timber.w("Wi-Fi Direct discoverPeers failed with reason: %d", reasonCode)
            }
        })
    }

    @SuppressLint("MissingPermission")
    fun requestPeers() {
        val manager = p2pManager ?: return
        val ch = channel ?: return
        manager.requestPeers(ch, peerListListener)
    }

    fun requestConnectionInfo() {
        val manager = p2pManager ?: return
        val ch = channel ?: return
        manager.requestConnectionInfo(ch, connectionInfoListener)
    }

    @SuppressLint("MissingPermission")
    fun connectToPeer(
        deviceAddress: String,
        onConnected: (groupOwnerAddress: String) -> Unit,
        onError: (String) -> Unit
    ) {
        val manager = p2pManager ?: run {
            onError("Wi-Fi Direct not supported on this device")
            return
        }
        val ch = channel ?: run {
            onError("Wi-Fi Direct channel not initialized")
            return
        }

        this.onConnectedCallback = onConnected
        this.onErrorCallback = onError

        val config = WifiP2pConfig().apply {
            this.deviceAddress = deviceAddress
            // Low groupOwnerIntent gives priority to Windows receiver to become Group Owner
            this.groupOwnerIntent = 0
        }

        manager.connect(ch, config, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                Timber.i("Wi-Fi Direct connect initiation succeeded to %s", deviceAddress)
            }

            override fun onFailure(reason: Int) {
                val msg = "Wi-Fi Direct connection failed (code $reason)"
                Timber.e(msg)
                onErrorCallback?.invoke(msg)
                onErrorCallback = null
            }
        })
    }

    fun disconnect() {
        val manager = p2pManager ?: return
        val ch = channel ?: return

        manager.removeGroup(ch, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                Timber.i("Wi-Fi Direct group removed successfully")
            }

            override fun onFailure(reason: Int) {
                Timber.w("Failed to remove Wi-Fi Direct group (code %d)", reason)
            }
        })
    }

    fun cleanup() {
        disconnect()
        if (isReceiverRegistered) {
            try {
                context.unregisterReceiver(p2pReceiver)
            } catch (e: Exception) {
                Timber.w(e, "Error unregistering P2P receiver")
            }
            isReceiverRegistered = false
        }
    }
}
