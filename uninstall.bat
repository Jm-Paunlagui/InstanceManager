@echo off
:: ============================================================================
::  IMEE Uninstaller — Intelligent Mutex Execution Environment
::  Author : John Moises Paunlagui
::  Note   : No admin rights required.
:: ============================================================================

setlocal EnableDelayedExpansion

set "APP_NAME=Intelligent Mutex Execution Environment"
set "INSTALL_DIR=%LOCALAPPDATA%\MES\IMEEv1.0.0.17"
set "EXE_NAME=IntelligentMutexExecutionEnvironment.exe"
set "SHORTCUT_NAME=IMEE v1.0.0.17"

echo.
echo  ============================================================
echo   %APP_NAME% — Uninstaller
echo  ============================================================
echo.

:: --- Kill running process ---
echo  [1/3] Stopping application if running...
taskkill /IM "%EXE_NAME%" /F >nul 2>&1
if %errorlevel% equ 0 (
    echo        Application stopped.
) else (
    echo        Application was not running.
)

:: --- Remove desktop shortcut ---
echo  [2/3] Removing desktop shortcut...
set "SHORTCUT_PATH=%USERPROFILE%\Desktop\%SHORTCUT_NAME%.lnk"
if exist "%SHORTCUT_PATH%" (
    del "%SHORTCUT_PATH%"
    echo        Shortcut removed.
) else (
    echo        No shortcut found.
)

:: --- Remove install directory ---
echo  [3/3] Removing installation directory...
if exist "%INSTALL_DIR%" (
    rmdir /S /Q "%INSTALL_DIR%"
    echo        Removed: %INSTALL_DIR%
) else (
    echo        Directory not found.
)

echo.
echo  ============================================================
echo   Uninstallation complete.
echo  ============================================================
echo.
pause
