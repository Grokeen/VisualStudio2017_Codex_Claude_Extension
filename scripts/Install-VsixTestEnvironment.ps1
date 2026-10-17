#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Install-VsixTestEnvironment
desc         : VS2017/2019/2022 Community를 VSIX 테스트용으로 순차 설치한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : 서명·해시 검증, PS5 JSON 배열 정규화, 구성된 IDE 건너뛰기 추가.
-------------------------------------------------------------------------
.SYNOPSIS
Installs the three IDE versions with WPF, VSSDK, and .NET Framework 4.6.2 tools.
.DESCRIPTION
Run -PlanOnly without elevation to inspect the exact installation plan.
Actual installation requires Windows administrator elevation. No restart is performed.
#>
[CmdletBinding()]
param(
    [ValidateSet('2017', '2019', '2022')][string[]]$Versions = @('2017', '2019', '2022'),
    [string]$InstallerDirectory = (Join-Path $env:LOCALAPPDATA 'CodexOrig\VSIX-TestEnvironment\installers'),
    [string]$LogDirectory = (Join-Path $env:LOCALAPPDATA 'CodexOrig\VSIX-TestEnvironment\logs'),
    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$components = @(
    'Microsoft.VisualStudio.Workload.ManagedDesktop',
    'Microsoft.VisualStudio.Workload.VisualStudioExtension',
    'Microsoft.VisualStudio.Component.VSSDK',
    'Microsoft.Net.Component.4.6.2.TargetingPack',
    'Microsoft.Net.Component.4.6.2.SDK'
)
# 확인한 공식 Microsoft bootstrapper를 고정하고 파일이 갱신되면 재확인을 요구한다.
$definitions = @{
    '2017' = @{ Major = 15; Sha256 = '1BF11FC98CF22D93498180D50794219A0CBB860A98880E29836BB66ACADC0123'; Uri = 'https://aka.ms/vs/15/release/vs_community.exe'; Path = (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2017\Community') }
    '2019' = @{ Major = 16; Sha256 = 'D70A79F50E37C337227A928875FCEF8D233B71056D4C967F070D92F1584D2C7E'; Uri = 'https://aka.ms/vs/16/release/vs_community.exe'; Path = (Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2019\Community') }
    '2022' = @{ Major = 17; Sha256 = 'D52E18859B0996BAC3A89BA7D07E180508E49EB55F824D7EDA2822ADED495020'; Uri = 'https://download.visualstudio.microsoft.com/download/pr/c95450be-0a45-4779-b142-33f567ccfd6e/d52e18859b0996bac3a89ba7d07e180508e49eb55f824d7eda2822aded495020/vs_Community.exe'; Path = (Join-Path $env:ProgramFiles 'Microsoft Visual Studio\2022\Community') }
}
$plan = @()
foreach ($year in $Versions | Select-Object -Unique) {
    $definition = $definitions[$year]
    $plan += [pscustomobject]@{
        Year = $year; Major = $definition.Major
        Installer = Join-Path $InstallerDirectory "vs${year}_community.exe"
        DownloadUri = $definition.Uri; ExpectedSha256 = $definition.Sha256
        InstallPath = $definition.Path; Components = $components
        Language = 'ko-KR'; NoRestart = $true
    }
}
if ($PlanOnly) { $plan | ConvertTo-Json -Depth 6; return }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not ([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Administrator elevation is required for installation. Use the Windows UAC prompt; do not disable UAC.'
}
$null = New-Item -ItemType Directory -Path $InstallerDirectory -Force
$null = New-Item -ItemType Directory -Path $LogDirectory -Force
$statusPath = Join-Path $LogDirectory 'install-status.json'
$runStatus = [pscustomobject]@{ StartedAt = [DateTimeOffset]::Now.ToString('o'); State = 'Preparing'; CurrentVersion = ''; RestartRequired = $false; Results = @(); Error = '' }
$runStatus | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $statusPath -Encoding UTF8
Start-Transcript -LiteralPath (Join-Path $LogDirectory ('install-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')) | Out-Null
try {
    # 먼저 모든 설치 파일을 확인하여 도중에 다른 버전이나 서명 불명 파일을 실행하지 않는다.
    foreach ($item in $plan) {
        if (-not (Test-Path -LiteralPath $item.Installer -PathType Leaf)) {
            Invoke-WebRequest -Uri $item.DownloadUri -OutFile $item.Installer -UseBasicParsing -TimeoutSec 120
        }
        $signature = Get-AuthenticodeSignature -LiteralPath $item.Installer
        $fileVersion = [version](Get-Item -LiteralPath $item.Installer).VersionInfo.FileVersion
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)' -or
            $fileVersion.Major -ne $item.Major -or (Get-FileHash -LiteralPath $item.Installer -Algorithm SHA256).Hash -ne $item.ExpectedSha256) {
            throw "Installer signature, version, or SHA256 mismatch for VS$($item.Year)."
        }
    }
    foreach ($item in $plan) {
        $runStatus.State = 'Installing'
        $runStatus.CurrentVersion = $item.Year
        $runStatus | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $statusPath -Encoding UTF8
        $arguments = @('--passive', '--norestart', '--wait', '--installPath', ('"' + $item.InstallPath + '"'), '--addProductLang', 'ko-KR')
        $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (Test-Path -LiteralPath $vswherePath) {
            $existingText = & $vswherePath -all -products '*' -version "[$($item.Major).0,$($item.Major + 1).0)" -format json -utf8
            # PS5의 ConvertFrom-Json은 JSON 배열 자체를 파이프라인 단일 객체로 내보낸다.
            $parsedInstances = $existingText | ConvertFrom-Json
            $existing = @($parsedInstances)
            if ($existing.Count -gt 1) { throw "Multiple VS$($item.Year) instances exist. Select the intended instance before modifying workloads." }
            if ($existing.Count -eq 1) {
                $item.InstallPath = $existing[0].installationPath
                $configuredPaths = @(& $vswherePath -all -products '*' -version "[$($item.Major).0,$($item.Major + 1).0)" -requires $components -property installationPath)
                if ($existing[0].isComplete -and ($configuredPaths -contains $item.InstallPath)) {
                    $runStatus.Results += [pscustomobject]@{ Year = $item.Year; InstallPath = $item.InstallPath; ExitCode = 0; FinishedAt = [DateTimeOffset]::Now.ToString('o'); State = 'AlreadyConfigured' }
                    $runStatus | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $statusPath -Encoding UTF8
                    Write-Host "VS$($item.Year) already has all required components. Skipping."
                    continue
                }
                $arguments = @('modify', '--passive', '--norestart', '--wait', '--installPath', ('"' + $item.InstallPath + '"'), '--addProductLang', 'ko-KR')
            }
        }
        foreach ($component in $components) { $arguments += @('--add', $component) }
        Write-Host "Installing VS$($item.Year) at $($item.InstallPath)"
        # 제어용 프로세스는 숨기고 실제 설치 진행 UI는 --passive로 표시한다.
        $process = Start-Process -FilePath $item.Installer -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
        $exitCode = $process.ExitCode
        $runStatus.Results += [pscustomobject]@{ Year = $item.Year; InstallPath = $item.InstallPath; ExitCode = $exitCode; FinishedAt = [DateTimeOffset]::Now.ToString('o') }
        if ($exitCode -eq 3010) { $runStatus.RestartRequired = $true }
        $runStatus | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $statusPath -Encoding UTF8
        if ($exitCode -notin @(0, 3010)) { throw "VS$($item.Year) setup failed with exit code $exitCode. Inspect %TEMP%\dd_* logs." }
        if ($exitCode -eq 3010) { $runStatus.State = 'RestartRequired'; break }
    }
    if (-not $runStatus.RestartRequired) { $runStatus.State = 'InstallersCompleted' }
}
catch {
    $runStatus.State = 'Failed'
    $runStatus.Error = $_.Exception.Message
    Write-Host $runStatus.Error
}
finally {
    $runStatus | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $statusPath -Encoding UTF8
    Stop-Transcript | Out-Null
}
if ($runStatus.State -eq 'Failed') { exit 1 }
if ($runStatus.RestartRequired) { exit 3010 }
exit 0
