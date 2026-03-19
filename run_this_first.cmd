@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_this_first.ps1"
if errorlevel 1 pause
