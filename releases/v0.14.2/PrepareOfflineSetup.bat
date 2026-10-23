@echo off
setlocal DisableDelayedExpansion
set "OFFLINE_PREP_SCRIPT=%~dp0Prepare-OfflineSetup.ps1"
if not exist "%OFFLINE_PREP_SCRIPT%" set "OFFLINE_PREP_SCRIPT=%~dp0scripts\Prepare-OfflineSetup.ps1"
if not exist "%OFFLINE_PREP_SCRIPT%" (
  echo Extract the complete offline tools ZIP first.
  pause
  exit /b 1
)
pushd "%~dp0"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%OFFLINE_PREP_SCRIPT%" -CreateZip
set "OFFLINE_PREP_EXIT=%ERRORLEVEL%"
popd
pause
exit /b %OFFLINE_PREP_EXIT%
