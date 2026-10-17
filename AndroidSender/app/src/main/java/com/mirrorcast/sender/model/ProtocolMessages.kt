package com.mirrorcast.sender.model

import org.json.JSONObject

object ProtocolConstants {
    const val DEFAULT_STREAM_PORT = 59100
    const val DEFAULT_CONTROL_PORT = 59101
    const val MDNS_SERVICE_TYPE = "_screenmirror._tcp."
    const val MAX_NAL_LENGTH_BYTES = 16_777_216 // 16 MB max frame protection
    const val PROTOCOL_VERSION = 1
}

data class HandshakeRequest(
    val device: String,
    val os: String = "android",
    val width: Int,
    val height: Int,
    val fps: Int = 60,
    val mode: String // "wifi" or "wifi-direct"
) {
    fun toJsonString(): String {
        return JSONObject().apply {
            put("device", device)
            put("os", os)
            put("width", width)
            put("height", height)
            put("fps", fps)
            put("mode", mode)
        }.toString()
    }
}

data class HandshakeResponse(
    val status: String,
    val protocolVersion: Int = 1,
    val server: String = "",
    val controlPort: Int = ProtocolConstants.DEFAULT_CONTROL_PORT,
    val errorMessage: String? = null
) {
    val isOk: Boolean get() = status.equals("ok", ignoreCase = true)

    companion object {
        fun fromJson(jsonStr: String): HandshakeResponse {
            val obj = JSONObject(jsonStr)
            val status = obj.optString("status", "error")
            val protocolVersion = obj.optInt("protocolVersion", 1)
            val server = obj.optString("server", "")
            val controlPort = obj.optInt("controlPort", ProtocolConstants.DEFAULT_CONTROL_PORT)
            val message = if (obj.has("message")) obj.getString("message") else null
            return HandshakeResponse(status, protocolVersion, server, controlPort, message)
        }
    }
}

sealed class ControlMessage {
    abstract val type: String
    abstract fun toJsonString(): String

    data class Ping(val timestamp: Long = System.currentTimeMillis()) : ControlMessage() {
        override val type: String = "ping"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
            put("timestamp", timestamp)
        }.toString()
    }

    data class Pong(val timestamp: Long) : ControlMessage() {
        override val type: String = "pong"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
            put("timestamp", timestamp)
        }.toString()
    }

    data object Pause : ControlMessage() {
        override val type: String = "pause"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
        }.toString()
    }

    data object Resume : ControlMessage() {
        override val type: String = "resume"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
        }.toString()
    }

    data class ConfigUpdate(
        val width: Int,
        val height: Int,
        val bitrate: Int,
        val fps: Int = 60
    ) : ControlMessage() {
        override val type: String = "config_update"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
            put("width", width)
            put("height", height)
            put("bitrate", bitrate)
            put("fps", fps)
        }.toString()
    }

    data class ConfigAck(val status: String) : ControlMessage() {
        override val type: String = "config_ack"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
            put("status", status)
        }.toString()
    }

    data class Disconnect(val reason: String = "user_terminated") : ControlMessage() {
        override val type: String = "disconnect"
        override fun toJsonString(): String = JSONObject().apply {
            put("type", type)
            put("reason", reason)
        }.toString()
    }

    companion object {
        fun parse(line: String): ControlMessage? {
            return try {
                val obj = JSONObject(line.trim())
                when (obj.optString("type")) {
                    "ping" -> Ping(obj.optLong("timestamp", System.currentTimeMillis()))
                    "pong" -> Pong(obj.optLong("timestamp", 0L))
                    "pause" -> Pause
                    "resume" -> Resume
                    "config_update" -> ConfigUpdate(
                        width = obj.optInt("width"),
                        height = obj.optInt("height"),
                        bitrate = obj.optInt("bitrate"),
                        fps = obj.optInt("fps", 60)
                    )
                    "config_ack" -> ConfigAck(obj.optString("status", "accepted"))
                    "disconnect" -> Disconnect(obj.optString("reason", "unknown"))
                    else -> null
                }
            } catch (e: Exception) {
                null
            }
        }
    }
}
