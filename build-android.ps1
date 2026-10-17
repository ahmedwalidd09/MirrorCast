<#
.SYNOPSIS
    Builds the MirrorCast Android Sender APK.
.DESCRIPTION
    Wraps the Gradle build system to compile the Android app in Debug or Release mode.
#>

param (
    [ValidateSet("Debug", "Release")]
    [string]$BuildType = "Debug",
    [switch]$InstallOnDevice = $false
)

$ErrorActionPreference = "Stop"

$AndroidDir = Join-Path $PSScriptRoot "AndroidSender"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  MirrorCast Android Sender Build Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (-not (Test-Path $AndroidDir)) {
    Write-Error "AndroidSender directory not found at $AndroidDir"
    exit 1
}

# Check for gradlew or gradle
$GradleCmd = Join-Path $AndroidDir "gradlew.bat"
if (-not (Test-Path $GradleCmd)) {
    if (Get-Command gradle -ErrorAction SilentlyContinue) {
        $GradleCmd = "gradle"
    } else {
        Write-Warning "[!] Neither gradlew.bat nor gradle found. Please ensure Android Studio or Gradle is installed."
    }
}

# Check Java
if (Get-Command java -ErrorAction SilentlyContinue) {
    $javaVer = & java -version 2>&1 | Select-Object -First 1
    Write-Host "[+] Using Java: $javaVer" -ForegroundColor Green
} else {
    Write-Warning "[!] Java runtime not found on PATH."
}

$task = if ($BuildType -eq "Release") { "assembleRelease" } else { "assembleDebug" }

Write-Host "[*] Executing Gradle task: $task..." -ForegroundColor Yellow
Push-Location $AndroidDir
try {
    if (Test-Path $GradleCmd) {
        & $GradleCmd $task
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[+] Android build successful!" -ForegroundColor Green
            $apkFolder = Join-Path $AndroidDir "app\build\outputs\apk\$($BuildType.ToLower())"
            Write-Host "[+] APKs output to: $apkFolder" -ForegroundColor Cyan

            if ($InstallOnDevice) {
                if (Get-Command adb -ErrorAction SilentlyContinue) {
                    $apk = Get-ChildItem -Path $apkFolder -Filter "*.apk" | Select-Object -First 1
                    if ($apk) {
                        Write-Host "[*] Installing $($apk.FullName) via adb..." -ForegroundColor Cyan
                        & adb install -r $apk.FullName
                    }
                } else {
                    Write-Warning "[!] adb not found on PATH. Cannot install on device."
                }
            }
        } else {
            Write-Error "Gradle build failed with exit code $LASTEXITCODE"
        }
    } else {
        Write-Host "[*] Project is structured and ready for Android Studio." -ForegroundColor Cyan
        Write-Host "    Open '$AndroidDir' in Android Studio to build and run." -ForegroundColor Yellow
    }
} finally {
    Pop-Location
}
