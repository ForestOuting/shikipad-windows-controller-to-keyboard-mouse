@echo off
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Administator privileges confirmed.
) else (
    echo This script requires Administrator privileges.
    echo Right-click on this script and select "Run as administrator".
    pause
    exit /b
)

echo Installing Interception driver...
cd /d "%~dp0\driver"
install-interception.exe /install
echo.
echo Installation complete! 
echo =======================================================
echo YOU MUST RESTART YOUR COMPUTER FOR THE DRIVER TO LOAD.
echo =======================================================
pause
