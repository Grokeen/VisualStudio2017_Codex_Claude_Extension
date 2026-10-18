#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-HoverViewport
desc         : 고정 DLL 또는 명시적 source fixture의 변경 Dropdown·휠·목록 overlay를 창 없이 검사한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK024 실제 제품/소스 결과 분리, 네트워크·실계정·GUI 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task024/hover-viewport-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -gt 0){throw '기존 증거를 보존합니다. 새 출력 폴더를 지정하세요.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions')){Add-Type -AssemblyName $framework}
$sourceOnly=[String]::IsNullOrWhiteSpace($AssemblyPath)
$inputs=@()
if($sourceOnly){
    $productPath=Join-Path $OutputDirectory 'SourceOnlyHoverViewport.dll'
    $compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
    $referenceDir='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
    $compilerArgs=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$productPath),('/lib:'+$referenceDir),'/reference:mscorlib.dll','/reference:System.dll','/reference:System.Core.dll','/reference:WindowsBase.dll','/reference:PresentationCore.dll','/reference:PresentationFramework.dll','/reference:System.Xaml.dll')
    foreach($name in @('ProgressNotice.cs','AssistantControl.Viewport.cs','AssistantControl.Thinking.cs','AssistantControl.HistoryOverlay.cs','DiffPreviewPresenter.cs','ChangeDiffPopup.cs','AssistantPalette.cs','AssistantThemePolicy.cs')){
        $inputPath=Join-Path $repositoryDir ('src/VS2017.AIAssistant/Views/'+$name);$compilerArgs+=$inputPath;$inputs+=Get-FileHash -LiteralPath $inputPath -Algorithm SHA256 | Select-Object Path,Hash
    }
    $compilerArgs+=Join-Path $PSScriptRoot 'ViewportOverlayFixture.cs'
    & $compiler @compilerArgs
    if($LASTEXITCODE -ne 0){throw 'Source-only 독립 컴파일 실패'}
}else{
    $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
    $productPath=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $AssemblyPath -Destination $productPath
    if((Get-FileHash -LiteralPath $AssemblyPath).Hash -ne (Get-FileHash -LiteralPath $productPath).Hash){throw '제품 DLL 사본 해시 불일치'}
}
$referencePaths=@{}
if(!$sourceOnly){
    . (Join-Path $PSScriptRoot 'Get-ProductSdkReferences.ps1')
    $referencePaths=Get-Task024SdkReferences -RepositoryDirectory $repositoryDir -ProductAssemblyPath $productPath
}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;if($referencePaths.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($referencePaths[$name])};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [Reflection.Assembly]::LoadFrom($productPath)|Out-Null
    $refs=@($productPath,'System.dll','System.Core.dll','System.Web.Extensions.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'HoverViewportChecks.cs') -ReferencedAssemblies $refs
    [HoverViewportChecks]::Run()
    $hash=(Get-FileHash -LiteralPath $productPath -Algorithm SHA256).Hash
    $result=[pscustomobject]@{Succeeded=[HoverViewportChecks]::Errors.Count -eq 0;Passed=[HoverViewportChecks]::Checks.Count;Failed=[HoverViewportChecks]::Errors.Count;Checks=@([HoverViewportChecks]::Checks);Errors=@([HoverViewportChecks]::Errors);SourceOnly=$sourceOnly;AssemblySha256=$hash;ProductAssemblySha256=$(if($sourceOnly){$null}else{$hash});SourceInputs=$inputs;WindowShown=$false;NativePointerUsed=$false;AiInvoked=$false;NetworkRequested=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
    $result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $result|ConvertTo-Json -Depth 5
    if(!$result.Succeeded){throw 'Hover/Viewport source or actual product checks failed; original results preserved.'}
}finally{$env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
