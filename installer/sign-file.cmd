@echo off
rem =====================================================================
rem SwitchCast — Inno Setup SignTool Bridge Wrapper
rem Passes target binary ($p) to PowerShell sign-target.ps1 securely
rem =====================================================================

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0sign-target.ps1" -FilePath "%~1"
exit /b %ERRORLEVEL%
