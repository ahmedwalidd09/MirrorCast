package com.mirrorcast.sender.model

sealed interface StreamState {
    data object Idle : StreamState
    data object Discovering : StreamState
    data class Connecting(val receiverName: String) : StreamState
    data class Streaming(val receiver: DiscoveredReceiver) : StreamState
    data class Paused(val receiver: DiscoveredReceiver) : StreamState
    data class Error(val message: String, val canRetry: Boolean = true) : StreamState
}
