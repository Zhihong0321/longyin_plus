@echo off
setlocal

set "ROOT=%~dp0"
if not exist "%ROOT%LongYinLiZhiZhuan.exe" (
    echo 未找到游戏可执行文件：
    echo "%ROOT%LongYinLiZhiZhuan.exe"
    pause
    exit /b 1
)

if not exist "%ROOT%steam_appid.txt" (
    >"%ROOT%steam_appid.txt" echo 3202030
)

start "" /D "%ROOT%" "%ROOT%LongYinLiZhiZhuan.exe"
