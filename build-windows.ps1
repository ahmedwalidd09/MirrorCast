<#
.SYNOPSIS
    Builds and publishes the MirrorCast Windows Receiver desktop client.
.DESCRIPTION
    Checks for .NET SDK 8+, restores dependencies, builds the WPF application,
    and publishes a self-contained or framework-dependent release executable.
#>

param (
    [switch]$SelfContained = $false,
    [string]$Configuration = "Release",
    [switch]$RunAfterBuild = $false
)

$ErrorActionPreference = "Stop"

$ProjectDir = Join-Path $PSScriptRoot "WindowsReceiver"
$ProjectFile = Join-Path $ProjectDir "WindowsReceiver.csproj"

Write-Host "==================================================" -ForegroundColor Cyan
Write-Host "  MirrorCast Windows Receiver Build Script" -ForegroundColor Cyan
Write-Host "==================================================" -ForegroundColor Cyan

if (-not (Test-Path $ProjectFile)) {
    Write-Error "WindowsReceiver.csproj not found at $ProjectFile"
    exit 1
}

# Locate dotnet executable
$DotnetCmd = "dotnet"
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $DotnetCmd = "dotnet"
} elseif (Test-Path "C:\Program Files\dotnet\dotnet.exe") {
    $DotnetCmd = "C:\Program Files\dotnet\dotnet.exe"
} elseif (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $DotnetCmd = "$env:USERPROFILE\.dotnet\dotnet.exe"
}

Write-Host "[*] Using dotnet: $DotnetCmd" -ForegroundColor Gray

# Check dotnet version
try {
    $version = & $DotnetCmd --version
    Write-Host "[+] Detected .NET SDK version: $version" -ForegroundColor Green
} catch {
    Write-Warning "[!] .NET SDK not detected on PATH. Checking runtimes..."
    & $DotnetCmd --list-runtimes
}

Write-Host "[*] Restoring NuGet packages..." -ForegroundColor Yellow
& $DotnetCmd restore $ProjectFile

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to restore NuGet packages."
    exit $LASTEXITCODE
}

Write-Host "[*] Building WindowsReceiver ($Configuration)..." -ForegroundColor Yellow
if ($SelfContained) {
    & $DotnetCmd publish $ProjectFile -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o "$ProjectDir\bin\$Configuration\publish"
} else {
    & $DotnetCmd build $ProjectFile -c $Configuration
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "[+] Build completed successfully!" -ForegroundColor Green

if ($RunAfterBuild) {
    $exePath = "$ProjectDir\bin\$Configuration\net8.0-windows10.0.22621.0\MirrorCastReceiver.exe"
    if ($SelfContained) {
        $exePath = "$ProjectDir\bin\$Configuration\publish\MirrorCastReceiver.exe"
    }
    if (Test-Path $exePath) {
        Write-Host "[*] Launching $exePath..." -ForegroundColor Cyan
        Start-Process $exePath
    } else {
        Write-Warning "Executable not found at $exePath"
    }
}
