@echo off
setlocal
set "ROOT=%~dp0"
set "APP=%ROOT%LongYinProMaxApp\LongYinProMax.exe"

if not exist "%APP%" (
  echo [LongYin Pro Max] Electron launcher not found:
  echo %APP%
  pause
  exit /b 1
)

start "" "%APP%"
exit /b 0
