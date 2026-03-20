@echo off
setlocal

set "ROOT=%~dp0"
if not exist "%ROOT%LongYinLiZhiZhuan.exe" (
    echo Game executable not found:
    echo "%ROOT%LongYinLiZhiZhuan.exe"
    pause
    exit /b 1
)

if not exist "%ROOT%steam_appid.txt" (
    >"%ROOT%steam_appid.txt" echo 3202030
)

start "" /D "%ROOT%" "%ROOT%LongYinLiZhiZhuan.exe"
