package com.mirrorcast.sender

import android.app.Application
import timber.log.Timber

class MirrorCastApp : Application() {
    override fun onCreate() {
        super.onCreate()
        Timber.plant(Timber.DebugTree())
        Timber.i("MirrorCast Application initialized")
    }
}
