package com.mirrorcast.sender.capture

import android.content.Context
import android.hardware.display.DisplayManager
import android.view.Display
import android.view.WindowManager
import timber.log.Timber

/**
 * Listens for screen orientation and resolution changes, allowing
 * seamless dynamic re-encoding without disconnecting the streaming socket.
 */
class RotationWatcher(
    private val context: Context,
    private val onDisplayChanged: (width: Int, height: Int, rotationDegrees: Int) -> Unit
) {

    private val displayManager = context.getSystemService(Context.DISPLAY_SERVICE) as? DisplayManager
    private val windowManager = context.getSystemService(Context.WINDOW_SERVICE) as? WindowManager

    private var lastWidth = -1
    private var lastHeight = -1
    private var lastRotation = -1
    private var isListening = false

    private val displayListener = object : DisplayManager.DisplayListener {
        override fun onDisplayAdded(displayId: Int) {}
        override fun onDisplayRemoved(displayId: Int) {}
        override fun onDisplayChanged(displayId: Int) {
            if (displayId == Display.DEFAULT_DISPLAY) {
                checkDisplayMetrics()
            }
        }
    }

    fun start() {
        if (isListening) return
        displayManager?.registerDisplayListener(displayListener, null)
        isListening = true
        checkDisplayMetrics()
    }

    fun stop() {
        if (!isListening) return
        displayManager?.unregisterDisplayListener(displayListener)
        isListening = false
    }

    private fun checkDisplayMetrics() {
        val windowManager = this.windowManager ?: return
        val currentDisplay = windowManager.defaultDisplay ?: return

        val point = android.graphics.Point()
        @Suppress("DEPRECATION")
        currentDisplay.getRealSize(point)

        val rotation = currentDisplay.rotation
        val rotationDegrees = when (rotation) {
            android.view.Surface.ROTATION_0 -> 0
            android.view.Surface.ROTATION_90 -> 90
            android.view.Surface.ROTATION_180 -> 180
            android.view.Surface.ROTATION_270 -> 270
            else -> 0
        }

        if (point.x != lastWidth || point.y != lastHeight || rotation != lastRotation) {
            Timber.i("Display rotated / resized: %dx%d (rotation: %d deg)", point.x, point.y, rotationDegrees)
            lastWidth = point.x
            lastHeight = point.y
            lastRotation = rotation
            onDisplayChanged(point.x, point.y, rotationDegrees)
        }
    }
}
