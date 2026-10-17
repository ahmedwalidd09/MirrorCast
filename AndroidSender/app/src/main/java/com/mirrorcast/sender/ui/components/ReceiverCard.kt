package com.mirrorcast.sender.ui.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mirrorcast.sender.R
import com.mirrorcast.sender.model.ConnectionMode
import com.mirrorcast.sender.model.DiscoveredReceiver
import com.mirrorcast.sender.ui.theme.ColorBgElevated
import com.mirrorcast.sender.ui.theme.ColorBgHover
import com.mirrorcast.sender.ui.theme.ColorBgSurface
import com.mirrorcast.sender.ui.theme.ColorBorderSubtle
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverDim
import com.mirrorcast.sender.ui.theme.ColorSilverMid
import com.mirrorcast.sender.ui.theme.ColorStreamActive

/**
 * Custom receiver card item with press state scale and color animation.
 * Strictly adheres to Section 6: No Material ripple.
 */
@Composable
fun ReceiverCard(
    receiver: DiscoveredReceiver,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    val interactionSource = remember { MutableInteractionSource() }
    val isPressed by interactionSource.collectIsPressedAsState()

    val scale by animateFloatAsState(
        targetValue = if (isPressed) 0.98f else 1.0f,
        animationSpec = spring(stiffness = 600f, dampingRatio = 0.75f),
        label = "receiver_card_scale"
    )

    val backgroundColor by animateColorAsState(
        targetValue = if (isPressed) ColorBgHover else ColorBgSurface,
        label = "receiver_card_bg"
    )

    val shape = RoundedCornerShape(12.dp)

    Box(
        modifier = modifier
            .fillMaxWidth()
            .scale(scale)
            .clip(shape)
            .background(backgroundColor)
            .border(1.dp, ColorBorderSubtle, shape)
            .clickable(
                interactionSource = interactionSource,
                indication = null // No material ripple
            ) {
                onClick()
            }
            .padding(horizontal = 16.dp, vertical = 14.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.weight(1f)
            ) {
                // Device Icon
                Box(
                    modifier = Modifier
                        .size(36.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(ColorBgElevated),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        painter = painterResource(
                            id = if (receiver.mode == ConnectionMode.WIFI_DIRECT) R.drawable.ic_wifi_direct else R.drawable.ic_screen
                        ),
                        contentDescription = null,
                        tint = ColorSilverBright,
                        modifier = Modifier.size(20.dp)
                    )
                }

                Spacer(modifier = Modifier.width(14.dp))

                Column {
                    Text(
                        text = receiver.name,
                        color = ColorSilverBright,
                        fontSize = 15.sp,
                        fontWeight = FontWeight.Medium,
                        letterSpacing = (-0.1).sp
                    )
                    Spacer(modifier = Modifier.height(2.dp))
                    Text(
                        text = "${receiver.hostAddress}:${receiver.port}",
                        color = ColorSilverMid,
                        fontSize = 12.sp,
                        fontFamily = FontFamily.Monospace
                    )
                }
            }

            // Connection indicator / badge
            Row(
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .clip(RoundedCornerShape(6.dp))
                        .background(ColorBgElevated)
                        .padding(horizontal = 8.dp, vertical = 4.dp)
                ) {
                    Text(
                        text = if (receiver.mode == ConnectionMode.WIFI_DIRECT) "DIRECT" else "LAN",
                        color = ColorSilverDim,
                        fontSize = 10.sp,
                        fontFamily = FontFamily.Monospace,
                        fontWeight = FontWeight.Bold,
                        letterSpacing = 0.5.sp
                    )
                }
            }
        }
    }
}
