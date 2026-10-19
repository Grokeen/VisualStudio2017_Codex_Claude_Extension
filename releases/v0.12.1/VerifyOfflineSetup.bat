@echo off
setlocal
set "TASK_SCRIPT=%~dp0Test-OfflineSetupOnTarget.ps1"
if not exist "%TASK_SCRIPT%" set "TASK_SCRIPT=%~dp0scripts\Test-OfflineSetupOnTarget.ps1"
if not exist "%TASK_SCRIPT%" (
  echo Verification script is missing. Extract the entire prepared ZIP.
  pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%TASK_SCRIPT%" -BundleDirectory "%~dp0." -Install
set "TASK_EXIT=%ERRORLEVEL%"
pause
exit /b %TASK_EXIT%
