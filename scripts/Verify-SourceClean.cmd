@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Verify-SourceClean.ps1" %*
exit /b %ERRORLEVEL%
