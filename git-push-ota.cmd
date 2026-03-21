@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0git-push-ota.ps1" %*
exit /b %errorlevel%
