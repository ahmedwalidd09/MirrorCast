@echo off
title Enable Native Samsung Smart View / Miracast
echo ================================================================
echo   Enabling Native Wireless Display for Samsung Smart View & Android
echo ================================================================
echo.
echo Installing the official Windows Wireless Display feature...
echo (Please click YES if Windows asks for administrator permission)
echo.

powershell -NoProfile -Command "Start-Process powershell -Verb RunAs -ArgumentList '-NoProfile -ExecutionPolicy Bypass -Command Write-Host \"Downloading & Installing Windows Wireless Display (Miracast)...\" -ForegroundColor Cyan; Add-WindowsCapability -Online -Name App.WirelessDisplay.Connect~~~~0.0.1.0; Write-Host \"[SUCCESS] Native Wireless Display Installed!\" -ForegroundColor Green; start shell:AppsFolder\Windows.MiracastView_cw5n1h2txyewy!App; pause'"

echo.
echo Setup initiated in elevated window.
pause
