package com.mirrorcast.sender.model

enum class ConnectionMode(val label: String, val wireValue: String) {
    WIFI("Same Wi-Fi", "wifi"),
    WIFI_DIRECT("Wi-Fi Direct", "wifi-direct")
}
