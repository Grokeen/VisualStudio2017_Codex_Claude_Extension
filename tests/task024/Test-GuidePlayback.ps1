#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-GuidePlayback
desc         : 최종 제품·동봉 자산 사본을 고정하고 예약된 실제 GIF 표시 검사와 컴파일 전용을 구분한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK-024 CompileOnly는 창·자산 검증·제품 실행이 전혀 없다.
               2026-10-19 CodexCode : 실제 제품의 Shell 참조 버전에 맞는 공용 SDK 참조 선택 사용.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory,[switch]$CompileOnly,[string]$CompilerPath='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$playbackRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if($AssemblyPath.StartsWith('\\',[StringComparison]::Ordinal)){throw '로컬 고정 DLL을 지정하세요.'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $playbackRepository ('artifacts\task024\play-'+[guid]::NewGuid().ToString('N').Substring(0,10))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$playbackInputRoot=Split-Path -Parent $AssemblyPath
if($OutputDirectory.StartsWith($playbackInputRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw '입력 제품 폴더 밖의 새 결과 경로를 지정하세요.'}
if(Test-Path -LiteralPath $OutputDirectory){if(@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw '새 결과 경로 또는 빈 디렉터리를 지정하세요.'}}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$playbackInputs=New-Object 'Collections.Generic.List[object]'
$playbackDependencies=New-Object 'Collections.Generic.List[object]'
$script:playbackTotalBytes=0L
<#
-------------------------------------------------------------------------
name         : Assert-PlainPlaybackPath
desc         : 검사 입력 경로와 모든 상위 폴더의 재분석 지점을 거부한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK-024 고정 파일 대신 연결 경로를 따라가지 않음.
-------------------------------------------------------------------------
#>
function Assert-PlainPlaybackPath([string]$Path){
    for($playbackCurrent=$Path;![String]::IsNullOrEmpty($playbackCurrent);$playbackCurrent=[IO.Path]::GetDirectoryName($playbackCurrent)){
        $playbackItem=Get-Item -LiteralPath $playbackCurrent -Force
        if(($playbackItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '고정 입력의 재분석 경로를 허용하지 않습니다.'}
    }
}
<#
-------------------------------------------------------------------------
name         : Copy-FixedPlaybackTree
desc         : 지정된 제품 폴더 전체를 새 사본으로 복사하고 각 원본·사본 SHA를 대조한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK-024 2000파일·총512MiB·파일당64MiB를 넘으면 중단.
-------------------------------------------------------------------------
#>
function Copy-FixedPlaybackTree([string]$From,[string]$To){
    $playbackItem=Get-Item -LiteralPath $From -Force
    if(($playbackItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '연결된 제품 파일·폴더는 복사하지 않습니다.'}
    if($playbackItem.PSIsContainer){
        $null=New-Item -ItemType Directory -Path $To -Force
        foreach($playbackEntry in Get-ChildItem -LiteralPath $From -Force){Copy-FixedPlaybackTree $playbackEntry.FullName (Join-Path $To $playbackEntry.Name)}
    }else{
        $script:playbackTotalBytes+=$playbackItem.Length
        if($playbackInputs.Count -ge 2000 -or $playbackItem.Length -gt 64MB -or $script:playbackTotalBytes -gt 512MB){throw '고정 제품 사본의 크기·개수 제한을 넘었습니다.'}
        $playbackBefore=(Get-FileHash -LiteralPath $From -Algorithm SHA256).Hash
        Copy-Item -LiteralPath $From -Destination $To
        $playbackAfter=(Get-FileHash -LiteralPath $To -Algorithm SHA256).Hash
        if($playbackBefore -ne $playbackAfter){throw '제품 원본·사본 SHA가 다릅니다.'}
        $playbackInputs.Add([pscustomobject]@{Original=$From;Copy=$To;Sha256=$playbackBefore})
    }
}
Assert-PlainPlaybackPath $AssemblyPath
$playbackAssemblyHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
$playbackLoadedPath=$AssemblyPath
$playbackProductDirectory=$playbackInputRoot
$playbackMediaValidated=$false
if(!$CompileOnly){
    $playbackProductDirectory=Join-Path $OutputDirectory 'product'
    Copy-FixedPlaybackTree $playbackInputRoot $playbackProductDirectory
    $playbackLoadedPath=Join-Path $playbackProductDirectory ([IO.Path]::GetFileName($AssemblyPath))
    foreach($playbackName in @('01-connect','02-request','03-review','04-guide','05-install')){
        foreach($playbackExtension in @('.gif','.png')){
            $playbackAsset=Join-Path $playbackProductDirectory ('Resources\GuideMedia\'+$playbackName+$playbackExtension)
            if(!(Test-Path -LiteralPath $playbackAsset -PathType Leaf)){throw ('최종 제품에 동봉된 안내 자산이 필요합니다: '+$playbackName+$playbackExtension)}
        }
    }
    $playbackMediaValidated=$true
}
$playbackReferences='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$playbackHarness=Join-Path $OutputDirectory 'GuidePlaybackChecks.dll'
$playbackSource=Join-Path $PSScriptRoot 'GuidePlaybackChecks.cs'
$playbackCompilerArguments=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$playbackHarness),('/reference:'+$playbackLoadedPath))
foreach($playbackName in @('mscorlib','System','System.Core','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$playbackCompilerArguments+=('/reference:'+(Join-Path $playbackReferences ($playbackName+'.dll')))}
& $CompilerPath @playbackCompilerArguments $playbackSource 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
$playbackCompilerExit=$LASTEXITCODE
if($playbackCompilerExit -ne 0){throw '안내 재생 검사 컴파일이 실패했습니다.'}
if((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash -ne $playbackAssemblyHash){throw '메타데이터 DLL이 검사 준비 중 변경되었습니다.'}
if($CompileOnly){
    $playbackReport=[ordered]@{Succeeded=$true;Mode='MetadataCompileOnly';CompileOnly=$true;CompilerExitCode=$playbackCompilerExit;MetadataAssemblyPath=$AssemblyPath;MetadataAssemblySha256=$playbackAssemblyHash;HarnessSha256=(Get-FileHash -LiteralPath $playbackHarness -Algorithm SHA256).Hash;TestSourceSha256=(Get-FileHash -LiteralPath $playbackSource -Algorithm SHA256).Hash;ChecksExecuted=$false;GuideMediaValidated=$false;ProductBundleValidated=$false;WindowShown=$false;AnimationAdvanceVerified=$false;Network=$false;AuthenticationInvoked=$false;ClipboardChanged=$false;SharedProductBuild=$false}
    $playbackReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $playbackReport | ConvertTo-Json -Depth 5
    exit 0
}
foreach($playbackFramework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $playbackFramework}
. (Join-Path $PSScriptRoot 'Get-ProductSdkReferences.ps1')
$playbackPackageReferences=Get-Task024SdkReferences -RepositoryDirectory $playbackRepository -ProductAssemblyPath $playbackLoadedPath
$playbackDependencyRoot=Join-Path $OutputDirectory 'host-dependencies';$null=New-Item -ItemType Directory -Path $playbackDependencyRoot
$playbackResolve=[ResolveEventHandler]{param($sender,$eventArgs)
    $playbackName=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
    if($playbackName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){return $null}
    $playbackLocal=Join-Path $playbackProductDirectory ($playbackName+'.dll')
    if(Test-Path -LiteralPath $playbackLocal){return [Reflection.Assembly]::LoadFrom($playbackLocal)}
    if($playbackPackageReferences.ContainsKey($playbackName)){
        $playbackFrom=$playbackPackageReferences[$playbackName];Assert-PlainPlaybackPath $playbackFrom
        $playbackDependency=Join-Path $playbackDependencyRoot ($playbackName+'.dll')
        $playbackHash=(Get-FileHash -LiteralPath $playbackFrom -Algorithm SHA256).Hash
        if(!(Test-Path -LiteralPath $playbackDependency)){Copy-Item -LiteralPath $playbackFrom -Destination $playbackDependency}
        if((Get-FileHash -LiteralPath $playbackDependency -Algorithm SHA256).Hash -ne $playbackHash){throw '격리된 SDK 호스트 의존성 SHA가 다릅니다.'}
        $playbackDependencies.Add([pscustomobject]@{Original=$playbackFrom;Copy=$playbackDependency;Sha256=$playbackHash})
        return [Reflection.Assembly]::LoadFrom($playbackDependency)
    }
    return $null
}
$playbackOldSettings=$env:VS2017_AI_SETTINGS_PATH;$playbackOldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$playbackOldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
[AppDomain]::CurrentDomain.add_AssemblyResolve($playbackResolve)
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"FontSize":14,"ThemeMode":"dark"}',(New-Object Text.UTF8Encoding($true)))
    $playbackProduct=[Reflection.Assembly]::LoadFrom($playbackLoadedPath)
    if(![String]::Equals($playbackProduct.Location,$playbackLoadedPath,[StringComparison]::OrdinalIgnoreCase)){throw '이미 로드된 다른 제품 DLL과 충돌했습니다.'}
    $null=[Reflection.Assembly]::LoadFrom($playbackHarness)
    $playbackResult=[CodexCode.Task024.GuidePlaybackChecks]::Run($OutputDirectory)
    $playbackOriginalsUnchanged=$true
    foreach($playbackInput in $playbackInputs){if((Get-FileHash -LiteralPath $playbackInput.Original -Algorithm SHA256).Hash -ne $playbackInput.Sha256 -or (Get-FileHash -LiteralPath $playbackInput.Copy -Algorithm SHA256).Hash -ne $playbackInput.Sha256){$playbackOriginalsUnchanged=$false}}
    if(!$playbackOriginalsUnchanged){$playbackResult.Failures.Add('Original or copied fixed input changed during playback.')}
    $playbackReport=[ordered]@{Succeeded=$playbackResult.Succeeded;Mode='FrozenProductActualWpfPlayback';CompileOnly=$false;Passed=$playbackResult.Passed.Count;Failed=$playbackResult.Failures.Count;Checks=$playbackResult.Passed;Failures=$playbackResult.Failures;Skipped=$playbackResult.Skipped;Observations=$playbackResult.Observations;AssemblyPath=$AssemblyPath;ProductAssemblySha256=$playbackAssemblyHash;CopiedAssemblySha256=(Get-FileHash -LiteralPath $playbackLoadedPath -Algorithm SHA256).Hash;GuideMediaValidated=$playbackMediaValidated;FixedInputCopies=$playbackInputs;FixedInputsUnchanged=$playbackOriginalsUnchanged;HostDependencies=$playbackDependencies;WindowShown=$playbackResult.WindowShown;DispatcherLoopStarted=$playbackResult.WindowShown;ClientAreaAnimation=$playbackResult.AnimationAllowed;AnimationAdvanceVerified=$playbackResult.AnimationAdvanceVerified;OsSettingsChanged=$false;NativePointerUsed=$false;Network=$false;AuthenticationInvoked=$false;InstallerStarted=$false;ClipboardChanged=$false;SharedProductBuild=$false;CheckedAt=(Get-Date).ToString('O')}
    $playbackReport | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    [pscustomobject]@{Succeeded=$playbackResult.Succeeded;Passed=$playbackResult.Passed.Count;Failed=$playbackResult.Failures.Count;ClientAreaAnimation=$playbackResult.AnimationAllowed;AnimationAdvanceVerified=$playbackResult.AnimationAdvanceVerified;ResultPath=(Join-Path $OutputDirectory 'result.json')} | ConvertTo-Json
    if(!$playbackResult.Succeeded){exit 1}
}finally{
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($playbackResolve)
    $env:VS2017_AI_SETTINGS_PATH=$playbackOldSettings;$env:VS2017_AI_WORKSPACE_PATH=$playbackOldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$playbackOldDiscovery
}
exit 0
