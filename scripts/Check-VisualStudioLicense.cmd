@echo off
setlocal DisableDelayedExpansion
rem ---------------------------------------------------------------------
rem name         : Check-VisualStudioLicense
rem desc         : Report observed IDE licensing prompts without changing licenses.
rem author       : CodexOrig
rem create date  : 2026-10-18
rem update date  : 2026-10-18 CodexOrig : TASK-020 audit F03 actual IDE evidence.
rem ---------------------------------------------------------------------
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%~dp0Test-VisualStudioLicense.ps1" %*
exit /b %errorlevel%
