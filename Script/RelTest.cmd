@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0RelTest.ps1" %*
exit /b %ERRORLEVEL%
