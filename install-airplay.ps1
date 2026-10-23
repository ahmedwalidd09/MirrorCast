# Downloads and extracts the portable AirPlay receiver engine
$engineDir = Join-Path $PSScriptRoot "AirPlayEngine"
if (Test-Path "$engineDir\uxplay-windows.exe") {
    Write-Host "[OK] AirPlay engine already installed at $engineDir" -ForegroundColor Green
    exit 0
}

Write-Host "[*] Downloading portable AirPlay engine for Windows..." -ForegroundColor Cyan
if (-not (Test-Path $engineDir)) { New-Item -ItemType Directory -Force -Path $engineDir | Out-Null }
$zipUrl = "https://github.com/leapbtw/uxplay-windows/releases/download/2.0.0.1736/uxplay-windows.zip"
$zipFile = Join-Path $engineDir "uxplay-windows.zip"

Invoke-WebRequest -Uri $zipUrl -OutFile $zipFile
Write-Host "[*] Extracting..." -ForegroundColor Cyan
Expand-Archive -Path $zipFile -DestinationPath $engineDir -Force
Remove-Item $zipFile -Force -ErrorAction SilentlyContinue

Write-Host "[SUCCESS] AirPlay engine installed successfully." -ForegroundColor Green
