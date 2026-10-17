package com.mirrorcast.sender.capture

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Binder
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import com.mirrorcast.sender.MainActivity
import com.mirrorcast.sender.R
import com.mirrorcast.sender.model.ConnectionMode
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.model.StreamQuality
import com.mirrorcast.sender.model.StreamState
import com.mirrorcast.sender.model.StreamStats
import com.mirrorcast.sender.network.NetworkUtils
import com.mirrorcast.sender.network.StreamingClient
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import timber.log.Timber

/**
 * Foreground Service responsible for MediaProjection screen capture,
 * H.264 hardware encoding, and streaming to the remote MirrorCast receiver.
 */
class ScreenCaptureService : Service() {

    inner class LocalBinder : Binder() {
        val service: ScreenCaptureService get() = this@ScreenCaptureService
    }

    private val binder = LocalBinder()
    private val serviceScope = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    private var mediaProjectionManager: MediaProjectionManager? = null
    private var mediaProjection: MediaProjection? = null
    private var virtualDisplay: VirtualDisplay? = null
    private var mediaEncoder: MediaEncoder? = null
    private var streamingClient: StreamingClient? = null
    private var rotationWatcher: RotationWatcher? = null
    private var networkUtils: NetworkUtils? = null

    private var currentReceiver: DiscoveredReceiver? = null
    private var currentQuality: StreamQuality = StreamQuality.FHD_1080P
    private var resultCode: Int = 0
    private var resultData: Intent? = null
    private var isPaused = false

    private var screenDensityDpi = 320
    private var physicalWidth = 1080
    private var physicalHeight = 2400

    private val _streamState = MutableStateFlow<StreamState>(StreamState.Idle)
    val streamState: StateFlow<StreamState> = _streamState.asStateFlow()

    private val _streamStats = MutableStateFlow(StreamStats())
    val streamStats: StateFlow<StreamStats> = _streamStats.asStateFlow()

    private var statsCollectorJob: Job? = null

    companion object {
        const val CHANNEL_ID = "mirrorcast_stream_channel"
        const val NOTIFICATION_ID = 1001

        const val ACTION_START_STREAM = "com.mirrorcast.sender.ACTION_START"
        const val ACTION_STOP_STREAM = "com.mirrorcast.sender.ACTION_STOP"
        const val ACTION_PAUSE_STREAM = "com.mirrorcast.sender.ACTION_PAUSE"
        const val ACTION_RESUME_STREAM = "com.mirrorcast.sender.ACTION_RESUME"

        const val EXTRA_RESULT_CODE = "extra_result_code"
        const val EXTRA_RESULT_DATA = "extra_result_data"
        const val EXTRA_HOST = "extra_host"
        const val EXTRA_PORT = "extra_port"
        const val EXTRA_NAME = "extra_name"
        const val EXTRA_MODE = "extra_mode"
        const val EXTRA_QUALITY = "extra_quality"

        // Global shared state for UI to observe easily
        val serviceState = MutableStateFlow<StreamState>(StreamState.Idle)
        val serviceStats = MutableStateFlow(StreamStats())
    }

    override fun onCreate() {
        super.onCreate()
        Timber.i("ScreenCaptureService onCreate")
        mediaProjectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as? MediaProjectionManager
        networkUtils = NetworkUtils(this)
        createNotificationChannel()

        val metrics = resources.displayMetrics
        screenDensityDpi = metrics.densityDpi
        physicalWidth = metrics.widthPixels
        physicalHeight = metrics.heightPixels
    }

    override fun onBind(intent: Intent?): IBinder = binder

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val action = intent?.action ?: return START_NOT_STICKY
        Timber.i("ScreenCaptureService onStartCommand action: %s", action)

        when (action) {
            ACTION_START_STREAM -> {
                resultCode = intent.getIntExtra(EXTRA_RESULT_CODE, 0)
                resultData = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    intent.getParcelableExtra(EXTRA_RESULT_DATA, Intent::class.java)
                } else {
                    @Suppress("DEPRECATION")
                    intent.getParcelableExtra(EXTRA_RESULT_DATA)
                }

                val host = intent.getStringExtra(EXTRA_HOST) ?: "127.0.0.1"
                val port = intent.getIntExtra(EXTRA_PORT, 59100)
                val name = intent.getStringExtra(EXTRA_NAME) ?: "MirrorCast Receiver"
                val modeStr = intent.getStringExtra(EXTRA_MODE) ?: "wifi"
                val qualityStr = intent.getStringExtra(EXTRA_QUALITY) ?: StreamQuality.FHD_1080P.name
                currentQuality = try { StreamQuality.valueOf(qualityStr) } catch (e: Exception) { StreamQuality.FHD_1080P }

                val mode = if (modeStr == "wifi-direct") ConnectionMode.WIFI_DIRECT else ConnectionMode.WIFI
                val receiver = DiscoveredReceiver(
                    id = "active_receiver",
                    name = name,
                    hostAddress = host,
                    port = port,
                    mode = mode
                )
                currentReceiver = receiver

                startForegroundServiceNotification(name)
                startStreamingPipeline(receiver)
            }
            ACTION_STOP_STREAM -> {
                stopStreaming()
                stopSelf()
            }
            ACTION_PAUSE_STREAM -> {
                pauseStream()
            }
            ACTION_RESUME_STREAM -> {
                resumeStream()
            }
        }

        return START_NOT_STICKY
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                getString(R.string.channel_name),
                NotificationManager.IMPORTANCE_LOW
            ).apply {
                description = getString(R.string.channel_description)
                setShowBadge(false)
            }
            val manager = getSystemService(NotificationManager::class.java)
            manager?.createNotificationChannel(channel)
        }
    }

    private fun buildNotification(receiverName: String, isPaused: Boolean): Notification {
        val mainIntent = Intent(this, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP
        }
        val mainPendingIntent = PendingIntent.getActivity(
            this, 0, mainIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val stopIntent = Intent(this, ScreenCaptureService::class.java).apply {
            action = ACTION_STOP_STREAM
        }
        val stopPendingIntent = PendingIntent.getService(
            this, 1, stopIntent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )

        val pauseResumeAction = if (isPaused) {
            val resumeIntent = Intent(this, ScreenCaptureService::class.java).apply {
                action = ACTION_RESUME_STREAM
            }
            val resumePendingIntent = PendingIntent.getService(
                this, 2, resumeIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            NotificationCompat.Action.Builder(0, getString(R.string.notification_action_resume), resumePendingIntent).build()
        } else {
            val pauseIntent = Intent(this, ScreenCaptureService::class.java).apply {
                action = ACTION_PAUSE_STREAM
            }
            val pausePendingIntent = PendingIntent.getService(
                this, 3, pauseIntent,
                PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
            )
            NotificationCompat.Action.Builder(0, getString(R.string.notification_action_pause), pausePendingIntent).build()
        }

        val statusText = if (isPaused) "Mirroring paused" else getString(R.string.notification_text, receiverName)

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle(getString(R.string.notification_title))
            .setContentText(statusText)
            .setSmallIcon(R.drawable.ic_cast)
            .setContentIntent(mainPendingIntent)
            .setOngoing(true)
            .addAction(pauseResumeAction)
            .addAction(0, getString(R.string.notification_action_stop), stopPendingIntent)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()
    }

    private fun startForegroundServiceNotification(receiverName: String) {
        val notification = buildNotification(receiverName, isPaused)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }
    }

    private fun updateNotification() {
        val receiver = currentReceiver ?: return
        val manager = getSystemService(NotificationManager::class.java)
        manager?.notify(NOTIFICATION_ID, buildNotification(receiver.name, isPaused))
    }

    private fun startStreamingPipeline(receiver: DiscoveredReceiver) {
        val data = resultData ?: run {
            Timber.e("Missing MediaProjection resultData")
            return
        }

        serviceScope.launch(Dispatchers.Main) {
            networkUtils?.acquireLocks()
            updateState(StreamState.Connecting(receiver.name))

            val (encWidth, encHeight) = currentQuality.computeDimensions(physicalWidth, physicalHeight)

            // Setup Streaming Client
            val client = StreamingClient(
                host = receiver.hostAddress,
                streamPort = receiver.port,
                mode = receiver.mode.wireValue,
                width = encWidth,
                height = encHeight,
                targetFps = 60,
                onConnectionLost = { canRetry ->
                    Timber.w("Streaming client connection lost (canRetry=$canRetry)")
                    if (!canRetry) {
                        updateState(StreamState.Error("Connection to receiver lost", canRetry = true))
                        stopStreaming()
                    }
                },
                onKeyframeRequested = {
                    mediaEncoder?.requestKeyframe()
                }
            )
            streamingClient = client

            val connected = client.connect(serviceScope)
            if (!connected) {
                updateState(StreamState.Error("Failed to connect to ${receiver.name}"))
                stopStreaming()
                return@launch
            }

            // Start metrics forwarding
            statsCollectorJob?.cancel()
            statsCollectorJob = serviceScope.launch {
                client.streamStats.collect { stats ->
                    _streamStats.value = stats
                    serviceStats.value = stats
                }
            }

            // Obtain MediaProjection
            val projection = mediaProjectionManager?.getMediaProjection(resultCode, data) ?: run {
                Timber.e("Failed to obtain MediaProjection")
                updateState(StreamState.Error("Screen capture permission denied"))
                stopStreaming()
                return@launch
            }
            mediaProjection = projection

            projection.registerCallback(object : MediaProjection.Callback() {
                override fun onStop() {
                    Timber.w("MediaProjection stopped by system")
                    stopStreaming()
                }
            }, null)

            // Setup MediaEncoder
            setupEncoderAndDisplay(encWidth, encHeight)

            // Setup Rotation Watcher
            rotationWatcher?.stop()
            rotationWatcher = RotationWatcher(this@ScreenCaptureService) { newWidth, newHeight, _ ->
                handleScreenRotation(newWidth, newHeight)
            }
            rotationWatcher?.start()

            updateState(StreamState.Streaming(receiver))
            Timber.i("MirrorCast screen streaming pipeline active")
        }
    }

    private fun setupEncoderAndDisplay(width: Int, height: Int) {
        mediaEncoder?.stop()
        virtualDisplay?.release()

        val encoder = MediaEncoder(
            width = width,
            height = height,
            bitrateBps = currentQuality.defaultBitrateBps,
            fps = 60
        ) { framedPacket, _ ->
            serviceScope.launch(Dispatchers.IO) {
                streamingClient?.sendFramedPacket(framedPacket)
            }
        }
        encoder.start(serviceScope)
        mediaEncoder = encoder

        val surface = encoder.surface ?: throw IllegalStateException("Encoder surface null")

        virtualDisplay = mediaProjection?.createVirtualDisplay(
            "MirrorCastVirtualDisplay",
            width,
            height,
            screenDensityDpi,
            DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
            surface,
            null,
            null
        )
        Timber.i("Created VirtualDisplay %dx%d @ %d dpi", width, height, screenDensityDpi)
    }

    private fun handleScreenRotation(newPhysicalW: Int, newPhysicalH: Int) {
        physicalWidth = newPhysicalW
        physicalHeight = newPhysicalH

        val (encWidth, encHeight) = currentQuality.computeDimensions(newPhysicalW, newPhysicalH)
        Timber.i("Reconfiguring encoder for orientation change: %dx%d", encWidth, encHeight)

        serviceScope.launch(Dispatchers.IO) {
            streamingClient?.sendConfigUpdate(encWidth, encHeight, currentQuality.defaultBitrateBps)
        }

        setupEncoderAndDisplay(encWidth, encHeight)
        mediaEncoder?.requestKeyframe()
    }

    fun setQuality(quality: StreamQuality) {
        if (currentQuality == quality) return
        currentQuality = quality
        Timber.i("Changing stream quality to: %s", quality.label)

        val (encWidth, encHeight) = quality.computeDimensions(physicalWidth, physicalHeight)
        serviceScope.launch(Dispatchers.IO) {
            streamingClient?.sendConfigUpdate(encWidth, encHeight, quality.defaultBitrateBps)
        }
        setupEncoderAndDisplay(encWidth, encHeight)
        mediaEncoder?.requestKeyframe()
    }

    fun pauseStream() {
        if (isPaused) return
        isPaused = true
        mediaEncoder?.pause()
        streamingClient?.pause()
        val receiver = currentReceiver
        if (receiver != null) {
            updateState(StreamState.Paused(receiver))
        }
        updateNotification()
    }

    fun resumeStream() {
        if (!isPaused) return
        isPaused = false
        mediaEncoder?.resume()
        streamingClient?.resume()
        val receiver = currentReceiver
        if (receiver != null) {
            updateState(StreamState.Streaming(receiver))
        }
        updateNotification()
    }

    fun stopStreaming() {
        Timber.i("Stopping screen streaming pipeline")
        rotationWatcher?.stop()
        rotationWatcher = null

        statsCollectorJob?.cancel()
        statsCollectorJob = null

        try {
            virtualDisplay?.release()
        } catch (ignored: Exception) {}
        virtualDisplay = null

        mediaEncoder?.stop()
        mediaEncoder = null

        try {
            mediaProjection?.stop()
        } catch (ignored: Exception) {}
        mediaProjection = null

        streamingClient?.disconnect()
        streamingClient = null

        networkUtils?.releaseLocks()
        updateState(StreamState.Idle)

        stopForeground(STOP_FOREGROUND_REMOVE)
    }

    private fun updateState(newState: StreamState) {
        _streamState.value = newState
        serviceState.value = newState
    }

    override fun onDestroy() {
        super.onDestroy()
        stopStreaming()
        serviceScope.cancel()
        Timber.i("ScreenCaptureService onDestroy")
    }
}
