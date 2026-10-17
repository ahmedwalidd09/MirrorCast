package com.mirrorcast.sender.network

import android.content.Context
import android.net.wifi.WifiManager
import android.os.PowerManager
import timber.log.Timber
import java.net.Inet4Address
import java.net.NetworkInterface

/**
 * Handles Wi-Fi locks, Multicast locks, and network address utilities
 * to guarantee uninterrupted low-latency streaming without OS battery throttling.
 */
class NetworkUtils(private val context: Context) {

    private val wifiManager = context.applicationContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
    private val powerManager = context.applicationContext.getSystemService(Context.POWER_SERVICE) as? PowerManager

    private var wifiLock: WifiManager.WifiLock? = null
    private var multicastLock: WifiManager.MulticastLock? = null
    private var wakeLock: PowerManager.WakeLock? = null

    fun acquireLocks() {
        try {
            // High perf wifi lock
            if (wifiLock == null) {
                @Suppress("DEPRECATION")
                wifiLock = wifiManager?.createWifiLock(
                    WifiManager.WIFI_MODE_FULL_HIGH_PERF,
                    "MirrorCast:StreamingWifiLock"
                )
            }
            wifiLock?.acquire()

            // Multicast lock for mDNS / NSD
            if (multicastLock == null) {
                multicastLock = wifiManager?.createMulticastLock("MirrorCast:NsdMulticastLock")
                multicastLock?.setReferenceCounted(true)
            }
            multicastLock?.acquire()

            // Partial wake lock to keep CPU active during screen casting
            if (wakeLock == null) {
                wakeLock = powerManager?.newWakeLock(
                    PowerManager.PARTIAL_WAKE_LOCK,
                    "MirrorCast:StreamingWakeLock"
                )
            }
            wakeLock?.acquire(3 * 60 * 60 * 1000L) // 3 hours max safe timeout

            Timber.i("Acquired Wi-Fi, Multicast, and Wake locks for streaming")
        } catch (e: Exception) {
            Timber.w(e, "Error acquiring network/wake locks")
        }
    }

    fun releaseLocks() {
        try {
            if (wifiLock?.isHeld == true) {
                wifiLock?.release()
            }
            if (multicastLock?.isHeld == true) {
                multicastLock?.release()
            }
            if (wakeLock?.isHeld == true) {
                wakeLock?.release()
            }
            Timber.i("Released Wi-Fi, Multicast, and Wake locks")
        } catch (e: Exception) {
            Timber.w(e, "Error releasing locks")
        }
    }

    companion object {
        /**
         * Resolves the primary local IPv4 address of the active Wi-Fi or P2P interface.
         */
        fun getLocalIpAddress(): String? {
            try {
                val interfaces = NetworkInterface.getNetworkInterfaces()
                for (intf in interfaces) {
                    val addrs = intf.inetAddresses
                    for (addr in addrs) {
                        if (!addr.isLoopbackAddress && addr is Inet4Address) {
                            return addr.hostAddress
                        }
                    }
                }
            } catch (e: Exception) {
                Timber.w(e, "Error finding local IP address")
            }
            return null
        }
    }
}
