@echo off
setlocal EnableDelayedExpansion
:: ==============================================================================
:: RetroBatGameModeWidget -- Package Installer (EN/FR bilingual)
:: EN: Installs the UWP Game Bar widget MSIX for the current user.
::     NO administrator required. If Developer Mode is off, the script will
::     pause and wait for the user to enable it manually (Windows Settings >
::     For developers > Developer Mode), polling until it is enabled.
:: FR: Installe le widget UWP Game Bar MSIX pour l'utilisateur courant.
::     AUCUN administrateur requis. Si le Mode Developpeur est desactive, le
::     script se met en pause et attend que l'utilisateur l'active manuellement
::     (Parametres Windows > Pour les developpeurs > Mode developpeur), en
::     verifiant en continu jusqu'a ce qu'il soit active.
:: ==============================================================================
title RetroBatGameModeWidget Installer
color 0A

:: Detect UI language.
:: EN/FR: We use a PowerShell one-liner to read the user UI culture, which is
::        reliable and doesn't depend on the deprecated wmic.exe.
set FRENCH=0
for /f "delims=" %%a in ('PowerShell -NoProfile -Command "if ([System.Globalization.CultureInfo]::CurrentUICulture.Name -like 'fr*') { '1' } else { '0' }"') do set FRENCH=%%a

if "%FRENCH%"=="1" (
    set MSG_DEV_CHECK=[INFO] Verification du Mode Developpeur...
    set MSG_DEV_ON=[INFO] Mode Developpeur deja active.
    set MSG_DEV_OFF=[WARNING] Mode Developpeur NON active.
    set MSG_DEV_HINT=[INFO] Ouvrez : Parametres Windows ^> Systeme ^> Pour les developpeurs
    set MSG_DEV_HINT2=[INFO] Activez l'option "Mode Developpeur" puis fermez les Parametres.
    set MSG_DEV_HINT3=[INFO] (Les Parametres Windows viennent de s'ouvrir automatiquement.)
    set MSG_DEV_PROGRESS=[INFO] Toujours en attente... (%d% secondes ecoulees)
    set MSG_DEV_WAIT=[INFO] En attente de l'activation du Mode Developpeur... (verifie toutes les 5 secondes)
    set MSG_DEV_DETECTED=[SUCCESS] Mode Developpeur detecte. Reprise de l'installation.
    set MSG_DEV_CANCEL=[INFO] Arret de l'attente. Le widget ne peut pas etre installe sans le Mode Developpeur.
    set MSG_RUNNING=[EXEC] Installation du package MSIX en cours...
    set MSG_OK=[SUCCESS] Widget RetroBat Game Mode installe avec succes !
    set MSG_OK_HINT=             Ouvrez Win+G et ajoutez le widget depuis la galerie.
    set MSG_OK_DEV_DISABLE=[TIP] Vous pouvez maintenant DESACTIVER le Mode Developpeur dans :
    set MSG_OK_DEV_DISABLE_2=             Parametres ^> Systeme ^> Pour les developpeurs.
    set MSG_OK_DEV_DISABLE_3=             Il n'est necessaire que pour l'installation / mise a jour du widget.
    set MSG_FAIL=[ERROR] L'installation a echoue :
    set MSG_NOMSIX_FR=[ERROR] Aucun fichier .msix trouve dans le meme dossier.
    set MSG_SAME_DIR=             Le .msix doit etre a cote du script Install_Widget_Package.bat
) else (
    set MSG_DEV_CHECK=[INFO] Checking Developer Mode...
    set MSG_DEV_ON=[INFO] Developer Mode is already enabled.
    set MSG_DEV_OFF=[WARNING] Developer Mode is NOT enabled.
    set MSG_DEV_HINT=[INFO] Open: Windows Settings ^> System ^> For developers
    set MSG_DEV_HINT2=[INFO] Turn on the "Developer Mode" toggle, then close Settings.
    set MSG_DEV_HINT3=[INFO] (Windows Settings has just been opened automatically.)
    set MSG_DEV_PROGRESS=[INFO] Still waiting... (%d% seconds elapsed)
    set MSG_DEV_WAIT=[INFO] Waiting for Developer Mode to be enabled... (checking every 5 seconds)
    set MSG_DEV_DETECTED=[SUCCESS] Developer Mode detected. Resuming install.
    set MSG_DEV_CANCEL=[INFO] Wait aborted. The widget cannot be installed without Developer Mode.
    set MSG_RUNNING=[EXEC] Installing the MSIX package...
    set MSG_OK=[SUCCESS] RetroBat Game Mode widget installed successfully!
    set MSG_OK_HINT=             Open Win+G and add the widget from the gallery.
    set MSG_OK_DEV_DISABLE=[TIP] You can now TURN OFF Developer Mode in:
    set MSG_OK_DEV_DISABLE_2=             Settings ^> System ^> For developers.
    set MSG_OK_DEV_DISABLE_3=             It is only needed for installing / updating the widget.
    set MSG_FAIL=[ERROR] Installation failed:
    set MSG_NOMSIX_FR=[ERROR] No .msix file found in the same folder.
    set MSG_SAME_DIR=             The .msix must sit next to Install_Widget_Package.bat.
)

set NO_PAUSE=0
if /i "%~1"=="--no-pause" set NO_PAUSE=1

:: ------------------------------------------------------------------
:: Developer Mode check (HKLM registry value AllowDevelopmentWithoutDevLicense = 1).
:: EN/FR: We NEVER auto-enable it (that needs Admin). We instead wait and
::        poll the registry every 5 seconds until the user enables it, then
::        we continue. The user is invited via dialog.
:: ------------------------------------------------------------------
echo ===================================================
echo !MSG_DEV_CHECK!
echo ===================================================
:devmode_loop
reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" /v AllowDevelopmentWithoutDevLicense 2>nul | findstr "0x1" >nul 2>&1
if %errorlevel% equ 0 (
    echo !MSG_DEV_ON!
    goto devmode_done
)

echo !MSG_DEV_OFF!
echo !MSG_DEV_HINT!
echo !MSG_DEV_HINT2!
echo !MSG_DEV_HINT3!
echo !MSG_DEV_WAIT!
echo.

:: Open the Settings page automatically (no Admin) so the user only needs to click the toggle.
start "" "ms-settings:developers"

:: Poll every 5 seconds. Use simple time-based loop.
set ELAPSED=0
:devmode_wait
timeout /t 5 /nobreak >nul
set /a ELAPSED=ELAPSED+5
set MSG_TMP=!MSG_DEV_PROGRESS!
:: Replace %d% with elapsed seconds (workaround for delayed-expansion %%).
call set MSG_TMP=%%MSG_DEV_PROGRESS:%d%=!ELAPSED!%%
echo !MSG_TMP!

reg query "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" /v AllowDevelopmentWithoutDevLicense 2>nul | findstr "0x1" >nul 2>&1
if %errorlevel% equ 0 (
    echo.
    echo !MSG_DEV_DETECTED!
    goto devmode_done
)

:: Give the user a chance to abort the wait if launched interactively (no --no-pause).
if "%NO_PAUSE%"=="0" (
    choice /c YN /n /m "[Y] Continue waiting / [N] Abort"
    if errorlevel 2 (
        echo !MSG_DEV_CANCEL!
        if "%NO_PAUSE%"=="1" exit /b 2
        pause
        exit /b 2
    )
)
goto devmode_wait

:devmode_done
echo ===================================================
echo.

:: ------------------------------------------------------------------
:: EN/FR: Call the PowerShell installer script from the same folder.
::        The .ps1 finds a .msix in its own directory (no recursion).
:: ------------------------------------------------------------------
echo !MSG_RUNNING!
PowerShell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install_Widget_Package.ps1"
set RC=%errorlevel%

echo.
if %RC% equ 0 (
    echo !MSG_OK!
    echo !MSG_OK_HINT!
    echo.
    echo ===============================
    echo !MSG_OK_DEV_DISABLE!
    echo !MSG_OK_DEV_DISABLE_2!
    echo !MSG_OK_DEV_DISABLE_3!
    echo ===============================
) else (
    echo !MSG_FAIL!
)

echo.
if "%NO_PAUSE%"=="1" exit /b %RC%
pause
exit /b %RC%
