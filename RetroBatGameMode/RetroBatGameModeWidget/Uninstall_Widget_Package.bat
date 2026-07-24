@echo off
setlocal EnableDelayedExpansion
:: ==============================================================================
:: RetroBatGameModeWidget -- Package Uninstaller (EN/FR bilingual)
:: EN: Force-closes the widget process and removes the MSIX package.
::     NO administrator required.
:: FR: Ferme le widget de force et supprime le package MSIX de l'utilisateur.
::     AUCUN administrateur requis.
:: ==============================================================================
title RetroBatGameModeWidget Uninstaller
color 0C

:: Detect UI language.
:: EN/FR: We use a PowerShell one-liner to read the user UI culture, which is
::        reliable and doesn't depend on the deprecated wmic.exe.
set FRENCH=0
for /f "delims=" %%a in ('PowerShell -NoProfile -Command "if ([System.Globalization.CultureInfo]::CurrentUICulture.Name -like 'fr*') { '1' } else { '0' }"') do set FRENCH=%%a

if "%FRENCH%"=="1" (
    set MSG_KILL=[INFO] FERMETURE DU PROCESSUS WIDGET...
    set MSG_KILL_OK=[SUCCESS] Processus widget arrete.
    set MSG_KILL_NO=[INFO] Le widget n'etait pas en cours d'execution.
    set MSG_RM=[INFO] DESINSTALLATION DU PACKAGE UWP...
    set MSG_OK=[SUCCESS] Widget RetroBat Game Mode desinstalle !
    set MSG_OK_HINT=Relancez Install_Widget_Package.bat pour reinstaller.
    set MSG_FAIL=[ERROR] La desinstallation a echoue. Peut-etre deja desinstalle ?
    set MSG_FAIL_HINT=Verifiez la sortie PowerShell ci-dessus.
) else (
    set MSG_KILL=[INFO] CLOSING WIDGET PROCESS...
    set MSG_KILL_OK=[SUCCESS] Widget process stopped.
    set MSG_KILL_NO=[INFO] The widget was not running.
    set MSG_RM=[INFO] UNINSTALLING UWP PACKAGE...
    set MSG_OK=[SUCCESS] RetroBat Game Mode widget uninstalled!
    set MSG_OK_HINT=Run Install_Widget_Package.bat again to reinstall.
    set MSG_FAIL=[ERROR] Uninstall failed. Maybe it was already uninstalled?
    set MSG_FAIL_HINT=Check the PowerShell output above.
)

set NO_PAUSE=0
if /i "%~1"=="--no-pause" set NO_PAUSE=1

echo ===================================================
echo !MSG_KILL!
echo ===================================================
taskkill /F /IM "RetroBatGameModeWidget.exe" /T >nul 2>&1
if %errorlevel% equ 0 (
    echo !MSG_KILL_OK!
) else (
    echo !MSG_KILL_NO!
)

timeout /t 1 /nobreak >nul

echo.
echo ===================================================
echo !MSG_RM!
echo ===================================================
PowerShell -NoProfile -ExecutionPolicy Bypass -Command "Get-AppxPackage -Name *RetroBatGameModeWidget* | Remove-AppxPackage"
set RC=%errorlevel%

if %RC% equ 0 (
    echo.
    echo !MSG_OK!
    echo !MSG_OK_HINT!
) else (
    echo.
    echo !MSG_FAIL!
    echo !MSG_FAIL_HINT!
)

echo.
if "%NO_PAUSE%"=="1" exit /b %RC%
pause
exit /b %RC%
