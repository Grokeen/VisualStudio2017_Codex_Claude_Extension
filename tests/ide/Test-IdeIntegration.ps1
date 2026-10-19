#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-IdeIntegration
desc         : 별도 VS2017 인스턴스에 VSIX와 테스트 패키지를 설치하고 실제 편집기 테스트를 실행한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 실제 IDE 실행과 실행별 합성 솔루션·결과 격리.
update date  : 2026-10-18 CodexOrig : 최신 패키지·Provider 탭·개인 설정 격리 검증.
update date  : 2026-10-18 CodexOrig : 기본 안내 격리 및 별도 첫 사용 UX15 실제 호스트 검증.
update date  : 2026-10-18 CodexOrig : 지정 VSIX에서 추출한 DLL을 참조하여 Markdown 대화·SQL·설정 UX 검증을 격리.
               2026-10-19 CodexOrig : 기존 검정 팔레트 회귀는 명시적 Dark 설정에서 실행하고 새 테마 모드는 별도 검사.
               2026-10-19 CodexOrig : 프로젝트 속성 fixture 추가·실패 결과도 보존 후 전용 IDE만 정리.
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
$devenv = Join-Path $VisualStudioPath 'Common7\IDE\devenv.exe'
$installer = Join-Path $VisualStudioPath 'Common7\IDE\VSIXInstaller.exe'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$instances = & $vswhere -all -products '*' -format json -utf8 | ConvertFrom-Json
$instance = $instances | Where-Object { $_.installationPath -eq $VisualStudioPath } | Select-Object -First 1
if (!$instance) { throw 'VS2017 설치 인스턴스를 찾을 수 없습니다.' }
$major=([version]$instance.installationVersion).Major
if($major -notin @(15,16,17)){throw 'VS2017/2019/2022 설치 경로를 지정하세요.'}
$msbuild=Join-Path $VisualStudioPath $(if($major -eq 15){'MSBuild\15.0\Bin\MSBuild.exe'}else{'MSBuild\Current\Bin\MSBuild.exe'})
$runId = [guid]::NewGuid().ToString('N')
$rootSuffix = 'CodexCodeTest' + $runId.Substring(0,8)
$outputDir = Join-Path $repositoryDir ('artifacts\ide\run-' + $runId)
$null = New-Item -ItemType Directory -Path $outputDir -Force
$runtimeFixturePath=Join-Path $outputDir 'NativeProtocolFixture.exe'
$fixtureCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $fixtureCompiler /nologo /target:exe "/out:$runtimeFixturePath" /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot '..\task020\RuntimeProtocolFixture.cs')
if($LASTEXITCODE -ne 0){throw '실제 IDE용 네이티브 프로토콜 fixture 컴파일 실패.'}
$outputDir | Set-Content -LiteralPath (Join-Path $repositoryDir 'artifacts\ide-current.txt') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
$referenceDir = Join-Path $outputDir 'ProductReference'
$null = New-Item -ItemType Directory -Path $referenceDir
$referenceDll = Join-Path $referenceDir 'VS2017.AIAssistant.dll'
$package = [IO.Compression.ZipFile]::OpenRead($VsixPath)
try {
    $pdfEntries=@($package.Entries|Where-Object {$_.FullName -match '\.pdf$'})
    if($pdfEntries.Count -ne 1 -or [Uri]::UnescapeDataString($pdfEntries[0].FullName) -cne 'Resources/CliGuide/VisualStudio_AI_Assistant_설치_사용설명서.pdf'){throw 'Runtime PDF name differs from installed VSIX entry.'}
    $pdfStream=$pdfEntries[0].Open()
    try{$pdfSha=[Security.Cryptography.SHA256]::Create();$packagePdfHash=[BitConverter]::ToString($pdfSha.ComputeHash($pdfStream)).Replace('-','')}
    finally{$pdfStream.Dispose();$pdfSha.Dispose()}
    $entry = $package.GetEntry('VS2017.AIAssistant.dll')
    if (!$entry) { throw '제품 DLL이 없는 VSIX입니다.' }
    $stream = $entry.Open()
    try { $sha = [Security.Cryptography.SHA256]::Create(); $packageDllHash = [BitConverter]::ToString($sha.ComputeHash($stream)) }
    finally { $stream.Dispose(); $sha.Dispose() }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $referenceDll)
} finally { $package.Dispose() }
if ($packageDllHash.Replace('-','') -ne (Get-FileHash -LiteralPath $referenceDll -Algorithm SHA256).Hash) {
    throw 'VSIX에서 추출한 테스트 참조 DLL의 해시가 일치하지 않습니다.'
}
. (Join-Path $PSScriptRoot 'New-IdeTestTarget.ps1')
$helperProject=New-IdeTestTarget -Major $major -Directory (Join-Path $outputDir 'HelperProject')
& $msbuild $helperProject /restore /t:Rebuild /p:Configuration=Release /p:DeployExtension=false "/p:ProductAssemblyPath=$referenceDll" /verbosity:minimal /nologo 2>&1 |
    Tee-Object -FilePath (Join-Path $outputDir 'helper-build.log')
if ($LASTEXITCODE -ne 0) { throw 'IDE 테스트 패키지 빌드 실패.' }
$helperVsix = Join-Path ([IO.Path]::GetDirectoryName($helperProject)) 'bin\Release\CodexCode.IdeIntegration.vsix'

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
  <PropertyGroup><Configuration>Debug</Configuration><Platform>AnyCPU</Platform><OutputType>Library</OutputType><RootNamespace>IdeProbe</RootNamespace><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion><OutputPath>bin\Debug\</OutputPath><ProjectGuid>{18FF2748-6B77-4F67-AF48-CE2D75CE460C}</ProjectGuid></PropertyGroup>
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
    LiveCodexPath=$LiveCodexPath; CloseWhenDone=$true; ExpectedIdeMajor=$major.ToString(); RuntimeFixturePath=$runtimeFixturePath; ExpectedProductSha256=(Get-FileHash -LiteralPath $referenceDll -Algorithm SHA256).Hash;ExpectedGuideSha256=$packagePdfHash
} | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
$startInfo = New-Object Diagnostics.ProcessStartInfo
$startInfo.FileName = $devenv
$startInfo.Arguments = '/RootSuffix ' + $rootSuffix + ' /NoSplash /ResetSettings General /Log "' + (Join-Path $outputDir 'ActivityLog.xml') + '"'
$startInfo.UseShellExecute = $false
@{ HideWelcome=$true; FontSize=14; ThemeMode='dark' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir 'ui-settings.json') -Encoding UTF8
$startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$startInfo.EnvironmentVariables['CODEXCODE_IDE_TEST_CONFIG'] = $configPath
$startInfo.EnvironmentVariables['VS2017_AI_SETTINGS_PATH'] = Join-Path $outputDir 'ui-settings.json'
$startInfo.EnvironmentVariables['VS2017_AI_WORKSPACE_PATH'] = Join-Path $outputDir 'workspace'
$startInfo.EnvironmentVariables['VS2017_AI_DISABLE_RUNTIME_DISCOVERY'] = '1'
$ideProcess = [Diagnostics.Process]::Start($startInfo)
[pscustomobject]@{
    ProcessId=$ideProcess.Id; RootSuffix=$rootSuffix; OutputDirectory=$outputDir; VsixPath=$VsixPath
    VsixSha256=(Get-FileHash -LiteralPath $VsixPath -Algorithm SHA256).Hash
    ReferenceDllSha256=(Get-FileHash -LiteralPath $referenceDll -Algorithm SHA256).Hash
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
if ($result.Succeeded) { Write-Output "실제 IDE 통합 테스트 PASS: $($result.Passed)/$($result.Passed + $result.Failed)" }
# 결과를 기록한 검사 전용 IDE만 정리한다. VS2022의 늦은 종료가 부모의 출력 파이프를 붙잡지 않게 한다.
# update date  : 2026-10-19 CodexOrig : 전용 RootSuffix와 실행 PID를 함께 대조하고 정상/강제 종료를 별도 기록.
$exitedNormally = $ideProcess.WaitForExit(10000)
$forcedCleanup = $false
if (!$exitedNormally) {
    $ownedProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$($ideProcess.Id)"
    if (!$ownedProcess -or !$ownedProcess.CommandLine.Contains('/RootSuffix ' + $rootSuffix + ' ')) {
        throw '검사 전용 IDE의 identity를 확인하지 못해 자동 종료하지 않았습니다.'
    }
    $ideProcess.Kill()
    if (!$ideProcess.WaitForExit(5000)) { throw '검사 전용 IDE 종료를 확인하지 못했습니다.' }
    $forcedCleanup = $true
}
[pscustomobject]@{
    ProcessId=$ideProcess.Id; RootSuffix=$rootSuffix; ResultPreserved=$true
    ExitedNormally=$exitedNormally; ForcedOwnedProcessCleanup=$forcedCleanup; CheckedAt=[DateTimeOffset]::Now.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir 'owned-process-cleanup.json') -Encoding UTF8
if (!$result.Succeeded) { throw "실제 IDE 통합 테스트 실패. $resultPath" }
