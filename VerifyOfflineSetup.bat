@echo off
setlocal
set "TASK_SCRIPT=%~dp0Verify-OfflineFiles.ps1"
if not exist "%TASK_SCRIPT%" set "TASK_SCRIPT=%~dp0scripts\Verify-OfflineFiles.ps1"
if not exist "%TASK_SCRIPT%" (
  echo Checksum verifier is missing. Extract the entire ZIP.
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%TASK_SCRIPT%" -BundleDirectory "%~dp0."
set "TASK_EXIT=%ERRORLEVEL%"
echo Verification exit code: %TASK_EXIT%
if /i "%~1"=="--noninteractive" exit /b %TASK_EXIT%
if /i "%~1"=="--interactive" goto hold_result
powershell.exe -NoProfile -Command "if ([Console]::IsInputRedirected) {exit 1} else {exit 0}" >nul 2>nul
if errorlevel 1 exit /b %TASK_EXIT%
:hold_result
echo Press any key to close this verification window.
pause >nul
exit /b %TASK_EXIT%
