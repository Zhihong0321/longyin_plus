@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0run_this_first.ps1" %*
if errorlevel 1 pause
