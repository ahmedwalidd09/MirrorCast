@echo off
cd /d "%~dp0"
echo [*] Checking AirPlay engine...
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-airplay.ps1"
pause
