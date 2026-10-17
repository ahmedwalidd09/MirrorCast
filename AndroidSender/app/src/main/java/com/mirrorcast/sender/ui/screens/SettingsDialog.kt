package com.mirrorcast.sender.ui.screens

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.window.Dialog
import com.mirrorcast.sender.model.StreamQuality
import com.mirrorcast.sender.ui.components.MirrorCastButton
import com.mirrorcast.sender.ui.components.QualityToggle
import com.mirrorcast.sender.ui.theme.ColorBgElevated
import com.mirrorcast.sender.ui.theme.ColorBgSurface
import com.mirrorcast.sender.ui.theme.ColorBorderSubtle
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverDim
import com.mirrorcast.sender.ui.theme.ColorSilverMid

@Composable
fun SettingsDialog(
    currentQuality: StreamQuality,
    onQualitySelected: (StreamQuality) -> Unit,
    onDismiss: () -> Unit
) {
    val shape = RoundedCornerShape(16.dp)

    Dialog(onDismissRequest = onDismiss) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .clip(shape)
                .background(ColorBgSurface)
                .border(1.dp, ColorBorderSubtle, shape)
                .padding(20.dp)
        ) {
            Text(
                text = "MirrorCast Settings",
                color = ColorSilverBright,
                fontSize = 17.sp,
                fontWeight = FontWeight.SemiBold
            )

            Spacer(modifier = Modifier.height(16.dp))

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
                onQualitySelected = onQualitySelected
            )

            Spacer(modifier = Modifier.height(16.dp))

            Text(
                text = "PORTS & NETWORKING",
                color = ColorSilverDim,
                fontSize = 11.sp,
                fontFamily = FontFamily.Monospace,
                fontWeight = FontWeight.Bold,
                letterSpacing = 0.8.sp
            )
            Spacer(modifier = Modifier.height(8.dp))
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(8.dp))
                    .background(ColorBgElevated)
                    .padding(horizontal = 12.dp, vertical = 10.dp),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Stream / Control Port",
                    color = ColorSilverMid,
                    fontSize = 13.sp
                )
                Text(
                    text = "59100 / 59101",
                    color = ColorSilverBright,
                    fontSize = 13.sp,
                    fontFamily = FontFamily.Monospace
                )
            }

            Spacer(modifier = Modifier.height(20.dp))

            MirrorCastButton(
                text = "Done",
                onClick = onDismiss,
                modifier = Modifier.height(44.dp)
            )
        }
    }
}
