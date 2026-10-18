#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : GuideReadability
desc         : 안내 소스 또는 명시한 제품 DLL의 고정 복귀·접이 설명·원문·설치 취소 구조를 격리 검사한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK-024 창 표시·메시지 루프·실제 CLI·설치·공유 빌드 없음.
               2026-10-19 CodexCode : 실제 제품의 Shell 참조 버전에 맞는 공용 SDK 참조 선택 사용.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory,[string]$CompilerPath='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$guideRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $guideRepository ('artifacts\task024\guide-'+[guid]::NewGuid().ToString('N').Substring(0,10))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){if(@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw '새 결과 경로 또는 빈 폴더를 지정하세요.'}}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$guideProductDirectory=Join-Path $OutputDirectory 'product';$null=New-Item -ItemType Directory -Path $guideProductDirectory
$guideMode='SourceOnlyMemoryStructure';$guideOriginalHash=$null;$guideCopiedInputs=@();$guideSourceHashes=@()
<#
-------------------------------------------------------------------------
name         : Copy-GuideTree
desc         : 고정 자산만 새 결과 폴더로 복사하고 재분석 지점과 과도한 파일을 거부한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK-024 원본 자산·배치·설명서는 읽기만 한다.
-------------------------------------------------------------------------
#>
function Copy-GuideTree([string]$From,[string]$To){
    if(!(Test-Path -LiteralPath $From)){return}
    $guideEntry=Get-Item -LiteralPath $From -Force
    if(($guideEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '고정 입력의 연결 파일·폴더를 복사하지 않습니다.'}
    if($guideEntry.PSIsContainer){
        $null=New-Item -ItemType Directory -Path $To -Force
        foreach($guideChild in Get-ChildItem -LiteralPath $From -Force){Copy-GuideTree $guideChild.FullName (Join-Path $To $guideChild.Name)}
    }else{
        if($guideEntry.Length -gt 16MB -or $script:guideCopiedInputs.Count -ge 200){throw '검사 입력 자산의 크기·개수 한도를 초과했습니다.'}
        Copy-Item -LiteralPath $From -Destination $To
        $guideHash=(Get-FileHash -LiteralPath $From -Algorithm SHA256).Hash
        if($guideHash -ne (Get-FileHash -LiteralPath $To -Algorithm SHA256).Hash){throw '고정 사본 해시가 다릅니다.'}
        $script:guideCopiedInputs+=[pscustomobject]@{Original=$From;Copy=$To;Sha256=$guideHash}
    }
}
foreach($guideFramework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Xml','System.Drawing')){Add-Type -AssemblyName $guideFramework}
$guideReferenceDirectory='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$guideFrameworkReferences=@('mscorlib','System','System.Core','System.Drawing','WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Xml') | ForEach-Object{Join-Path $guideReferenceDirectory ($_+'.dll')}
$guideWebViewDirectory=Join-Path $env:USERPROFILE '.nuget\packages\microsoft.web.webview2\1.0.4191.47\lib\net462'
$guideSources=@('AssistantDialogs.cs','AssistantHelpContent.cs','CliGuideDialog.cs','AssistantGuideDetails.cs','AssistantGuideMedia.cs') | ForEach-Object{Join-Path $guideRepository ('src\VS2017.AIAssistant\Views\'+$_)}
if(!$AssemblyPath){
    $guideProductPath=Join-Path $guideProductDirectory 'GuideSourceWithHostFixture.dll'
    $guideMarkdownSources=@('MarkdownAnimatedImage.cs','MarkdownRendering.cs','MarkdownRendering.Html.cs','MarkdownPreview.cs','MarkdownnCodeHighlighting.cs') | ForEach-Object{Join-Path $guideRepository ('src\VS2017.AIAssistant\Views\'+$_)}
    if(!($guideMarkdownSources | Where-Object{[IO.Path]::GetFileName($_) -eq 'MarkdownPreview.cs'})){throw '공용 MarkdownPreview 제품 소스가 준비되어야 source-only 검사가 가능합니다.'}
    $guideProductionSources=$guideSources+$guideMarkdownSources+@((Join-Path $guideRepository 'src\VS2017.AIAssistant\Services\CliGuideService.cs'),(Join-Path $PSScriptRoot 'GuideHostFixture.cs'))
    $guideArguments=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$guideProductPath))+@($guideFrameworkReferences | ForEach-Object{'/reference:'+$_})
    # update date  : 2026-10-19 CodexOrig : 미리보기 구문 강조기가 사용하는 동일 AvalonEdit 의존성만 격리 사본에 포함.
    $guideAvalon=Join-Path $env:USERPROFILE '.nuget/packages/avalonedit/6.3.1.120/lib/net462/ICSharpCode.AvalonEdit.dll'
    $guideArguments+=('/reference:'+$guideAvalon)
    Copy-GuideTree $guideAvalon (Join-Path $guideProductDirectory 'ICSharpCode.AvalonEdit.dll')
    foreach($guideName in @('Core','Wpf')){
        $guideDependency=Join-Path $guideWebViewDirectory ('Microsoft.Web.WebView2.'+$guideName+'.dll')
        $guideArguments+=('/reference:'+$guideDependency);Copy-GuideTree $guideDependency (Join-Path $guideProductDirectory ([IO.Path]::GetFileName($guideDependency)))
    }
    & $CompilerPath @guideArguments @guideProductionSources
    if($LASTEXITCODE -ne 0){throw '안내 소스 독립 컴파일 실패'}
    $guideSourceHashes=@(Get-FileHash -LiteralPath $guideProductionSources -Algorithm SHA256 | Select-Object Path,Hash)
    # 소스 전용 검사 자산이며 실제 제품 GIF·실제 재생 근거로 집계하지 않는다.
    $guideMediaDirectory=Join-Path $guideProductDirectory 'Resources\GuideMedia';$null=New-Item -ItemType Directory -Path $guideMediaDirectory -Force
    foreach($guideName in @('01-connect','02-request','03-review','04-guide','05-install')){[IO.File]::WriteAllBytes((Join-Path $guideMediaDirectory ($guideName+'.gif')),[Convert]::FromBase64String('R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7'))}
}else{
    $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
    if(((Get-Item -LiteralPath $AssemblyPath).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '고정 제품 DLL은 연결 파일일 수 없습니다.'}
    $guideOriginalHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    $guideProductPath=Join-Path $guideProductDirectory 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $AssemblyPath -Destination $guideProductPath
    $guideOriginalDirectory=Split-Path -Parent $AssemblyPath
    foreach($guideName in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','ICSharpCode.AvalonEdit.dll')){Copy-GuideTree (Join-Path $guideOriginalDirectory $guideName) (Join-Path $guideProductDirectory $guideName)}
    foreach($guideName in @('GuideMedia','CliGuide')){Copy-GuideTree (Join-Path $guideOriginalDirectory ('Resources\'+$guideName)) (Join-Path $guideProductDirectory ('Resources\'+$guideName))}
    if($guideOriginalHash -ne (Get-FileHash -LiteralPath $guideProductPath -Algorithm SHA256).Hash){throw '고정 제품 DLL 해시 불일치'}
    $guideMode='FrozenProductMemoryStructure'
}
$guideChecksPath=Join-Path $OutputDirectory 'GuideReadabilityChecks.dll'
$guideCheckSource=Join-Path $PSScriptRoot 'GuideReadabilityChecks.cs'
$guideCheckArguments=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$guideChecksPath))+@($guideFrameworkReferences | ForEach-Object{'/reference:'+$_})+@($guideCheckSource)
& $CompilerPath @guideCheckArguments
if($LASTEXITCODE -ne 0){throw '안내 회귀 검사 컴파일 실패'}
$guideFixtures=Join-Path $OutputDirectory 'fixtures';$guideCliAssets=Join-Path $guideFixtures 'Resources\CliGuide';$null=New-Item -ItemType Directory -Path $guideCliAssets -Force
[IO.File]::WriteAllText((Join-Path $guideCliAssets 'HowToInstallCLI.md'),"# fixture guide`r`n`r`n**보존할 원문**`r`n`r`n1. 설치`r`n2. 로그인`r`n`r`n[명시 선택 링크](https://example.invalid/guide)",(New-Object Text.UTF8Encoding($false)))
$null=New-Item -ItemType Directory -Path (Join-Path $guideFixtures 'plain')
$null=New-Item -ItemType Junction -Path (Join-Path $guideFixtures 'linked') -Target (Join-Path $guideFixtures 'plain')
$guidePackageReferences=@{}
if($AssemblyPath){
    . (Join-Path $PSScriptRoot 'Get-ProductSdkReferences.ps1')
    $guidePackageReferences=Get-Task024SdkReferences -RepositoryDirectory $guideRepository -ProductAssemblyPath $guideProductPath
}
$guideResolve=[ResolveEventHandler]{param($sender,$eventArgs)
    $guideName=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
    if($guideName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){return $null}
    $guideLocal=Join-Path $guideProductDirectory ($guideName+'.dll')
    if(Test-Path -LiteralPath $guideLocal){return [Reflection.Assembly]::LoadFrom($guideLocal)}
    if($guidePackageReferences.ContainsKey($guideName)){return [Reflection.Assembly]::LoadFrom($guidePackageReferences[$guideName])}
    return $null
}
$guideOldSettings=$env:VS2017_AI_SETTINGS_PATH;$guideOldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$guideOldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
[AppDomain]::CurrentDomain.add_AssemblyResolve($guideResolve)
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"FontSize":14}',(New-Object Text.UTF8Encoding($true)))
    $guideAssembly=[Reflection.Assembly]::LoadFrom($guideProductPath)
    if($guidePackageReferences.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){$null=[Reflection.Assembly]::LoadFrom($guidePackageReferences['Microsoft.VisualStudio.Shell.15.0'])}
    $null=[Reflection.Assembly]::LoadFrom($guideChecksPath)
    $guideResult=[CodexCode.Task024.GuideReadabilityChecks]::Run($guideAssembly,$guideFixtures)
    $guideReport=[ordered]@{Succeeded=$guideResult.Succeeded;Passed=$guideResult.Passed.Count;Failed=$guideResult.Failures.Count;Checks=$guideResult.Passed;Failures=$guideResult.Failures;Mode=$guideMode;ProductAssemblyPath=$AssemblyPath;ProductAssemblySha256=$guideOriginalHash;LoadedAssemblySha256=(Get-FileHash -LiteralPath $guideProductPath -Algorithm SHA256).Hash;SourceHashes=$guideSourceHashes;CopiedInputs=$guideCopiedInputs;WindowShown=$false;DispatcherLoopStarted=$false;GifPlaybackVerified=$false;BrowserStarted=$false;AiInvoked=$false;InstallerStarted=$false;SharedProductBuild=$false;CheckedAt=(Get-Date).ToString('O')}
    $guideReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $guideReport | ConvertTo-Json -Depth 8
    if(!$guideResult.Succeeded){exit 1}
}finally{
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($guideResolve)
    $env:VS2017_AI_SETTINGS_PATH=$guideOldSettings;$env:VS2017_AI_WORKSPACE_PATH=$guideOldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$guideOldDiscovery
}
exit 0
