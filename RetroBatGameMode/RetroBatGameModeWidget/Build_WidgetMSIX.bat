@echo off
setlocal enabledelayedexpansion
:: ==============================================================================
:: RetroBatGameModeWidget — Build MSIX
:: EN: Builds the UWP widget MSIX package using MSBuild (VS 2022 or VS 2026).
:: FR: Compile le package MSIX du widget UWP via MSBuild (VS 2022 ou VS 2026).
:: No need to use the VS "Create App Packages" wizard — this does it directly.
:: ==============================================================================
title Build RetroBatGameModeWidget MSIX
color 0B

:: ------------------------------------------------------------------
:: 1. Auto-detect MSBuild from VS 2026 Insiders or VS 2022 Community
:: EN: Search for MSBuild in known VS installation paths.
:: FR: Recherche MSBuild dans les chemins d'installation VS connus.
:: ------------------------------------------------------------------
set "MSBUILD="

if exist "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe"
    echo [INFO] MSBuild trouve : VS 2026 Insiders
    goto :foundMSBuild
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
    echo [INFO] MSBuild trouve : VS 2022 Community
    goto :foundMSBuild
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"
    echo [INFO] MSBuild trouve : VS 2022 Professional
    goto :foundMSBuild
)
if exist "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe" (
    set "MSBUILD=C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe"
    echo [INFO] MSBuild trouve : VS 2022 Enterprise
    goto :foundMSBuild
)

echo [ERROR] MSBuild.exe introuvable. Installez VS 2022 ou VS 2026.
pause
exit /b 1

:foundMSBuild
echo ===================================================
echo [INFO] Compilation du widget MSIX (Release x64)...
echo ===================================================
echo.

:: ------------------------------------------------------------------
:: 2. Restore NuGet packages then build
:: EN: Restore dependencies first, then compile the MSIX package.
:: FR: Restaurer les dépendances NuGet puis compiler le package MSIX.
:: ------------------------------------------------------------------
"%MSBUILD%" "%~dp0RetroBatGameModeWidget\RetroBatGameModeWidget.csproj" ^
    /t:Restore ^
    /p:Configuration=Release ^
    /p:Platform=x64 ^
    /nologo

if %errorlevel% neq 0 (
    echo [ERROR] La restauration NuGet a echoue.
    pause
    exit /b 1
)

"%MSBUILD%" "%~dp0RetroBatGameModeWidget\RetroBatGameModeWidget.csproj" ^
    /t:Build ^
    /p:Configuration=Release ^
    /p:Platform=x64 ^
    /p:GenerateAppxPackageOnBuild=true ^
    /nologo

if %errorlevel% neq 0 (
    echo.
    echo [ERROR] La compilation a echoue. Voir les erreurs MSBuild ci-dessus.
    pause
    exit /b 1
)

:: ------------------------------------------------------------------
:: 3. Report generated MSIX location
:: EN: Find and display the path of the generated .msix file.
:: FR: Trouver et afficher le chemin du fichier .msix généré.
:: ------------------------------------------------------------------
echo.
echo ===================================================
set "MSIX_FOUND="
for /r "%~dp0RetroBatGameModeWidget\AppPackages" %%i in (*.msix) do (
    set "MSIX_FOUND=%%i"
)

if defined MSIX_FOUND (
    echo [SUCCESS] MSIX genere avec succes !
    echo Fichier : !MSIX_FOUND!
    echo.
    echo Lancez maintenant : Install_Widget_Package.bat
) else (
    echo [SUCCESS] Compilation OK — aucun .msix trouve dans AppPackages.
    echo Verifiez le dossier RetroBatGameModeWidget\AppPackages\
)
echo ===================================================

echo.
pause
