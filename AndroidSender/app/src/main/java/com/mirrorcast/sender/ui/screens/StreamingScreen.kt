package com.mirrorcast.sender.ui.screens

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.model.StreamQuality
import com.mirrorcast.sender.model.StreamState
import com.mirrorcast.sender.ui.components.MirrorCastButton
import com.mirrorcast.sender.ui.components.QualityToggle
import com.mirrorcast.sender.ui.components.StatsRow
import com.mirrorcast.sender.ui.components.TextActionButton
import com.mirrorcast.sender.ui.theme.ColorBgBase
import com.mirrorcast.sender.ui.theme.ColorBgElevated
import com.mirrorcast.sender.ui.theme.ColorBgSurface
import com.mirrorcast.sender.ui.theme.ColorBorderSubtle
import com.mirrorcast.sender.ui.theme.ColorError
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverDim
import com.mirrorcast.sender.ui.theme.ColorSilverMid
import com.mirrorcast.sender.ui.theme.ColorStreamActive
import com.mirrorcast.sender.ui.viewmodel.MainViewModel

/**
 * MirrorCast Active Streaming Screen adhering strictly to Section 6 UI/UX specifications.
 */
@Composable
fun StreamingScreen(
    viewModel: MainViewModel,
    receiver: DiscoveredReceiver,
    isPaused: Boolean,
    modifier: Modifier = Modifier
) {
    val context = LocalContext.current
    val stats by viewModel.streamStats.collectAsState()
    val currentQuality by viewModel.currentQuality.collectAsState()

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(ColorBgBase)
            .statusBarsPadding()
            .padding(horizontal = 24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.SpaceBetween
    ) {
        // Top section: Status Header & Metrics
        Column(
            modifier = Modifier.fillMaxWidth(),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Spacer(modifier = Modifier.height(28.dp))

            // Status header: "Mirroring to [PC-Name]" with green live dot #30D158
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.Center,
                modifier = Modifier.fillMaxWidth()
            ) {
                // Live dot (green #30D158 when active, orange/dim when paused)
                Canvas(modifier = Modifier.size(10.dp)) {
                    drawCircle(
                        color = if (isPaused) ColorSilverMid else ColorStreamActive,
                        radius = size.minDimension / 2f
                    )
                }

                Spacer(modifier = Modifier.width(10.dp))

                Text(
                    text = if (isPaused) "Paused · ${receiver.name}" else "Mirroring to ${receiver.name}",
                    color = ColorSilverBright,
                    fontSize = 18.sp,
                    fontWeight = FontWeight.SemiBold,
                    letterSpacing = (-0.2).sp
                )
            }

            Spacer(modifier = Modifier.height(8.dp))

            // Stats row: resolution · FPS · bitrate (monospaced caps #8E8E93)
            StatsRow(stats = stats)

            Spacer(modifier = Modifier.height(32.dp))

            // Visual representation card / active stream card
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(180.dp)
                    .clip(RoundedCornerShape(16.dp))
                    .background(ColorBgSurface)
                    .border(1.dp, ColorBorderSubtle, RoundedCornerShape(16.dp))
                    .padding(20.dp),
                contentAlignment = Alignment.Center
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center
                ) {
                    Box(
                        modifier = Modifier
                            .size(56.dp)
                            .clip(CircleShape)
                            .background(ColorBgElevated),
                        contentAlignment = Alignment.Center
                    ) {
                        Canvas(modifier = Modifier.size(14.dp)) {
                            drawCircle(
                                color = if (isPaused) ColorSilverDim else ColorStreamActive,
                                radius = size.minDimension / 2f
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(14.dp))

                    Text(
                        text = if (isPaused) "SCREEN STREAM PAUSED" else "STREAM LIVE",
                        color = if (isPaused) ColorSilverDim else ColorStreamActive,
                        fontSize = 12.sp,
                        fontFamily = FontFamily.Monospace,
                        fontWeight = FontWeight.Bold,
                        letterSpacing = 1.0.sp
                    )

                    Spacer(modifier = Modifier.height(4.dp))

                    Text(
                        text = "RTT: ${stats.roundTripTimeMs} ms · Packets: ${stats.packetsSent}",
                        color = ColorSilverMid,
                        fontSize = 12.sp,
                        fontFamily = FontFamily.Monospace
                    )
                }
            }

            Spacer(modifier = Modifier.height(28.dp))

            // Quality toggle: Segmented pill for 720p / 1080p / Native
            Column(
                modifier = Modifier.fillMaxWidth()
            ) {
                Text(
                    text = "STREAM QUALITY",
                    color = ColorSilverDim,
                    fontSize = 11.sp,
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.Bold,
                    letterSpacing = 0.8.sp
                )
                Spacer(modifier = Modifier.height(8.dp))
                QualityToggle(
                    currentQuality = currentQuality,
                    onQualitySelected = { q ->
                        viewModel.setQuality(q, context)
                    }
                )
            }
        }

        // Bottom section: Primary action buttons
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 32.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // Primary button: "Pause" / "Resume" (#161616 bg, subtle border, scale 0.97 on press)
            MirrorCastButton(
                text = if (isPaused) "Resume" else "Pause",
                onClick = {
                    if (isPaused) {
                        viewModel.resumeStream(context)
                    } else {
                        viewModel.pauseStream(context)
                    }
                },
                modifier = Modifier.fillMaxWidth()
            )

            Spacer(modifier = Modifier.height(14.dp))

            // Stop button: Text-only "Stop Mirroring" in #FF453A
            TextActionButton(
                text = "Stop Mirroring",
                onClick = {
                    viewModel.stopStream(context)
                },
                textColor = ColorError
            )
        }
    }
}
