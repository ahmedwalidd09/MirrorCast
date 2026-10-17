package com.mirrorcast.sender.ui.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mirrorcast.sender.ui.theme.ColorBgElevated
import com.mirrorcast.sender.ui.theme.ColorBgHover
import com.mirrorcast.sender.ui.theme.ColorBorderSubtle
import com.mirrorcast.sender.ui.theme.ColorError
import com.mirrorcast.sender.ui.theme.ColorSilverBright

/**
 * Primary action button adhering strictly to Section 6:
 * #161616 background, subtle border, scale 0.97 on press, no Material ripple.
 */
@Composable
fun MirrorCastButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    backgroundColor: Color = ColorBgElevated,
    textColor: Color = ColorSilverBright
) {
    val interactionSource = remember { MutableInteractionSource() }
    val isPressed by interactionSource.collectIsPressedAsState()

    val scale by animateFloatAsState(
        targetValue = if (isPressed) 0.97f else 1.0f,
        animationSpec = spring(stiffness = 600f, dampingRatio = 0.75f),
        label = "btn_scale"
    )

    val currentBg by animateColorAsState(
        targetValue = if (isPressed) ColorBgHover else backgroundColor,
        label = "btn_bg"
    )

    val shape = RoundedCornerShape(12.dp)

    Box(
        modifier = modifier
            .fillMaxWidth()
            .height(50.dp)
            .scale(scale)
            .clip(shape)
            .background(currentBg)
            .border(1.dp, ColorBorderSubtle, shape)
            .clickable(
                interactionSource = interactionSource,
                indication = null // No material ripple
            ) {
                onClick()
            },
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = text,
            color = textColor,
            fontSize = 15.sp,
            fontWeight = FontWeight.SemiBold,
            letterSpacing = 0.2.sp
        )
    }
}

/**
 * Text-only Stop Mirroring button in #FF453A.
 */
@Composable
fun TextActionButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    textColor: Color = ColorError
) {
    val interactionSource = remember { MutableInteractionSource() }
    val isPressed by interactionSource.collectIsPressedAsState()

    val scale by animateFloatAsState(
        targetValue = if (isPressed) 0.96f else 1.0f,
        animationSpec = spring(stiffness = 600f, dampingRatio = 0.75f),
        label = "txt_btn_scale"
    )

    Box(
        modifier = modifier
            .scale(scale)
            .clickable(
                interactionSource = interactionSource,
                indication = null
            ) {
                onClick()
            }
            .padding(horizontal = 16.dp, vertical = 10.dp),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = text,
            color = textColor,
            fontSize = 14.sp,
            fontWeight = FontWeight.SemiBold,
            letterSpacing = 0.1.sp
        )
    }
}
