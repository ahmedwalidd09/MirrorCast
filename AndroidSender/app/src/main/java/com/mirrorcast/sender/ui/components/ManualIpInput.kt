package com.mirrorcast.sender.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.mirrorcast.sender.ui.theme.ColorBgElevated
import com.mirrorcast.sender.ui.theme.ColorBgSurface
import com.mirrorcast.sender.ui.theme.ColorBorderSubtle
import com.mirrorcast.sender.ui.theme.ColorSilverBright
import com.mirrorcast.sender.ui.theme.ColorSilverDim
import com.mirrorcast.sender.ui.theme.ColorSilverMid

/**
 * Manual IP:port entry row with clean dark text field and inline Connect button.
 */
@Composable
fun ManualIpInput(
    value: String,
    onValueChange: (String) -> Unit,
    onConnect: () -> Unit,
    modifier: Modifier = Modifier,
    placeholder: String = "192.168.1.100:59100"
) {
    val focusManager = LocalFocusManager.current
    val shape = RoundedCornerShape(10.dp)

    Row(
        modifier = modifier
            .fillMaxWidth()
            .height(48.dp)
            .clip(shape)
            .background(ColorBgSurface)
            .border(1.dp, ColorBorderSubtle, shape)
            .padding(horizontal = 12.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier.weight(1f),
            contentAlignment = Alignment.CenterStart
        ) {
            if (value.isEmpty()) {
                Text(
                    text = placeholder,
                    color = ColorSilverDim,
                    fontSize = 13.sp,
                    fontFamily = FontFamily.Monospace
                )
            }
            BasicTextField(
                value = value,
                onValueChange = onValueChange,
                singleLine = true,
                cursorBrush = SolidColor(ColorSilverBright),
                textStyle = TextStyle(
                    color = ColorSilverBright,
                    fontSize = 13.sp,
                    fontFamily = FontFamily.Monospace,
                    fontWeight = FontWeight.Medium
                ),
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Uri,
                    imeAction = ImeAction.Go
                ),
                keyboardActions = KeyboardActions(
                    onGo = {
                        focusManager.clearFocus()
                        onConnect()
                    }
                ),
                modifier = Modifier.fillMaxWidth()
            )
        }

        Spacer(modifier = Modifier.width(8.dp))

        // Compact Connect Button
        MirrorCastButton(
            text = "Connect",
            onClick = {
                focusManager.clearFocus()
                onConnect()
            },
            modifier = Modifier
                .width(80.dp)
                .height(34.dp),
            backgroundColor = ColorBgElevated
        )
    }
}
