package com.mirrorcast.sender.discovery

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.os.Build
import com.mirrorcast.sender.model.ConnectionMode
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.model.ProtocolConstants
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import timber.log.Timber
import java.net.InetAddress
import java.util.concurrent.ConcurrentHashMap

/**
 * Manages mDNS / DNS-SD discovery of MirrorCast receivers on the local Wi-Fi
 * using Android's native NsdManager.
 */
class NsdDiscoveryManager(private val context: Context) {

    private val nsdManager: NsdManager? by lazy {
        context.getSystemService(Context.NSD_SERVICE) as? NsdManager
    }

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private val resolveMutex = Mutex()

    private val _discoveredReceivers = MutableStateFlow<List<DiscoveredReceiver>>(emptyList())
    val discoveredReceivers: StateFlow<List<DiscoveredReceiver>> = _discoveredReceivers.asStateFlow()

    private val receiversMap = ConcurrentHashMap<String, DiscoveredReceiver>()
    private var isDiscovering = false
    private var discoveryListener: NsdManager.DiscoveryListener? = null

    fun startDiscovery() {
        if (isDiscovering) {
            Timber.d("NSD discovery already running")
            return
        }
        val manager = nsdManager ?: run {
            Timber.e("NsdManager not available on this device")
            return
        }

        receiversMap.clear()
        _discoveredReceivers.value = emptyList()

        discoveryListener = object : NsdManager.DiscoveryListener {
            override fun onDiscoveryStarted(regType: String) {
                Timber.i("NSD discovery started for type: %s", regType)
                isDiscovering = true
            }

            override fun onServiceFound(serviceInfo: NsdServiceInfo) {
                Timber.d("NSD Service found: %s, type: %s", serviceInfo.serviceName, serviceInfo.serviceType)
                // Filter service type
                if (serviceInfo.serviceType.contains("screenmirror")) {
                    resolveService(serviceInfo)
                }
            }

            override fun onServiceLost(serviceInfo: NsdServiceInfo) {
                Timber.d("NSD Service lost: %s", serviceInfo.serviceName)
                receiversMap.remove(serviceInfo.serviceName)
                _discoveredReceivers.value = receiversMap.values.toList()
            }

            override fun onDiscoveryStopped(serviceType: String) {
                Timber.i("NSD discovery stopped: %s", serviceType)
                isDiscovering = false
            }

            override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
                Timber.e("NSD start discovery failed: code %d", errorCode)
                stopDiscovery()
            }

            override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {
                Timber.e("NSD stop discovery failed: code %d", errorCode)
                isDiscovering = false
            }
        }

        try {
            manager.discoverServices(
                ProtocolConstants.MDNS_SERVICE_TYPE,
                NsdManager.PROTOCOL_DNS_SD,
                discoveryListener
            )
        } catch (e: Exception) {
            Timber.e(e, "Error initiating NSD service discovery")
            isDiscovering = false
        }
    }

    private fun resolveService(serviceInfo: NsdServiceInfo) {
        scope.launch {
            resolveMutex.withLock {
                val manager = nsdManager ?: return@withLock
                val resolveListener = object : NsdManager.ResolveListener {
                    override fun onResolveFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
                        Timber.w("NSD resolve failed for %s: code %d", serviceInfo.serviceName, errorCode)
                    }

                    override fun onServiceResolved(resolvedInfo: NsdServiceInfo) {
                        val host = resolvedInfo.host ?: return
                        val hostAddress = host.hostAddress ?: return

                        // Skip loopback / IPv6 non-link-local if needed, focus on IPv4 or reachable address
                        if (host.isLoopbackAddress) return

                        val serviceName = resolvedInfo.serviceName
                        val port = if (resolvedInfo.port > 0) resolvedInfo.port else ProtocolConstants.DEFAULT_STREAM_PORT

                        var receiverName = serviceName
                        var isDirect = false
                        var fps = 60

                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
                            try {
                                val attributes = resolvedInfo.attributes
                                attributes["name"]?.let { receiverName = String(it, Charsets.UTF_8) }
                                attributes["direct"]?.let { isDirect = String(it, Charsets.UTF_8).toBoolean() }
                                attributes["fps"]?.let { fps = String(it, Charsets.UTF_8).toIntOrNull() ?: 60 }
                            } catch (e: Exception) {
                                Timber.w(e, "Failed to read attributes for %s", serviceName)
                            }
                        }

                        val receiver = DiscoveredReceiver(
                            id = "nsd_${serviceName}_$hostAddress",
                            name = receiverName,
                            hostAddress = hostAddress,
                            port = port,
                            controlPort = ProtocolConstants.DEFAULT_CONTROL_PORT,
                            mode = ConnectionMode.WIFI,
                            isDirect = isDirect,
                            targetFps = fps
                        )

                        receiversMap[serviceName] = receiver
                        _discoveredReceivers.value = receiversMap.values.toList()
                        Timber.i("Discovered MirrorCast receiver: %s @ %s:%d", receiver.name, receiver.hostAddress, receiver.port)
                    }
                }

                try {
                    manager.resolveService(serviceInfo, resolveListener)
                } catch (e: Exception) {
                    Timber.w(e, "Exception while attempting to resolve service: %s", serviceInfo.serviceName)
                }
            }
        }
    }

    fun stopDiscovery() {
        if (!isDiscovering) return
        val manager = nsdManager ?: return
        val listener = discoveryListener ?: return

        try {
            manager.stopServiceDiscovery(listener)
        } catch (e: Exception) {
            Timber.w(e, "Error stopping NSD discovery")
        } finally {
            isDiscovering = false
            discoveryListener = null
        }
    }
}
