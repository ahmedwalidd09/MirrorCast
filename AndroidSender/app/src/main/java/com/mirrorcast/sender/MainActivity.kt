package com.mirrorcast.sender

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.Bundle
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.animation.AnimatedContent
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.togetherWith
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.material3.Text
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.mirrorcast.sender.model.StreamState
import com.mirrorcast.sender.ui.components.PulsingDot
import com.mirrorcast.sender.ui.screens.HomeScreen
import com.mirrorcast.sender.ui.screens.StreamingScreen
import com.mirrorcast.sender.ui.theme.ColorBgBase
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverMid
import com.mirrorcast.sender.ui.theme.MirrorCastTheme
import com.mirrorcast.sender.ui.viewmodel.MainViewModel
import timber.log.Timber

class MainActivity : ComponentActivity() {

    private val viewModel: MainViewModel by viewModels()

    private val mediaProjectionLauncher = registerForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            Timber.i("MediaProjection permission granted by user")
            viewModel.startServiceStreaming(
                context = this,
                resultCode = result.resultCode,
                resultData = result.data!!
            )
        } else {
            Timber.w("MediaProjection permission denied by user (code: %d)", result.resultCode)
            Toast.makeText(this, "Screen capture permission required to mirror", Toast.LENGTH_SHORT).show()
        }
    }

    private val permissionsLauncher = registerForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { permissions ->
        val allGranted = permissions.entries.all { it.value }
        Timber.i("Runtime permissions result: %s (allGranted=%b)", permissions, allGranted)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        requestNecessaryPermissions()

        setContent {
            MirrorCastTheme {
                val state by viewModel.streamState.collectAsState()

                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(ColorBgBase)
                ) {
                    AnimatedContent(
                        targetState = state,
                        transitionSpec = { fadeIn() togetherWith fadeOut() },
                        label = "screen_transition"
                    ) { targetState ->
                        when (targetState) {
                            is StreamState.Streaming -> {
                                StreamingScreen(
                                    viewModel = viewModel,
                                    receiver = targetState.receiver,
                                    isPaused = false
                                )
                            }
                            is StreamState.Paused -> {
                                StreamingScreen(
                                    viewModel = viewModel,
                                    receiver = targetState.receiver,
                                    isPaused = true
                                )
                            }
                            is StreamState.Connecting -> {
                                Box(
                                    modifier = Modifier.fillMaxSize(),
                                    contentAlignment = Alignment.Center
                                ) {
                                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                                        PulsingDot(size = 32.dp, color = ColorSilverBright)
                                        Spacer(modifier = Modifier.height(20.dp))
                                        Text(
                                            text = "Connecting to ${targetState.receiverName}...",
                                            color = ColorSilverMid,
                                            fontSize = 15.sp
                                        )
                                    }
                                }
                            }
                            is StreamState.Error -> {
                                Toast.makeText(this@MainActivity, targetState.message, Toast.LENGTH_LONG).show()
                                HomeScreen(
                                    viewModel = viewModel,
                                    onRequestMediaProjection = { requestMediaProjection() }
                                )
                            }
                            else -> {
                                HomeScreen(
                                    viewModel = viewModel,
                                    onRequestMediaProjection = { requestMediaProjection() }
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    private fun requestNecessaryPermissions() {
        val permissionsToRequest = mutableListOf<String>()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                permissionsToRequest.add(Manifest.permission.POST_NOTIFICATIONS)
            }
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.NEARBY_WIFI_DEVICES) != PackageManager.PERMISSION_GRANTED) {
                permissionsToRequest.add(Manifest.permission.NEARBY_WIFI_DEVICES)
            }
        }
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            permissionsToRequest.add(Manifest.permission.ACCESS_FINE_LOCATION)
        }

        if (permissionsToRequest.isNotEmpty()) {
            permissionsLauncher.launch(permissionsToRequest.toTypedArray())
        }
    }

    private fun requestMediaProjection() {
        val projectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as? MediaProjectionManager
        if (projectionManager != null) {
            val captureIntent = projectionManager.createScreenCaptureIntent()
            mediaProjectionLauncher.launch(captureIntent)
        } else {
            Toast.makeText(this, "MediaProjection service not available", Toast.LENGTH_SHORT).show()
        }
    }
}
