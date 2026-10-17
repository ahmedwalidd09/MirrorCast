package com.mirrorcast.sender.ui.components

import androidx.compose.animation.core.FastOutSlowInEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverMid

/**
 * Custom Canvas pulsing dot indicator for discovery and searching states.
 */
@Composable
fun PulsingDot(
    modifier: Modifier = Modifier,
    size: Dp = 16.dp,
    color: Color = ColorSilverBright
) {
    val infiniteTransition = rememberInfiniteTransition(label = "pulse_trans")

    val pulseRadiusFraction by infiniteTransition.animateFloat(
        initialValue = 0.4f,
        targetValue = 1.0f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1400, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "pulse_radius"
    )

    val pulseAlpha by infiniteTransition.animateFloat(
        initialValue = 0.7f,
        targetValue = 0.0f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1400, easing = FastOutSlowInEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "pulse_alpha"
    )

    Canvas(modifier = modifier.size(size)) {
        val center = this.center
        val maxRadius = size.toPx() / 2f
        val coreRadius = maxRadius * 0.4f

        // Outer expanding pulsing ring
        drawCircle(
            color = color.copy(alpha = pulseAlpha),
            radius = maxRadius * pulseRadiusFraction,
            center = center
        )

        // Solid inner core dot
        drawCircle(
            color = color,
            radius = coreRadius,
            center = center
        )
    }
}
