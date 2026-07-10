@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Publish-Portable.ps1" %*
exit /b %ERRORLEVEL%
