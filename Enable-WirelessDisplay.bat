@echo off
title Enable Native Samsung Smart View and Miracast
echo ================================================================
echo   Enabling Native Wireless Display for Samsung Smart View
echo ================================================================
echo.
echo Downloading and installing Windows Wireless Display feature...
echo Please wait 30-60 seconds...
echo.

DISM /Online /Add-Capability /CapabilityName:App.WirelessDisplay.Connect~~~~0.0.1.0

echo.
echo Starting the Wireless Display receiver app...
start shell:AppsFolder\Windows.MiracastView_cw5n1h2txyewy!App
echo.
echo [SUCCESS] Wireless Display is now installed and active!
echo You can now tap Smart View on your Samsung phone to mirror.
echo.
pause
