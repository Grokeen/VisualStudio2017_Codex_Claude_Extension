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
set "AI_ASSISTANT_PACKAGE_VERSION=0.15.1"
if not exist "%~dp0SHA256SUMS.txt" if exist "%~dp0releases\v%AI_ASSISTANT_PACKAGE_VERSION%\SHA256SUMS.txt" set "AI_ASSISTANT_PACKAGES=%~dp0releases\v%AI_ASSISTANT_PACKAGE_VERSION%"
if not exist "%AI_ASSISTANT_PACKAGES%\SHA256SUMS.txt" goto missing_packages
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%AI_ASSISTANT_SCRIPT%" -PackageDirectory "%AI_ASSISTANT_PACKAGES%" -PackageVersion "%AI_ASSISTANT_PACKAGE_VERSION%" -Install
set "AI_ASSISTANT_EXIT=%ERRORLEVEL%"
if /i "%~1"=="--noninteractive" goto done
pause
:done
exit /b %AI_ASSISTANT_EXIT%

:missing_packages
echo Current version installation packages are missing. Extract the complete package first.
if /i "%~1"=="--noninteractive" goto missing_exit
pause
:missing_exit
exit /b 1
