@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Export-Source.ps1" %*
exit /b %ERRORLEVEL%
