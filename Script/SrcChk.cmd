@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SrcChk.ps1" %*
exit /b %ERRORLEVEL%
