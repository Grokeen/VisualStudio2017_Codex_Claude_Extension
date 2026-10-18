<#
.SYNOPSIS
    전체 .NET Framework MSBuild로 선택한 Visual Studio용 VSIX를 복원하고 빌드한다.
.DESCRIPTION
    author: CodexCode
    create date: 2026-10-17
    update date: 2026-10-17 CodexCode : VS 탐색, 종료 코드 검사 및 CLI 배포 방지 추가.
                 2026-10-18 CodexOrig : TASK-020 버전별 프로젝트 빌드 지원.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('2017','2019','2022')][string]$VisualStudioVersion = '2017',
    [string]$MSBuildPath,
    [string]$NuGetPath
)

$ErrorActionPreference = 'Stop'
$repositoryDir = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryDir 'VS2017.AIAssistant.sln'
$targetPath = if ($VisualStudioVersion -eq '2017') { $solutionPath } else { Join-Path $repositoryDir ('src\VS' + $VisualStudioVersion + '.AIAssistant\VS' + $VisualStudioVersion + '.AIAssistant.csproj') }
if (!(Test-Path -LiteralPath $targetPath)) { throw "대상 프로젝트가 없습니다: $targetPath" }

# 선택한 버전의 MSBuild를 자동 탐색하고 명시적 경로도 지원한다.
if ([string]::IsNullOrWhiteSpace($MSBuildPath)) {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswherePath) {
        $engineRange = switch ($VisualStudioVersion) { '2017' { '[15.0,16.0)' } '2019' { '[16.0,17.0)' } '2022' { '[17.0,18.0)' } }
        $MSBuildPath = & $vswherePath -latest -version $engineRange -products '*' `
            -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($MSBuildPath)) {
        $buildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
        if ($null -ne $buildCommand) { $MSBuildPath = $buildCommand.Source }
    }
}
if ([string]::IsNullOrWhiteSpace($MSBuildPath) -or !(Test-Path -LiteralPath $MSBuildPath)) {
    throw '전체 .NET Framework MSBuild가 필요합니다. VS2017 확장 개발 워크로드를 설치하거나 -MSBuildPath에 MSBuild.exe를 지정하세요.'
}
$MSBuildPath = (Resolve-Path -LiteralPath $MSBuildPath).Path
Write-Host ("VS" + $VisualStudioVersion + " MSBuild: " + $MSBuildPath)

if (![string]::IsNullOrWhiteSpace($NuGetPath)) {
    $NuGetPath = (Resolve-Path -LiteralPath $NuGetPath).Path
    & $NuGetPath restore $targetPath -MSBuildPath (Split-Path -Parent $MSBuildPath) `
        -Source 'https://api.nuget.org/v3/index.json' -NonInteractive
    if ($LASTEXITCODE -ne 0) { throw "NuGet 복원 실패: $LASTEXITCODE" }
}

$buildArguments = @($targetPath, '/t:Rebuild', '/m', '/nologo', '/v:minimal',
    "/p:Configuration=$Configuration", '/p:DeployExtension=false', '/p:ZipPackageCompressionLevel=normal')
if ([string]::IsNullOrWhiteSpace($NuGetPath)) { $buildArguments += '/restore' }
& $MSBuildPath @buildArguments
if ($LASTEXITCODE -ne 0) { throw "VSIX 빌드 실패: $LASTEXITCODE" }

$vsixPath = Join-Path $repositoryDir "src\VS$VisualStudioVersion.AIAssistant\bin\$Configuration\VS2017.AIAssistant.vsix"
if (!(Test-Path -LiteralPath $vsixPath)) { throw "VSIX 출력이 없습니다: $vsixPath" }
Write-Output "VSIX 빌드 완료: $vsixPath"
