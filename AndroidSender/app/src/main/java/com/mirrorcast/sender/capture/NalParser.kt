package com.mirrorcast.sender.capture

import java.nio.ByteBuffer

/**
 * Utility to parse Annex-B byte streams from Android MediaCodec into
 * length-prefixed NAL units adhering to Section 3.2 of the MirrorCast protocol.
 *
 * Each NAL unit is framed as:
 * [4-byte Big-Endian Length] + [Raw NAL Unit (starting at NAL header byte)]
 */
object NalParser {

    /**
     * Splits an Annex-B formatted buffer into distinct raw NAL units
     * (stripping the 3-byte or 4-byte start codes).
     */
    fun extractRawNalUnits(buffer: ByteBuffer, offset: Int, size: Int): List<ByteArray> {
        val nalUnits = mutableListOf<ByteArray>()
        if (size <= 4) return nalUnits

        val bytes = ByteArray(size)
        val origPos = buffer.position()
        buffer.position(offset)
        buffer.get(bytes, 0, size)
        buffer.position(origPos)

        val startPositions = mutableListOf<Int>()
        val startCodeLengths = mutableListOf<Int>()

        var i = 0
        while (i <= size - 3) {
            if (bytes[i] == 0.toByte() && bytes[i + 1] == 0.toByte()) {
                if (bytes[i + 2] == 1.toByte()) {
                    // Check if 4-byte start code (0x00 00 00 01)
                    if (i > 0 && bytes[i - 1] == 0.toByte()) {
                        startPositions.add(i - 1)
                        startCodeLengths.add(4)
                    } else {
                        // 3-byte start code (0x00 00 01)
                        startPositions.add(i)
                        startCodeLengths.add(3)
                    }
                    i += 3
                    continue
                } else if (i <= size - 4 && bytes[i + 2] == 0.toByte() && bytes[i + 3] == 1.toByte()) {
                    startPositions.add(i)
                    startCodeLengths.add(4)
                    i += 4
                    continue
                }
            }
            i++
        }

        for (idx in 0 until startPositions.size) {
            val startCodePos = startPositions[idx]
            val codeLen = startCodeLengths[idx]
            val nalStart = startCodePos + codeLen
            val nalEnd = if (idx + 1 < startPositions.size) startPositions[idx + 1] else size

            val nalLen = nalEnd - nalStart
            if (nalLen > 0) {
                val nal = ByteArray(nalLen)
                System.arraycopy(bytes, nalStart, nal, 0, nalLen)
                nalUnits.add(nal)
            }
        }

        return nalUnits
    }

    /**
     * Converts a raw NAL unit into a framed packet prepended with a 4-byte Big-Endian length header.
     */
    fun createLengthPrefixedPacket(nalUnit: ByteArray): ByteArray {
        val length = nalUnit.size
        val packet = ByteArray(4 + length)
        // 4-byte big-endian length prefix
        packet[0] = ((length ushr 24) and 0xFF).toByte()
        packet[1] = ((length ushr 16) and 0xFF).toByte()
        packet[2] = ((length ushr 8) and 0xFF).toByte()
        packet[3] = (length and 0xFF).toByte()

        System.arraycopy(nalUnit, 0, packet, 4, length)
        return packet
    }

    /**
     * Returns true if the NAL unit is an IDR slice (type 5).
     */
    fun isIdrSlice(nalUnit: ByteArray): Boolean {
        if (nalUnit.isEmpty()) return false
        val nalType = nalUnit[0].toInt() and 0x1F
        return nalType == 5
    }

    /**
     * Returns true if the NAL unit is an SPS (type 7).
     */
    fun isSps(nalUnit: ByteArray): Boolean {
        if (nalUnit.isEmpty()) return false
        val nalType = nalUnit[0].toInt() and 0x1F
        return nalType == 7
    }

    /**
     * Returns true if the NAL unit is a PPS (type 8).
     */
    fun isPps(nalUnit: ByteArray): Boolean {
        if (nalUnit.isEmpty()) return false
        val nalType = nalUnit[0].toInt() and 0x1F
        return nalType == 8
    }
}
