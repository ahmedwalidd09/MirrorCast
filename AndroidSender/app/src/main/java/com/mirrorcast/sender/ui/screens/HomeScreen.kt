package com.mirrorcast.sender.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mirrorcast.sender.R
import com.mirrorcast.sender.model.ConnectionMode
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.ui.components.ManualIpInput
import com.mirrorcast.sender.ui.components.PillSegmentedControl
import com.mirrorcast.sender.ui.components.PulsingDot
import com.mirrorcast.sender.ui.components.ReceiverCard
import com.mirrorcast.sender.ui.theme.ColorBgBase
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverDim
import com.mirrorcast.sender.ui.theme.ColorSilverMid
import com.mirrorcast.sender.ui.viewmodel.MainViewModel

/**
 * MirrorCast Home Screen adhering strictly to Section 6 UI/UX design specifications.
 */
@Composable
fun HomeScreen(
    viewModel: MainViewModel,
    onRequestMediaProjection: () -> Unit,
    modifier: Modifier = Modifier
) {
    val connectionMode by viewModel.connectionMode.collectAsState()
    val receivers by viewModel.discoveredReceivers.collectAsState()
    val isSearching by viewModel.isSearching.collectAsState()
    val manualIpInput by viewModel.manualIpInput.collectAsState()
    val showSettings by viewModel.showSettings.collectAsState()
    val currentQuality by viewModel.currentQuality.collectAsState()

    val connectionModes = listOf(ConnectionMode.WIFI, ConnectionMode.WIFI_DIRECT)

    if (showSettings) {
        SettingsDialog(
            currentQuality = currentQuality,
            onQualitySelected = { q ->
                // Apply quality
            },
            onDismiss = { viewModel.setShowSettings(false) }
        )
    }

    Column(
        modifier = modifier
            .fillMaxSize()
            .background(ColorBgBase)
            .statusBarsPadding()
            .padding(horizontal = 20.dp)
    ) {
        // Strict Section 6 TopAppBar: "MirrorCast" with gear icon, no elevation, #000000 background
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(56.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(
                text = "MirrorCast",
                color = ColorSilverBright,
                fontSize = 20.sp,
                fontWeight = FontWeight.SemiBold,
                letterSpacing = (-0.3).sp
            )

            // Gear Icon Button
            Box(
                modifier = Modifier
                    .size(40.dp)
                    .clickable(
                        interactionSource = remember { MutableInteractionSource() },
                        indication = null
                    ) {
                        viewModel.setShowSettings(true)
                    },
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    painter = painterResource(id = R.drawable.ic_gear),
                    contentDescription = "Settings",
                    tint = ColorSilverBright,
                    modifier = Modifier.size(22.dp)
                )
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        // Connection Mode Segmented Pill Control
        PillSegmentedControl(
            items = connectionModes,
            selectedItem = connectionMode,
            onItemSelected = { mode ->
                viewModel.setConnectionMode(mode)
            },
            labelProvider = { it.label }
        )

        Spacer(modifier = Modifier.height(24.dp))

        // Section header with Custom Canvas pulsing dot searching indicator
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(
                text = "DISCOVERED RECEIVERS",
                color = ColorSilverDim,
                fontSize = 11.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Bold,
                letterSpacing = 0.8.sp
            )

            if (isSearching) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = "SEARCHING",
                        color = ColorSilverMid,
                        fontSize = 10.sp,
                        fontFamily = FontFamily.Monospace,
                        letterSpacing = 0.5.sp
                    )
                    Spacer(modifier = Modifier.width(6.dp))
                    PulsingDot(size = 14.dp, color = ColorSilverBright)
                }
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        // Receivers List
        LazyColumn(
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth(),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            contentPadding = PaddingValues(bottom = 16.dp)
        ) {
            if (receivers.isEmpty()) {
                item {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 40.dp),
                        contentAlignment = Alignment.Center
                    ) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(
                                text = if (connectionMode == ConnectionMode.WIFI)
                                    "No receivers found on local Wi-Fi"
                                else
                                    "No Wi-Fi Direct receivers found",
                                color = ColorSilverMid,
                                fontSize = 14.sp
                            )
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "Make sure the PC receiver is active",
                                color = ColorSilverDim,
                                fontSize = 12.sp
                            )
                        }
                    }
                }
            } else {
                items(receivers, key = { it.id }) { receiver ->
                    ReceiverCard(
                        receiver = receiver,
                        onClick = {
                            viewModel.selectReceiverForStreaming(
                                receiver = receiver,
                                onMediaProjectionPermissionNeeded = onRequestMediaProjection
                            )
                        }
                    )
                }
            }
        }

        // Manual IP Entry Row at bottom
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(bottom = 20.dp)
        ) {
            Text(
                text = "MANUAL CONNECTION",
                color = ColorSilverDim,
                fontSize = 11.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Bold,
                letterSpacing = 0.8.sp
            )
            Spacer(modifier = Modifier.height(8.dp))
            ManualIpInput(
                value = manualIpInput,
                onValueChange = { viewModel.setManualIp(it) },
                onConnect = {
                    viewModel.connectManualIp(
                        onMediaProjectionPermissionNeeded = onRequestMediaProjection
                    )
                }
            )
        }
    }
}
