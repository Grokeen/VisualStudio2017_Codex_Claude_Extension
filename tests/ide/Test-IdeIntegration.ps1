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
               2026-11-03 CodexOrig 보조 개발 055 : 격리 worktree 전용 TASK073 phase·고정 소스 사본·실행 전 seed 해시를 연결.
               2026-11-03 CodexOrig : 실제 CUA 검사가 필요한 exercise 인스턴스는 표시하고 restore는 숨김 경계를 유지.
-------------------------------------------------------------------------
.SYNOPSIS
    Runs the installed product in a dedicated VS2017 root suffix. Live Codex is opt-in.+#>
[CmdletBinding()]
param(
    [string]$VisualStudioPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community',
    [string]$VsixPath,
    [string]$FixturePath,
    [string]$LiveCodexPath,
    [string]$OutputDirectory,
    [ValidateSet('exercise','restore')][string]$ActivityFeedPhase,
    [string]$ActivityFeedFixturePath,
    [string]$ActivityFeedFixtureSHA256,
    [string]$ActivityFeedSnapshotManifestPath,
    [string]$ActivityFeedSnapshotManifestSHA256,
    [string]$ActivityFeedAddonSourceDirectory,
    [string]$WorkspaceSeedPath,
    [ValidateRange(30,600)][int]$TimeoutSeconds = 240
)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$VsixPath) { $VsixPath = Join-Path $repositoryDir 'src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.vsix' }
$VsixPath = (Resolve-Path -LiteralPath $VsixPath).Path
if ($ActivityFeedPhase -and $LiveCodexPath) { throw 'TASK073 uses only fixed local fixtures, never live AI.' }
if ($ActivityFeedPhase -ne 'restore') {
if (!$FixturePath) {
    $FixturePath = Get-ChildItem -LiteralPath (Join-Path $repositoryDir 'artifacts\source-editing') -Filter 'run-*' -Directory |
        Sort-Object LastWriteTime -Descending | ForEach-Object { Join-Path $_.FullName 'Fixture CLI 한글.exe' } |
        Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (!$FixturePath) { throw 'tests/editing/Test-SourceEditing.ps1을 먼저 실행하거나 -FixturePath를 지정하세요.' }
$FixturePath = (Resolve-Path -LiteralPath $FixturePath).Path
if($ActivityFeedPhase -eq 'exercise' -and (!$FixturePath.Contains([IO.Path]::DirectorySeparatorChar+'artifacts'+[IO.Path]::DirectorySeparatorChar) -or [IO.Path]::GetFileName($FixturePath).IndexOf('fixture',[StringComparison]::OrdinalIgnoreCase) -lt 0)){throw 'TASK073 main requests require an artifact fixture, never an installed AI executable.'}
}
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
if ($OutputDirectory) {
    $outputDir=[IO.Path]::GetFullPath($OutputDirectory)
    $allowedIdeRoots=@((Join-Path $repositoryDir 'artifacts/ide'))
    if((Split-Path $repositoryDir -Leaf) -eq 'source-isolated-1'){$allowedIdeRoots+=Join-Path ([IO.Path]::GetFullPath((Join-Path $repositoryDir '../../..'))) 'artifacts/ide'}
    if (@($allowedIdeRoots|Where-Object{$outputDir.StartsWith([IO.Path]::GetFullPath($_)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)}).Count -eq 0 -or (Test-Path -LiteralPath $outputDir)) { throw 'TASK073 explicit output must be a new owned IDE nonce path.' }
    for($part=$outputDir;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'IDE output links are forbidden.'}}
}
$null = New-Item -ItemType Directory -Path $outputDir -Force
$task073FixedInputs=@()
$activityFixtureHash=$ActivityFeedFixtureSHA256
if ($ActivityFeedPhase) {
    $task073VsixHash=(Get-FileHash -LiteralPath $VsixPath).Hash
    if (!$ActivityFeedAddonSourceDirectory) { $ActivityFeedAddonSourceDirectory=Join-Path $PSScriptRoot '../task073-runtime' }
    $addonCopies=Join-Path $outputDir 'Task073SourceCopies';$null=New-Item -ItemType Directory -Path $addonCopies
    foreach($name in @('ActivityFeedRuntime73.cs','ActivityFeedRuntimeProbe73.cs','ActivityFeedSnapshot73.cs','ShutdownObservation73.cs','ActivityFeedProtocolFixture73.cs')) {
        $source=(Get-Item -LiteralPath (Join-Path $ActivityFeedAddonSourceDirectory $name)).FullName
        $copy=Join-Path $addonCopies $name;$hash=(Get-FileHash -LiteralPath $source).Hash;Copy-Item -LiteralPath $source -Destination $copy
        $after=(Get-FileHash -LiteralPath $source).Hash;$copied=(Get-FileHash -LiteralPath $copy).Hash
        if($after -ne $hash -or $copied -ne $hash){throw 'TASK073 addon original-before/after or copied SHA differs.'}
        $task073FixedInputs+=[pscustomobject]@{Path=$source;Copy=$copy;SHA256=$hash;OriginalSHA256BeforeCopy=$hash;OriginalSHA256AfterCopy=$after;CopiedSHA256=$copied}
    }
}
if ($ActivityFeedPhase -eq 'restore') {
    if (!$WorkspaceSeedPath -or !$ActivityFeedSnapshotManifestPath -or !$ActivityFeedSnapshotManifestSHA256) { throw 'Restore requires exact snapshot seed and manifest hash.' }
    $ActivityFeedSnapshotManifestPath=[IO.Path]::GetFullPath($ActivityFeedSnapshotManifestPath)
    if(!$ActivityFeedSnapshotManifestPath.Contains([IO.Path]::DirectorySeparatorChar+'artifacts'+[IO.Path]::DirectorySeparatorChar+'ide'+[IO.Path]::DirectorySeparatorChar) -or !$ActivityFeedSnapshotManifestPath.Contains('task073'+[IO.Path]::DirectorySeparatorChar+'runtime'+[IO.Path]::DirectorySeparatorChar)){throw 'Only an owned actual IDE snapshot may be restored.'}
    for($part=$ActivityFeedSnapshotManifestPath;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Restore manifest links are forbidden.'}}
    if ((Get-FileHash -LiteralPath $ActivityFeedSnapshotManifestPath).Hash -ne $ActivityFeedSnapshotManifestSHA256) { throw 'Restore manifest hash differs.' }
    $snapshot=Get-Content -LiteralPath $ActivityFeedSnapshotManifestPath -Raw -Encoding UTF8|ConvertFrom-Json
    if(!$snapshot.SourceStableDuringCapture -or $snapshot.StateMetadataEdited -or @($snapshot.Files).Count -gt 10000){throw 'Snapshot provenance or capacity differs.'}
    if ([IO.Path]::GetFullPath($WorkspaceSeedPath) -ne [IO.Path]::GetFullPath($snapshot.DataRoot) -or [IO.Path]::GetFullPath($snapshot.DataRoot) -ne (Join-Path (Split-Path $ActivityFeedSnapshotManifestPath) 'data')) { throw 'Restore seed escaped manifest data root.' }
    $seedTarget=Join-Path $outputDir 'workspace';$null=New-Item -ItemType Directory -Path $seedTarget
    $seedProof=@();$seen=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase);$seedBytes=0L
    foreach($file in $snapshot.Files) {
        if([IO.Path]::IsPathRooted($file.RelativePath) -or $file.RelativePath -match '(^|[\\/])\.\.([\\/]|$)' -or [IO.Path]::GetExtension($file.RelativePath) -notin @('.md','.json')){throw 'Invalid snapshot whitelist reference.'}
        $source=[IO.Path]::GetFullPath((Join-Path $snapshot.DataRoot $file.RelativePath));$target=[IO.Path]::GetFullPath((Join-Path $seedTarget $file.RelativePath))
        if(!$source.StartsWith([IO.Path]::GetFullPath($snapshot.DataRoot)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or !$target.StartsWith($seedTarget+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or !$seen.Add($source)){throw 'Duplicate or escaped seed reference.'}
        for($part=$source;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Restore seed links are forbidden.'}}
        $seedBytes+=(Get-Item -LiteralPath $source).Length;if((Get-Item -LiteralPath $source).Length -gt 4194304 -or $seedBytes -gt 33554432){throw 'Snapshot capacity exceeded.'}
        if((Get-FileHash -LiteralPath $source).Hash -ne $file.SHA256){throw 'Restore seed source changed.'}
        $null=New-Item -ItemType Directory -Path (Split-Path $target) -Force;Copy-Item -LiteralPath $source -Destination $target
        $actual=(Get-FileHash -LiteralPath $target).Hash;if($actual -ne $file.SHA256){throw 'Restore target copy differs.'}
        $seedProof+=[pscustomobject]@{RelativePath=$file.RelativePath;SHA256=$actual}
    }
    $stack=New-Object 'Collections.Generic.Stack[string]';$stack.Push([IO.Path]::GetFullPath($snapshot.DataRoot));$fileCount=0
    while($stack.Count -gt 0){foreach($entry in Get-ChildItem -LiteralPath ($stack.Pop()) -Force){if($entry.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Restore source contains a link.'};if($entry.PSIsContainer){$stack.Push($entry.FullName)}else{$fileCount++;if(!$seen.Contains($entry.FullName)){throw 'Unreferenced snapshot file.'}}}}
    if($fileCount -ne $seen.Count -or (Get-FileHash -LiteralPath $ActivityFeedSnapshotManifestPath).Hash -ne $ActivityFeedSnapshotManifestSHA256){throw 'Snapshot inventory changed during seed copy.'}
    [pscustomobject]@{MatchesSnapshot=$true;SnapshotManifestSHA256=$ActivityFeedSnapshotManifestSHA256;Files=$seedProof;VerifiedBeforeIdeLaunch=$true;MetadataEdited=$false}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $outputDir 'seed-files-before-launch.json') -Encoding UTF8
}
$runtimeFixturePath=Join-Path $outputDir 'NativeProtocolFixture.exe'
$fixtureCompiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if ($ActivityFeedPhase -ne 'restore') {
& $fixtureCompiler /nologo /target:exe "/out:$runtimeFixturePath" /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot '..\task020\RuntimeProtocolFixture.cs')
if($LASTEXITCODE -ne 0){throw '실제 IDE용 네이티브 프로토콜 fixture 컴파일 실패.'}
}
if ($ActivityFeedPhase -eq 'exercise') {
    if (!$ActivityFeedFixturePath) {
        $ActivityFeedFixturePath=Join-Path $outputDir 'ActivityFeedProtocolFixture73.exe'
        & $fixtureCompiler /nologo /target:exe "/out:$ActivityFeedFixturePath" /reference:System.Web.Extensions.dll (Join-Path $addonCopies 'ActivityFeedProtocolFixture73.cs') *> (Join-Path $outputDir 'task073-fixture-build.log')
        if($LASTEXITCODE -ne 0){throw 'TASK073 local fixture compilation failed.'}
        $activityFixtureHash=(Get-FileHash -LiteralPath $ActivityFeedFixturePath).Hash
    } else {
        $ActivityFeedFixturePath=(Get-Item -LiteralPath $ActivityFeedFixturePath).FullName
        if(!$activityFixtureHash -or (Get-FileHash -LiteralPath $ActivityFeedFixturePath).Hash -ne $activityFixtureHash){throw 'Fixed TASK073 fixture hash differs.'}
    }
}
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
if ($ActivityFeedPhase) {
    [xml]$helperXml=Get-Content -LiteralPath $helperProject -Raw
    $ns=New-Object Xml.XmlNamespaceManager($helperXml.NameTable);$ns.AddNamespace('m',$helperXml.DocumentElement.NamespaceURI)
    $helperSources=Join-Path $outputDir 'Task073HelperSourceCopies';$null=New-Item -ItemType Directory -Path $helperSources
    foreach($compile in $helperXml.SelectNodes('//m:Compile[@Include]',$ns)){
        $source=$compile.Include;$name=[IO.Path]::GetFileName($source)
        if(Test-Path -LiteralPath (Join-Path $addonCopies $name)){$source=Join-Path $addonCopies $name}
        $copy=Join-Path $helperSources $name;if(Test-Path -LiteralPath $copy){throw 'Helper source basename collision.'};$hash=(Get-FileHash -LiteralPath $source).Hash;Copy-Item -LiteralPath $source -Destination $copy
        $after=(Get-FileHash -LiteralPath $source).Hash;$copied=(Get-FileHash -LiteralPath $copy).Hash
        if($after -ne $hash -or $copied -ne $hash){throw 'Helper original-before/after or copied SHA differs.'}
        # 2026-11-03 CodexOrig: generated Include is an XML attribute, set its string value explicitly.
        $task073FixedInputs+=[pscustomobject]@{Path=$source;Copy=$copy;SHA256=$hash;OriginalSHA256BeforeCopy=$hash;OriginalSHA256AfterCopy=$after;CopiedSHA256=$copied};$compile.SetAttribute('Include',[string]$copy)
    }
    $helperXml.Save($helperProject)
    $task073FixedInputs|ConvertTo-Json -Depth 5|Set-Content (Join-Path $outputDir 'task073-inputs-before.json') -Encoding UTF8
}
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
$debugProgram = @"
using System;
namespace IdeProbe
{
    internal static class DebugProgram
    {
        private static void Nested()
        {
            GC.KeepAlive(7);
        }




        public static void Main()
        {
            string privateValue = "SECRET_VALUE_SENTINEL";
            Nested();
            GC.KeepAlive(privateValue);
        }
    }
}
"@
[IO.File]::WriteAllText((Join-Path $outputDir 'DebugProgram.cs'), $debugProgram, $utf8Bom)
$projectText = @'
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup><Configuration>Debug</Configuration><Platform>AnyCPU</Platform><OutputType>Exe</OutputType><DebugSymbols>true</DebugSymbols><DebugType>full</DebugType><Optimize>false</Optimize><RootNamespace>IdeProbe</RootNamespace><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion><OutputPath>bin\Debug\</OutputPath><ProjectGuid>{18FF2748-6B77-4F67-AF48-CE2D75CE460C}</ProjectGuid></PropertyGroup>
  <ItemGroup><Reference Include="System"/><Compile Include="Probe.cs"/><Compile Include="Other.cs"/><Compile Include="DebugProgram.cs"/></ItemGroup>
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
    ActivityFeedPhase=$ActivityFeedPhase;ActivityFeedFixturePath=$ActivityFeedFixturePath;ActivityFeedFixtureSHA256=$activityFixtureHash
    ActivityFeedSnapshotManifestPath=$ActivityFeedSnapshotManifestPath;ActivityFeedSnapshotManifestSHA256=$ActivityFeedSnapshotManifestSHA256
} | ConvertTo-Json | Set-Content -LiteralPath $configPath -Encoding UTF8
$startInfo = New-Object Diagnostics.ProcessStartInfo
$startInfo.FileName = $devenv
$startInfo.Arguments = '/RootSuffix ' + $rootSuffix + ' /NoSplash /ResetSettings General /Log "' + (Join-Path $outputDir 'ActivityLog.xml') + '"'
$startInfo.UseShellExecute = $false
@{ HideWelcome=$true; FontSize=14; ThemeMode='dark' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputDir 'ui-settings.json') -Encoding UTF8
$startInfo.WindowStyle = if ($ActivityFeedPhase -eq 'exercise') { [Diagnostics.ProcessWindowStyle]::Normal } else { [Diagnostics.ProcessWindowStyle]::Hidden }
$startInfo.EnvironmentVariables['CODEXCODE_IDE_TEST_CONFIG'] = $configPath
$startInfo.EnvironmentVariables['VS2017_AI_SETTINGS_PATH'] = Join-Path $outputDir 'ui-settings.json'
$startInfo.EnvironmentVariables['VS2017_AI_WORKSPACE_PATH'] = Join-Path $outputDir 'workspace'
$startInfo.EnvironmentVariables['VS2017_AI_DISABLE_RUNTIME_DISCOVERY'] = '1'
if($ActivityFeedPhase){
    $prelaunchStable=@($task073FixedInputs|Where-Object{(Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 -or (Get-FileHash -LiteralPath $_.Copy).Hash -ne $_.SHA256}).Count -eq 0
    if(!$prelaunchStable -or (Get-FileHash -LiteralPath $VsixPath).Hash -ne $task073VsixHash -or (Get-FileHash -LiteralPath $referenceDll).Hash -ne $packageDllHash.Replace('-','')){throw 'TASK073 inputs changed before actual IDE launch.'}
    if($ActivityFeedPhase -eq 'exercise' -and (Get-FileHash -LiteralPath $ActivityFeedFixturePath).Hash -ne $activityFixtureHash){throw 'TASK073 fixture changed before actual launch.'}
    [pscustomobject]@{InputsUnchanged=$true;Sources=$task073FixedInputs;CheckedBeforeIdeLaunch=$true}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $outputDir 'task073-before-launch.json') -Encoding UTF8
}
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
if ($ActivityFeedPhase) {
    $stable=@($task073FixedInputs|Where-Object{(Get-FileHash -LiteralPath $_.Path).Hash -ne $_.SHA256 -or (Get-FileHash -LiteralPath $_.Copy).Hash -ne $_.SHA256}).Count -eq 0 -and (Get-FileHash -LiteralPath $VsixPath).Hash -eq $task073VsixHash
    [pscustomobject]@{InputsUnchanged=$stable;Files=$task073FixedInputs;CompileAndRuntimeScopesSeparate=$true}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $outputDir 'task073-inputs-after.json') -Encoding UTF8
    if(!$stable -or (Test-Path (Join-Path $outputDir 'task073-shutdown-error.json'))){throw 'TASK073 fixed input or shutdown verification failed; preserve results.'}
}
if (!$result.Succeeded) { throw "실제 IDE 통합 테스트 실패. $resultPath" }
