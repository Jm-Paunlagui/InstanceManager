@echo off
:: ============================================================================
::  IMEE Installer — Intelligent Mutex Execution Environment
::  Author : John Moises Paunlagui
::  Version: 1.0.0.17
::  Note   : No admin rights required — installs to user AppData.
:: ============================================================================

setlocal EnableDelayedExpansion

set "APP_NAME=Intelligent Mutex Execution Environment"
set "INSTALL_DIR=%LOCALAPPDATA%\MES\IMEEv1.0.0.17"
set "EXE_NAME=IntelligentMutexExecutionEnvironment.exe"
set "SOURCE_DIR=%~dp0bin\Release"
set "SHORTCUT_NAME=IMEE v1.0.0.17"

echo.
echo  ============================================================
echo   %APP_NAME%
echo   Installer v1.0.0.17
echo   Author: John Moises Paunlagui
echo  ============================================================
echo.

:: --- Verify source files exist ---
if not exist "%SOURCE_DIR%\%EXE_NAME%" (
    echo  [ERROR] Build output not found at:
    echo          %SOURCE_DIR%
    echo.
    echo  Please build the project in Release mode first.
    pause
    exit /b 1
)

:: --- Create install directory ---
echo  [1/4] Creating install directory...
if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
    echo        Created: %INSTALL_DIR%
) else (
    echo        Directory already exists, files will be updated.
)

:: --- Copy application files ---
echo  [2/4] Copying application files...
xcopy "%SOURCE_DIR%\*.exe" "%INSTALL_DIR%\" /Y /Q >nul
xcopy "%SOURCE_DIR%\*.dll" "%INSTALL_DIR%\" /Y /Q >nul 2>&1
xcopy "%SOURCE_DIR%\*.config" "%INSTALL_DIR%\" /Y /Q >nul
xcopy "%SOURCE_DIR%\*.pdf" "%INSTALL_DIR%\" /Y /Q >nul 2>&1

:: --- Create logs directory ---
if not exist "%INSTALL_DIR%\logs" mkdir "%INSTALL_DIR%\logs"
echo        Files copied successfully.

:: --- Create Desktop shortcut ---
echo  [3/4] Creating desktop shortcut...
set "SHORTCUT_PATH=%USERPROFILE%\Desktop\%SHORTCUT_NAME%.lnk"

powershell -NoProfile -Command ^
    "$ws = New-Object -ComObject WScript.Shell; " ^
    "$sc = $ws.CreateShortcut('%SHORTCUT_PATH%'); " ^
    "$sc.TargetPath = '%INSTALL_DIR%\%EXE_NAME%'; " ^
    "$sc.WorkingDirectory = '%INSTALL_DIR%'; " ^
    "$sc.Description = '%APP_NAME%'; " ^
    "$sc.IconLocation = '%INSTALL_DIR%\%EXE_NAME%,0'; " ^
    "$sc.Save()"

if exist "%SHORTCUT_PATH%" (
    echo        Shortcut created on Desktop.
) else (
    echo        [WARN] Could not create desktop shortcut.
)

:: --- Done ---
echo  [4/4] Installation complete!
echo.
echo  ============================================================
echo   Installed to : %INSTALL_DIR%
echo   Shortcut     : %SHORTCUT_PATH%
echo  ============================================================
echo.
pause
