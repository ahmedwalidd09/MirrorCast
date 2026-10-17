package com.mirrorcast.sender.model

import java.util.Locale

data class StreamStats(
    val width: Int = 0,
    val height: Int = 0,
    val fps: Double = 0.0,
    val bitrateMbps: Double = 0.0,
    val packetsSent: Long = 0,
    val bytesSent: Long = 0,
    val roundTripTimeMs: Long = 0
) {
    /**
     * Formats statistics row in monospaced caps:
     * e.g., "1080×2400 · 60 FPS · 8.2 MBPS"
     */
    fun formattedRow(): String {
        val res = if (width > 0 && height > 0) "${width}×${height}" else "--×--"
        val fpsStr = String.format(Locale.US, "%.0f FPS", fps)
        val mbpsStr = String.format(Locale.US, "%.1f MBPS", bitrateMbps)
        return "$res · $fpsStr · $mbpsStr"
    }
}
