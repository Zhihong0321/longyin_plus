@echo off
call "%~dp0git-push-ota.cmd" %*
exit /b %errorlevel%
