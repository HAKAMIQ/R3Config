@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Test-Release.ps1" %*
exit /b %ERRORLEVEL%
