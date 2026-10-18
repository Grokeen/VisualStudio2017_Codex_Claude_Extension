@echo off
setlocal DisableDelayedExpansion
rem CodexOrig3 - Official native Codex / Claude Code installer for Windows.
rem This single file contains its PowerShell payload below the batch entry point.
set "CLI_INSTALL_SELF=%~f0"
set "CLI_INSTALL_NO_PAUSE=0"
set "CLI_INSTALL_CHECK_ONLY=0"
set "CLI_INSTALL_RECORDED=0"

:parse_args
if "%~1"=="" goto run
if /i "%~1"=="/nopause" (
    set "CLI_INSTALL_NO_PAUSE=1"
    shift
    goto parse_args
)
if /i "%~1"=="/check" (
    set "CLI_INSTALL_CHECK_ONLY=1"
    shift
    goto parse_args
)
if /i "%~1"=="/recorded" (
    set "CLI_INSTALL_RECORDED=1"
    shift
    goto parse_args
)
if /i "%~1"=="/help" goto help
echo ERROR: Unknown option. Run InstallCLI.bat /help for usage.
exit /b 2

:help
echo Usage: InstallCLI.bat [/recorded] [/check] [/nopause]
echo.
echo Default    Install the latest native Codex CLI and Claude Code.
echo /recorded  Select Codex 0.163.0 and Claude Code 2.1.302 from the guide.
echo /check     Check existing native files without installation or PATH edits.
echo /nopause   Return immediately when finished, including on failure.
echo.
echo Run as your normal Windows user. Node.js and npm are not required.
echo A network connection is required for installation. Login is a separate step.
exit /b 0

:run
rem Use Windows PowerShell included with Windows, including from a 32-bit caller.
set "CLI_INSTALL_HOST=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if exist "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe" set "CLI_INSTALL_HOST=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
rem Do not inherit PowerShell 7 module paths into Windows PowerShell 5.1.
set "PSModulePath="
if not exist "%CLI_INSTALL_HOST%" (
    echo ERROR: Windows PowerShell was not found.
    set "CLI_INSTALL_EXIT=1"
    goto finish
)
"%CLI_INSTALL_HOST%" -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "try { $parts = (Get-Content -LiteralPath $env:CLI_INSTALL_SELF -Raw) -split '(?m)^# POWERSHELL_PAYLOAD_START\r?$', 2; if ($parts.Count -ne 2) { throw 'Embedded installer payload is missing.' }; & ([ScriptBlock]::Create($parts[1])) } catch { Write-Host ('ERROR: ' + $_.Exception.Message); exit 1 }"
set "CLI_INSTALL_EXIT=%errorlevel%"

:finish
echo.
if not "%CLI_INSTALL_EXIT%"=="0" echo Installation or verification failed. See the error and log path above.
if "%CLI_INSTALL_NO_PAUSE%"=="0" pause
exit /b %CLI_INSTALL_EXIT%

# POWERSHELL_PAYLOAD_START
# Author: CodexOrig3. Created: 2026-10-18. Compatible with Windows PowerShell 5.1.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$checkOnly = $env:CLI_INSTALL_CHECK_ONLY -eq '1'
$recorded = $env:CLI_INSTALL_RECORDED -eq '1'
$resultCode = 1
$transcriptStarted = $false

function Refresh-InstallPath {
    $machinePathValue = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPathValue = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machinePathValue;$userPathValue"
}

function Add-UserPathFolder([string]$Folder) {
    $userPathValue = [Environment]::GetEnvironmentVariable('Path', 'User')
    $present = @($userPathValue -split ';' | Where-Object {
        [Environment]::ExpandEnvironmentVariables($_.Trim().Trim('"')).TrimEnd('\') -ieq $Folder.TrimEnd('\')
    }).Count -gt 0
    if (-not $present) {
        $newPathValue = $Folder
        if (-not [string]::IsNullOrWhiteSpace($userPathValue)) {
            $newPathValue = $Folder + ';' + $userPathValue
        }
        [Environment]::SetEnvironmentVariable('Path', $newPathValue, 'User')
        Write-Host ('Added to user PATH: ' + $Folder)
    }
    Refresh-InstallPath
}

function Invoke-OfficialInstaller($Tool, [string]$RunDirectory) {
    $installerPath = Join-Path $RunDirectory $Tool.Script
    $installerLog = Join-Path $RunDirectory ($Tool.Name + '-installer.log')
    Write-Host ('Downloading official installer: ' + $Tool.Url)
    Invoke-WebRequest -UseBasicParsing -Uri $Tool.Url -OutFile $installerPath -TimeoutSec 120
    $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $installerPath, $Tool.VersionParameter, $Tool.Version)
    Write-Host ('Installing ' + $Tool.Name + ' (' + $Tool.Version + ')...')
    $previousPreference = $ErrorActionPreference
    try {
        # Windows PowerShell represents a native process's stderr as ErrorRecords.
        # Retain that output and judge the child by its exit code below.
        $ErrorActionPreference = 'Continue'
        & $env:CLI_INSTALL_HOST @arguments 2>&1 | Tee-Object -FilePath $installerLog
        $installerExitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousPreference
    }
    if ($installerExitCode -ne 0) {
        throw ('Official installer exited with code ' + $installerExitCode + '. Details: ' + $installerLog)
    }
}

function Test-NativeCli($Tool, [string]$RunDirectory) {
    if (-not (Test-Path -LiteralPath $Tool.Exe -PathType Leaf)) {
        throw ('Native executable was not found: ' + $Tool.Exe)
    }
    $versionOutput = @(& $Tool.Exe --version 2>&1)
    if ($LASTEXITCODE -ne 0) { throw 'The --version command failed.' }
    $versionText = ($versionOutput | ForEach-Object { [string]$_ }) -join "`n"
    $versionMatch = [regex]::Match($versionText, $Tool.VersionPattern)
    if (-not $versionMatch.Success) { throw ('Unexpected version output: ' + $versionText) }
    if ($recorded -and $versionMatch.Groups[1].Value -ne $Tool.Version) {
        throw ('Expected version ' + $Tool.Version + ', observed ' + $versionMatch.Groups[1].Value)
    }
    $helpPath = Join-Path $RunDirectory ($Tool.Name + '-help.txt')
    & $Tool.Exe --help > $helpPath 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'The --help command failed.' }
    Write-Host ('Verified: ' + $versionText)
    Write-Host ('Executable: ' + $Tool.Exe)
    Write-Host ('Help output: ' + $helpPath)
}

try {
    if (-not [Environment]::Is64BitOperatingSystem -or -not [Environment]::Is64BitProcess) {
        throw 'A 64-bit Windows installation and PowerShell process are required.'
    }
    $runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $runDirectory = Join-Path (Join-Path $env:TEMP 'CodexOrig3-CLI-Install') $runName
    New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
    $transcriptPath = Join-Path $runDirectory 'install.log'
    Start-Transcript -LiteralPath $transcriptPath -Force | Out-Null
    $transcriptStarted = $true
    Write-Host 'CodexOrig3 - Codex CLI and Claude Code native setup'
    Write-Host ('Log folder: ' + $runDirectory)
    Write-Host ('Mode: ' + $(if ($checkOnly) { 'verification only' } else { 'install and verify' }))

    $codexFolder = Join-Path $env:LOCALAPPDATA 'Programs\OpenAI\Codex\bin'
    $claudeFolder = Join-Path $env:USERPROFILE '.local\bin'
    $toolsToInstall = @(
        [PSCustomObject]@{
            Name = 'codex'; Url = 'https://chatgpt.com/codex/install.ps1'
            Script = 'install-codex.ps1'; Exe = (Join-Path $codexFolder 'codex.exe')
            VersionParameter = '-Release'; Version = $(if ($recorded) { '0.163.0' } else { 'latest' })
            VersionPattern = '(?m)^codex-cli\s+(\S+)'
        },
        [PSCustomObject]@{
            Name = 'claude'; Url = 'https://claude.ai/install.ps1'
            Script = 'install-claude.ps1'; Exe = (Join-Path $claudeFolder 'claude.exe')
            VersionParameter = '-Target'; Version = $(if ($recorded) { '2.1.302' } else { 'latest' })
            VersionPattern = '(?m)^(\S+)\s+\(Claude Code\)'
        }
    )

    Refresh-InstallPath
    if (-not $checkOnly) {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        $env:CODEX_NON_INTERACTIVE = '1'
        $env:CODEX_INSTALL_DIR = $codexFolder
        # Ignore internal daemon/updater flags in an inherited Codex app session.
        foreach ($name in @('CODEX_INSTALL_DAEMON_ONLY', 'CODEX_INSTALL_DEFER_SELECTION', 'CODEX_INSTALL_IF_LATEST', 'CODEX_INSTALL_IF_CURRENT')) {
            [Environment]::SetEnvironmentVariable($name, $null, 'Process')
        }
    }

    $failures = @()
    foreach ($tool in $toolsToInstall) {
        Write-Host ("`n--- " + $tool.Name + ' ---')
        try {
            if (-not $checkOnly) { Invoke-OfficialInstaller $tool $runDirectory }
            Test-NativeCli $tool $runDirectory
            if (-not $checkOnly) { Add-UserPathFolder (Split-Path -Parent $tool.Exe) }
            $resolvedCommand = Get-Command ($tool.Name + '.exe') -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($null -eq $resolvedCommand -or $resolvedCommand.Source -ine $tool.Exe) {
                Write-Warning 'PATH selects another installation or has no matching command. Use the executable path printed above, or reopen your terminal after correcting PATH.'
            } else {
                Write-Host ('PATH resolves: ' + $resolvedCommand.Source)
            }
        } catch {
            $failure = $tool.Name + ': ' + $_.Exception.Message
            $failures += $failure
            Write-Host ('FAILED: ' + $failure)
        }
    }

    Write-Host "`nLog folder: $runDirectory"
    if ($failures.Count -gt 0) {
        Write-Host ('FAILED: ' + $failures.Count + ' CLI installation/verification step(s).')
        $resultCode = 1
    } else {
        Write-Host 'SUCCESS: Both native CLI executables passed --version and --help.'
        Write-Host 'Open a NEW terminal (and restart Visual Studio) to use the saved PATH.'
        Write-Host 'Login separately if needed: codex login / claude auth login'
        Write-Host 'Authentication and model requests were not performed.'
        $resultCode = 0
    }
} catch {
    Write-Host ('ERROR: ' + $_.Exception.Message)
    $resultCode = 1
} finally {
    if ($transcriptStarted) { Stop-Transcript | Out-Null }
}
exit $resultCode
