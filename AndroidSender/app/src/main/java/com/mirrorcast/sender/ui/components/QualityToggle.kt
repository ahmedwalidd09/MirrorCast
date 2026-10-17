package com.mirrorcast.sender.ui.components

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import com.mirrorcast.sender.model.StreamQuality

/**
 * Quality toggle segmented pill for 720p / 1080p / Native presets.
 */
@Composable
fun QualityToggle(
    currentQuality: StreamQuality,
    onQualitySelected: (StreamQuality) -> Unit,
    modifier: Modifier = Modifier
) {
    val options = listOf(StreamQuality.HD_720P, StreamQuality.FHD_1080P, StreamQuality.NATIVE)

    PillSegmentedControl(
        items = options,
        selectedItem = currentQuality,
        onItemSelected = onQualitySelected,
        modifier = modifier.fillMaxWidth(),
        labelProvider = { it.label }
    )
}
