@echo off
setlocal DisableDelayedExpansion
rem ---------------------------------------------------------------------
rem name         : Build-Vsix
rem desc         : Build with MSBuild without starting the Visual Studio IDE.
rem author       : CodexOrig
rem create date  : 2026-10-18
rem update date  : 2026-10-18 CodexOrig : version selection and exit forwarding.
rem ---------------------------------------------------------------------
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Invoke-VsixBuild.ps1" %*
exit /b %errorlevel%
