package com.mirrorcast.sender.model

enum class StreamQuality(
    val label: String,
    val maxDimension: Int?,
    val defaultBitrateBps: Int
) {
    HD_720P("720p", 720, 4_000_000),
    FHD_1080P("1080p", 1080, 8_000_000),
    NATIVE("Native", null, 12_000_000);

    /**
     * Compute actual encoder width and height preserving aspect ratio,
     * ensuring dimensions are even integers (multiples of 2 or 16 for H.264).
     */
    fun computeDimensions(displayWidth: Int, displayHeight: Int): Pair<Int, Int> {
        if (maxDimension == null) {
            val w = if (displayWidth % 2 != 0) displayWidth - 1 else displayWidth
            val h = if (displayHeight % 2 != 0) displayHeight - 1 else displayHeight
            return Pair(w, h)
        }

        val minSide = minOf(displayWidth, displayHeight)
        val maxSide = maxOf(displayWidth, displayHeight)
        val isPortrait = displayHeight >= displayWidth

        val targetMinSide = minOf(maxDimension, minSide)
        val scale = targetMinSide.toFloat() / minSide.toFloat()
        val targetMaxSide = (maxSide * scale).toInt()

        var outW = if (isPortrait) targetMinSide else targetMaxSide
        var outH = if (isPortrait) targetMaxSide else targetMinSide

        // Make even numbers
        if (outW % 2 != 0) outW -= 1
        if (outH % 2 != 0) outH -= 1

        return Pair(outW, outH)
    }
}
