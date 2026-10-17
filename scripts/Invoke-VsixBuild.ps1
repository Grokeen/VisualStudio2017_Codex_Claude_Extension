#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Invoke-VsixBuild
desc         : 설치된 버전의 MSBuild를 선택하여 기존 빌드 스크립트를 호출한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : IDE 없는 배치 빌드, 사전 확인 및 오류 전달.
-------------------------------------------------------------------------
.SYNOPSIS
    Visual Studio IDE를 실행하지 않고 VSIX를 빌드한다.
.DESCRIPTION
    배치 빌드에서 IDE의 시작/라이선스 배너가 발생하지 않도록 MSBuild를 사용한다.
    IDE의 라이선스 상태를 변경하거나 확인하지 않는다. IDE가 로그인을 요구하면
    해당 IDE에서 사용자 계정으로 로그인해야 한다. 빌드 출력은 필터링하지 않는다.
.EXAMPLE
    .\scripts\Build-Vsix.cmd -VisualStudioVersion 2017 -Configuration Release
.EXAMPLE
    .\scripts\Build-Vsix.cmd -VisualStudioVersion 2019 -PlanOnly
#>
[CmdletBinding()]
param(
    [ValidateSet('2017', '2019', '2022')]
    [string]$VisualStudioVersion = '2017',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$MSBuildPath,
    [string]$VsWherePath,
    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
try {
    # 리다이렉트된 Windows PowerShell 출력도 UTF-8로 통일하여 한글 JSON/로그를 보존한다.
    if ([Console]::IsOutputRedirected) {
        [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
    }
    $major = switch ($VisualStudioVersion) { '2017' { 15 } '2019' { 16 } '2022' { 17 } }
    $buildScript = Join-Path $PSScriptRoot 'build.ps1'
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) {
        throw "Build entry is missing: $buildScript"
    }

    if ([string]::IsNullOrWhiteSpace($MSBuildPath)) {
        if ([string]::IsNullOrWhiteSpace($VsWherePath)) {
            $VsWherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        }
        if (-not (Test-Path -LiteralPath $VsWherePath -PathType Leaf)) {
            throw 'vswhere.exe was not found. Install Visual Studio or specify -MSBuildPath.'
        }
        # Build Tools만 있는 인스턴스 및 구성 중인 IDE를 선택하지 않는다.
        # PS5.1 콘솔 코드 페이지와 무관하게 -utf8 JSON의 한글 경로를 보존한다.
        $startInfo = New-Object Diagnostics.ProcessStartInfo
        $startInfo.FileName = (Resolve-Path -LiteralPath $VsWherePath).ProviderPath
        $startInfo.Arguments = '-latest -version "[' + $major + '.0,' + ($major + 1) + '.0)"' +
            ' -products Microsoft.VisualStudio.Product.Community Microsoft.VisualStudio.Product.Professional Microsoft.VisualStudio.Product.Enterprise' +
            ' -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VSSDK -format json -utf8'
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.StandardOutputEncoding = [Text.Encoding]::UTF8
        $startInfo.StandardErrorEncoding = [Text.Encoding]::UTF8
        $process = New-Object Diagnostics.Process
        $process.StartInfo = $startInfo
        try {
            $null = $process.Start()
            $stdout = $process.StandardOutput.ReadToEndAsync()
            $stderr = $process.StandardError.ReadToEndAsync()
            $process.WaitForExit()
            if ($process.ExitCode -ne 0) { throw "vswhere failed ($($process.ExitCode)): $($stderr.Result)" }
            $parsed = $stdout.Result | ConvertFrom-Json
        }
        finally { $process.Dispose() }
        $instances = @($parsed)
        if ($instances.Count -ne 1 -or $null -eq $instances[0]) {
            throw "VS$VisualStudioVersion with MSBuild and the Visual Studio SDK was not found. No version fallback is performed."
        }
        $instance = $instances[0]
        if (-not $instance.isComplete -or -not $instance.isLaunchable -or
            ([version]$instance.installationVersion).Major -ne $major) {
            throw 'The selected Visual Studio instance is incomplete or has an unexpected version.'
        }
        $relativePath = if ($major -eq 15) { 'MSBuild\15.0\Bin\MSBuild.exe' } else { 'MSBuild\Current\Bin\MSBuild.exe' }
        $MSBuildPath = Join-Path $instance.installationPath $relativePath
    }

    if (-not (Test-Path -LiteralPath $MSBuildPath -PathType Leaf) -or
        [IO.Path]::GetFileName($MSBuildPath) -ine 'MSBuild.exe') {
        throw "MSBuild.exe was not found: $MSBuildPath"
    }
    $MSBuildPath = (Resolve-Path -LiteralPath $MSBuildPath).ProviderPath
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($MSBuildPath)
    if ($fileVersion.FileMajorPart -ne $major) {
        throw "VS$VisualStudioVersion requires MSBuild $major.x; selected file is $($fileVersion.FileVersion)."
    }
    if ($PlanOnly) {
        [pscustomobject]@{
            VisualStudioVersion = $VisualStudioVersion
            Configuration = $Configuration
            MSBuildPath = $MSBuildPath
            MSBuildVersion = $fileVersion.FileVersion
            BuildScript = $buildScript
            LaunchesIde = $false
            ChecksIdeLicense = $false
        } | ConvertTo-Json
        exit 0
    }
    Write-Host "VS$VisualStudioVersion MSBuild $($fileVersion.FileVersion): $MSBuildPath"
    # 기존 복원·Rebuild·VSIX 출력 확인을 그대로 사용한다. IDE 실행/배포는 하지 않는다.
    & $buildScript -Configuration $Configuration -MSBuildPath $MSBuildPath
    if (-not $?) { throw 'The build script failed.' }
    exit 0
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    exit 1
}
