@echo off
setlocal
set "AI_ASSISTANT_SCRIPT=%~dp0Install-AIAssistant.ps1"
if not exist "%AI_ASSISTANT_SCRIPT%" set "AI_ASSISTANT_SCRIPT=%~dp0scripts\Install-AIAssistant.ps1"
if not exist "%AI_ASSISTANT_SCRIPT%" (
  echo Install-AIAssistant.ps1 is missing. Extract the complete ZIP first.
  pause
  exit /b 1
)
set "AI_ASSISTANT_PACKAGES=%~dp0."
if not exist "%~dp0SHA256SUMS.txt" if exist "%~dp0releases\v0.11.1\SHA256SUMS.txt" set "AI_ASSISTANT_PACKAGES=%~dp0releases\v0.11.1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%AI_ASSISTANT_SCRIPT%" -PackageDirectory "%AI_ASSISTANT_PACKAGES%" -Install
set "AI_ASSISTANT_EXIT=%ERRORLEVEL%"
pause
exit /b %AI_ASSISTANT_EXIT%
