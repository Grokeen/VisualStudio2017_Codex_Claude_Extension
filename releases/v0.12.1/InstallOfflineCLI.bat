@echo off
setlocal DisableDelayedExpansion
set "OFFLINE_CLI_SCRIPT=%~dp0Install-OfflineCli.ps1"
if not exist "%OFFLINE_CLI_SCRIPT%" set "OFFLINE_CLI_SCRIPT=%~dp0scripts\Install-OfflineCli.ps1"
if not exist "%OFFLINE_CLI_SCRIPT%" (
  echo Extract the complete prepared offline package first.
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%OFFLINE_CLI_SCRIPT%" -BundleDirectory "%~dp0." -Install
set "OFFLINE_CLI_EXIT=%ERRORLEVEL%"
pause
exit /b %OFFLINE_CLI_EXIT%
