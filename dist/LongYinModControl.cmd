@echo off
setlocal

if exist "%~dp0龙胤立志传 Pro Max.exe" (
    start "" "%~dp0龙胤立志传 Pro Max.exe" %*
    exit /b 0
)
if exist "%~dp0electron-app\龙胤立志传 Pro Max.exe" (
    start "" "%~dp0electron-app\龙胤立志传 Pro Max.exe" %*
    exit /b 0
)
if exist "%~dp0LongYinPlus\龙胤立志传 Pro Max.exe" (
    start "" "%~dp0LongYinPlus\龙胤立志传 Pro Max.exe" %*
    exit /b 0
)

powershell -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0LongYinModControl.ps1" %*
if errorlevel 1 pause
