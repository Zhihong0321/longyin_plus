@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "WAIT_PID=%~1"
set "SOURCE=%~2"
set "TARGET=%~3"
set "APP_EXE=%~4"
set "LOG_PATH=%~5"

if "%WAIT_PID%"=="" exit /b 1
if "%SOURCE%"=="" exit /b 1
if "%TARGET%"=="" exit /b 1
if "%APP_EXE%"=="" exit /b 1
if "%LOG_PATH%"=="" exit /b 1

for %%I in ("%LOG_PATH%") do set "LOG_DIR=%%~dpI"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>nul

call :log OTA helper started. waitPid=%WAIT_PID% source=%SOURCE% target=%TARGET% exe=%APP_EXE%

if not exist "%SOURCE%" (
  call :log ERROR: Stage root does not exist.
  exit /b 1
)

if not exist "%TARGET%" (
  call :log ERROR: Target root does not exist.
  exit /b 1
)

:wait_for_pid
tasklist /FI "PID eq %WAIT_PID%" /FO CSV /NH 2>nul | find "%WAIT_PID%" >nul
if not errorlevel 1 (
  call :log Waiting for app pid %WAIT_PID% to exit...
  timeout /t 1 /nobreak >nul
  goto wait_for_pid
)

timeout /t 2 /nobreak >nul
set /a COPY_ATTEMPT=0

:copy_loop
set /a COPY_ATTEMPT+=1
call :log Starting robocopy attempt !COPY_ATTEMPT!...
robocopy "%SOURCE%" "%TARGET%" /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP >nul
set "ROBOCOPY_EXIT=!ERRORLEVEL!"
call :log Robocopy exit code: !ROBOCOPY_EXIT!

if !ROBOCOPY_EXIT! LEQ 7 goto copy_success

tasklist /FI "IMAGENAME eq %APP_EXE%" /FO CSV /NH 2>nul | find /I "%APP_EXE%" >nul
if not errorlevel 1 (
  call :log App process still running. Waiting before retry...
  timeout /t 2 /nobreak >nul
  if !COPY_ATTEMPT! LSS 45 goto copy_loop
)

call :log ERROR: Robocopy failed and retries are exhausted.
exit /b 1

:copy_success
if not exist "%TARGET%\%APP_EXE%" (
  call :log ERROR: Updated executable not found after copy.
  exit /b 1
)

call :log Cleaning staged update folder...
rmdir /s /q "%SOURCE%" >nul 2>nul

call :log Relaunching updated app...
start "" "%TARGET%\%APP_EXE%"
call :log OTA helper completed successfully.
exit /b 0

:log
>> "%LOG_PATH%" echo [%date% %time%] %*
exit /b 0
