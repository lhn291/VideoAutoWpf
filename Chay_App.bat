@echo off
title VideoAutoWpf Launcher
cd /d "%~dp0"
echo ========================================================
echo   DANG KHOI DONG VIDEOAUTOWPF (BO QUA SMART APP CONTROL)
echo ========================================================
start "" dotnet "%~dp0bin\Debug\net10.0-windows\VideoAutoWpf.dll"
exit
