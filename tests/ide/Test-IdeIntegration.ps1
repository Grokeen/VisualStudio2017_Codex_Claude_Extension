#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-IdeIntegration
desc         : 별도 VS2017 인스턴스에 VSIX와 테스트 패키지를 설치하고 실제 편집기 테스트를 실행한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 실제 IDE 실행과 실행별 합성 솔루션·결과 격리.
update date  : 2026-10-18 CodexOrig : 최신 패키지·Provider 탭·개인 설정 격리 검증.
-------------------------------------------------------------------------
.SYNOPSIS
    Runs the installed product in a dedicated VS2017 root suffix. Live Codex is opt-in.+#>
[CmdletBinding()]
param(
    [string]$VisualStudioPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community',
    [string]$VsixPath,
    [string]$FixturePath,
    [string]$LiveCodexPath,
    [ValidateRange(30,600)][int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$VsixPath) { $VsixPath = Join-Path $repositoryDir 'src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.vsix' }
$VsixPath = (Resolve-Path -LiteralPath $VsixPath).Path
if (!$FixturePath) {
    $FixturePath = Get-ChildItem -LiteralPath (Join-Path $repositoryDir 'artifacts\source-editing') -Filter 'run-*' -Directory |
        Sort-Object LastWriteTime -Descending | ForEach-Object { Join-Path $_.FullName 'Fixture CLI 한글.exe' } |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$FixturePath) { throw 'tests/editing/Test-SourceEditing.ps1을 먼저 실행하거나 -FixturePath를 지정하세요.' }
$FixturePath = (Resolve-Path -LiteralPath $FixturePath).Path
if ($LiveCodexPath) { $LiveCodexPath = (Resolve-Path -LiteralPath $LiveCodexPath).Path }
$msbuild = Join-Path $VisualStudioPath 'MSBuild\15.0\Bin\MSBuild.exe'
$devenv = Join-Path $VisualStudioPath 'Common7\IDE\devenv.exe'
$installer = Join-Path $VisualStudioPath 'Common7\IDE\VSIXInstaller.exe'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$instances = & $vswhere -all -products '*' -format json -utf8 | ConvertFrom-Json
$instance = $instances | Where-Object { $_.installationPath -eq $VisualStudioPath } | Select-Object -First 1
if (!$instance) { throw 'VS2017 설치 인스턴스를 찾을 수 없습니다.' }
$runId = [guid]::NewGuid().ToString('N')
$rootSuffix = 'CodexCodeTest' + $runId.Substring(0,8)
$outputDir = Join-Path $repositoryDir ('artifacts\ide\run-' + $runId)
$null = New-Item -ItemType Directory -Path $outputDir -Force
$outputDir | Set-Content -LiteralPath (Join-Path $repositoryDir 'artifacts\ide-current.txt') -Encoding UTF8
& $msbuild (Join-Path $PSScriptRoot 'IdeIntegration.csproj') /restore /t:Rebuild /p:Configuration=Release /p:DeployExtension=false /verbosity:minimal /nologo 2>&1 |
    Tee-Object -FilePath (Join-Path $outputDir 'helper-build.log')
if ($LASTEXITCODE -ne 0) { throw 'IDE 테스트 패키지 빌드 실패.' }
$helperVsix = Join-Path $PSScriptRoot 'bin\Release\CodexCode.IdeIntegration.vsix'

Add-Type -AssemblyName System.IO.Compression.FileSystem
$package = [IO.Compression.ZipFile]::OpenRead($VsixPath)
try {
    $entry = $package.GetEntry('VS2017.AIAssistant.dll')
    if (!$entry) { throw '제품 DLL이 없는 VSIX입니다.' }
    $stream = $entry.Open()
    try { $sha = [Security.Cryptography.SHA256]::Create(); $packageDllHash = [BitConverter]::ToString($sha.ComputeHash($stream)) }
    finally { $stream.Dispose(); $sha.Dispose() }
} finally { $package.Dispose() }
$referenceDll = Join-Path $repositoryDir 'src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.dll'
if ($packageDllHash.Replace('-','') -ne (Get-FileHash -LiteralPath $referenceDll -Algorithm SHA256).Hash) {
    throw '테스트 참조 DLL과 VSIX에 포함된 제품 DLL이 다릅니다. 같은 빌드 결과를 사용하세요.'
}

$installStart = Get-Date
foreach ($packagePath in @($VsixPath, $helperVsix)) {
    $installArgs = '/quiet /rootSuffix:' + $rootSuffix + ' /instanceIds:' + $instance.instanceId + ' "' + $packagePath + '"'
    $installProcess = Start-Process -FilePath $installer -ArgumentList $installArgs -PassThru -Wait -WindowStyle Hidden
    if ($installProcess.ExitCode -ne 0) { throw "VSIX 설치 실패: $($installProcess.ExitCode), $packagePath" }
}
Get-ChildItem -LiteralPath $env:TEMP -Filter '*VSIXInstaller*.log' | Where-Object { $_.LastWriteTime -ge $installStart } |
    Copy-Item -Destination $outputDir
# 설치 프로그램의 기본 인스턴스 갱신과 별도로 테스트 suffix의 pkgdef 캐시를 갱신한다.
$configureArgs = '/RootSuffix ' + $rootSuffix + ' /UpdateConfiguration /Log "' + (Join-Path $outputDir 'update-configuration.xml') + '"'
$configureProcess = Start-Process -FilePath $devenv -ArgumentList $configureArgs -PassThru -Wait -WindowStyle Hidden
if ($configureProcess.ExitCode -ne 0) { throw "테스트 인스턴스 구성 갱신 실패: $($configureProcess.ExitCode)" }

$projectId = '{18FF2748-6B77-4F67-AF48-CE2D75CE460C}'
$utf8Bom = New-Object Text.UTF8Encoding($true)
[IO.File]::WriteAllText((Join-Path $outputDir 'Probe.cs'), "// 테스트 합성 코드`r`nclass Probe { int GetValue() { return 1; } }`r`n", $utf8Bom)
[IO.File]::WriteAllText((Join-Path $outputDir 'Other.cs'), "// 테스트 다른 파일`r`nclass Other { }`r`n", $utf8Bom)
$projectText = @'
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup><Configuration>Debug</Configuration><Platform>AnyCPU</Platform><OutputType>Library</OutputType><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion><OutputPath>bin\Debug\</OutputPath><ProjectGuid>{18FF2748-6B77-4F67-AF48-CE2D75CE460C}</ProjectGuid></PropertyGroup>
  <ItemGroup><Reference Include="System"/><Compile Include="Probe.cs"/><Compile Include="Other.cs"/></ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets"/>
</Project>
'@
[IO.File]::WriteAllText((Join-Path $outputDir 'IdeProbe.csproj'), $projectText, $utf8Bom)
$solutionText = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.28307.0
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "IdeProbe", "IdeProbe.csproj", "$projectId"
EndProject
Global
EndGlobal
"@
$solutionPath = Join-Path $outputDir 'IdeProbe.sln'
[IO.File]::WriteAllText($solutionPath, $solutionText, $utf8Bom)
$configPath = Join-Path $outputDir 'config.json'
[pscustomobject]@{
    OutputDirectory=$outputDir; SolutionPath=$solutionPath; FixturePath=$FixturePath
    LiveCodexPath=$LiveCodexPath; CloseWhenDone=$true
} | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
$startInfo = New-Object Diagnostics.ProcessStartInfo
$startInfo.FileName = $devenv
$startInfo.Arguments = '/RootSuffix ' + $rootSuffix + ' /NoSplash /ResetSettings General /Log "' + (Join-Path $outputDir 'ActivityLog.xml') + '"'
$startInfo.UseShellExecute = $false
$startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$startInfo.EnvironmentVariables['CODEXCODE_IDE_TEST_CONFIG'] = $configPath
$startInfo.EnvironmentVariables['VS2017_AI_SETTINGS_PATH'] = Join-Path $outputDir 'ui-settings.json'
$ideProcess = [Diagnostics.Process]::Start($startInfo)
[pscustomobject]@{
    ProcessId=$ideProcess.Id; RootSuffix=$rootSuffix; OutputDirectory=$outputDir; VsixPath=$VsixPath
    VsixSha256=(Get-FileHash -LiteralPath $VsixPath -Algorithm SHA256).Hash
    StartedAt=(Get-Date).ToString('O')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir 'launch.json') -Encoding UTF8
Write-Output "IDE PID $($ideProcess.Id), root suffix $rootSuffix, results $outputDir"
$clock = [Diagnostics.Stopwatch]::StartNew()
$resultPath = Join-Path $outputDir 'result.json'
while (!(Test-Path -LiteralPath $resultPath) -and !$ideProcess.HasExited -and $clock.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
    Start-Sleep -Milliseconds 500
}
if (!(Test-Path -LiteralPath $resultPath)) {
    throw "IDE 결과 없음 (PID $($ideProcess.Id), timeout=$TimeoutSeconds). 시작 화면/로그와 $outputDir 를 확인하세요."
}
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
$result | ConvertTo-Json -Depth 6
if (!$result.Succeeded) { throw "실제 IDE 통합 테스트 실패. $resultPath" }
Write-Output "실제 IDE 통합 테스트 PASS: $($result.Passed)/$($result.Passed + $result.Failed)"
