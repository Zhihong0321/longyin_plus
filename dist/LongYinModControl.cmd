@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0LongYinModControl.ps1" %*
if errorlevel 1 pause
