package com.mirrorcast.sender.ui.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.spring
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mirrorcast.sender.ui.theme.ColorBorderSubtle
import com.mirrorcast.sender.ui.theme.ColorPillBackground
import com.mirrorcast.sender.ui.theme.ColorPillThumb
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverMid

/**
 * Custom segmented control pill with animated sliding background indicator.
 * Strictly avoids default Material tabs.
 */
@Composable
fun <T> PillSegmentedControl(
    items: List<T>,
    selectedItem: T,
    onItemSelected: (T) -> Unit,
    modifier: Modifier = Modifier,
    labelProvider: (T) -> String = { it.toString() }
) {
    val selectedIndex = items.indexOf(selectedItem).coerceAtLeast(0)
    val cornerRadius = 20.dp

    BoxWithConstraints(
        modifier = modifier
            .fillMaxWidth()
            .height(40.dp)
            .clip(RoundedCornerShape(cornerRadius))
            .background(ColorPillBackground)
            .padding(3.dp)
    ) {
        val totalWidth = maxWidth
        val itemCount = items.size
        val segmentWidth = if (itemCount > 0) totalWidth / itemCount else 0.dp

        val animatedIndex by animateFloatAsState(
            targetValue = selectedIndex.toFloat(),
            animationSpec = spring(stiffness = 500f, dampingRatio = 0.85f),
            label = "pill_slider"
        )

        // Animated sliding thumb
        Box(
            modifier = Modifier
                .offset {
                    val xPx = (animatedIndex * (segmentWidth.toPx())).toInt()
                    IntOffset(x = xPx, y = 0)
                }
                .width(segmentWidth)
                .fillMaxHeight()
                .clip(RoundedCornerShape(cornerRadius - 2.dp))
                .background(ColorPillThumb)
        )

        // Items row
        Row(
            modifier = Modifier.fillMaxSize(),
            verticalAlignment = Alignment.CenterVertically
        ) {
            items.forEach { item ->
                val isSelected = item == selectedItem
                Box(
                    modifier = Modifier
                        .weight(1f)
                        .fillMaxHeight()
                        .clickable(
                            interactionSource = remember { MutableInteractionSource() },
                            indication = null // Strict Section 6: No Material ripple
                        ) {
                            onItemSelected(item)
                        },
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = labelProvider(item),
                        color = if (isSelected) ColorSilverBright else ColorSilverMid,
                        fontSize = 13.sp,
                        fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal,
                        letterSpacing = 0.2.sp
                    )
                }
            }
        }
    }
}
