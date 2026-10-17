package com.mirrorcast.sender.model

data class DiscoveredReceiver(
    val id: String,
    val name: String,
    val hostAddress: String,
    val port: Int = 59100,
    val controlPort: Int = 59101,
    val mode: ConnectionMode = ConnectionMode.WIFI,
    val isDirect: Boolean = false,
    val targetFps: Int = 60,
    val lastSeenTimestamp: Long = System.currentTimeMillis()
)
