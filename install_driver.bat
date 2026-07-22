@echo off
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Administrator privileges confirmed.
) else (
    echo This script requires Administrator privileges.
    echo Right-click on this script and select "Run as administrator".
    pause
    exit /b
)

echo Installing Interception driver...
cd /d "%~dp0\driver"
install-interception.exe /install
if not errorlevel 1 goto installation_succeeded
set "INSTALL_EXIT=%errorLevel%"
echo.
echo Interception driver installation failed with exit code %INSTALL_EXIT%.
echo Do not restart yet. Review the error above and run this script again as administrator.
pause
exit /b %INSTALL_EXIT%

:installation_succeeded
echo.
echo Installation complete! 
echo =======================================================
echo YOU MUST RESTART YOUR COMPUTER FOR THE DRIVER TO LOAD.
echo =======================================================
pause
