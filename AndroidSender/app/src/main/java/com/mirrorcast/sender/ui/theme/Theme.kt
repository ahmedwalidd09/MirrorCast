package com.mirrorcast.sender.ui.theme

import android.app.Activity
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.SideEffect
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.platform.LocalView
import androidx.core.view.WindowCompat

private val DarkColorScheme = darkColorScheme(
    primary = ColorSilverBright,
    onPrimary = ColorBgBase,
    secondary = ColorSilverMid,
    onSecondary = ColorBgBase,
    background = ColorBgBase,
    onBackground = ColorSilverBright,
    surface = ColorBgSurface,
    onSurface = ColorSilverBright,
    surfaceVariant = ColorBgElevated,
    onSurfaceVariant = ColorSilverMid,
    outline = ColorBorderSubtle,
    error = ColorError,
    onError = ColorBgBase
)

@Composable
fun MirrorCastTheme(
    content: @Composable () -> Unit
) {
    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as? Activity)?.window
            if (window != null) {
                window.statusBarColor = ColorBgBase.toArgb()
                window.navigationBarColor = ColorBgBase.toArgb()
                val controller = WindowCompat.getInsetsController(window, view)
                controller.isAppearanceLightStatusBars = false
                controller.isAppearanceLightNavigationBars = false
            }
        }
    }

    MaterialTheme(
        colorScheme = DarkColorScheme,
        typography = Typography,
        content = content
    )
}
