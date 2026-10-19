#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ProgressPresentation
desc         : 고정 제품 DLL 또는 명시적 source host로 진행 모델·화면 없는 WPF·실제 helper를 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 제품 사본·해시·설정 격리와 SourceOnly 구분, 실제 창 표시 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory,[switch]$VerifyHover)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task022/progress-'+[guid]::NewGuid().ToString('N').Substring(0,10))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){if(@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw '검사 결과 디렉터리는 새 경로 또는 빈 경로여야 합니다.'}}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions')){Add-Type -AssemblyName $framework}
$sourceOnly=[String]::IsNullOrWhiteSpace($AssemblyPath)
if($VerifyHover -and $sourceOnly){throw '선택적 실제 hover 검사는 고정 제품 -AssemblyPath가 필요합니다.'}
$inputs=@()
if($sourceOnly){
    $productPath=Join-Path $OutputDirectory 'ProgressProductionSourceWithHostFixture.dll'
    $compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
    $referenceDir='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
    $compilerArgs=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$productPath),('/lib:'+$referenceDir),'/reference:mscorlib.dll','/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:WindowsBase.dll','/reference:PresentationCore.dll','/reference:PresentationFramework.dll','/reference:System.Xaml.dll')
    foreach($inputPath in @((Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/ProgressNotice.cs'),(Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/AssistantControl.ProgressPresentation.cs'),(Join-Path $PSScriptRoot 'ProgressControlFixture.cs'))){$compilerArgs+=$inputPath;$inputs+=Get-FileHash -Algorithm SHA256 -LiteralPath $inputPath | Select-Object Path,Hash}
    & $compiler @compilerArgs
    if($LASTEXITCODE -ne 0){throw '진행 표시 source-only 독립 컴파일 실패'}
}else{
    $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
    $productPath=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'
    Copy-Item -LiteralPath $AssemblyPath -Destination $productPath
    if((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $productPath -Algorithm SHA256).Hash){throw '고정 DLL 사본 해시 불일치'}
}
$assemblyHash=(Get-FileHash -LiteralPath $productPath -Algorithm SHA256).Hash
$referencePaths=@{}
if(!$sourceOnly){
    $assets=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repositoryDir 'src/VS2017.AIAssistant/obj/project.assets.json') -Raw | ConvertFrom-Json
    $packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    foreach($entry in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$referencePaths[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}}}
}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;if($referencePaths.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($referencePaths[$name])};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'isolated-settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'isolated-workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"FontSize":14}',(New-Object Text.UTF8Encoding($true)))
    [Reflection.Assembly]::LoadFrom($productPath) | Out-Null
    if($referencePaths.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){[Reflection.Assembly]::LoadFrom($referencePaths['Microsoft.VisualStudio.Shell.15.0']) | Out-Null}
    $refs=@($productPath,'System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    $checksPath=Join-Path $PSScriptRoot 'ProgressPresentationChecks.cs'
    Add-Type -Path $checksPath -ReferencedAssemblies $refs
    [ProgressPresentationChecks]::Run()
    $allChecks=@([ProgressPresentationChecks]::Checks);$allErrors=@([ProgressPresentationChecks]::Errors)
    if($VerifyHover){
        Add-Type -Path (Join-Path $PSScriptRoot 'ProgressHoverChecks.cs') -ReferencedAssemblies $refs
        try{[ProgressHoverChecks]::Run()}catch{[ProgressHoverChecks]::Errors.Add('Hover could not complete: '+$_.Exception.Message)}
        $allChecks+=@([ProgressHoverChecks]::Checks);$allErrors+=@([ProgressHoverChecks]::Errors)
    }
    $result=[pscustomobject]@{Succeeded=$allErrors.Count -eq 0;Passed=$allChecks.Count;Failed=$allErrors.Count;Checks=$allChecks;Errors=$allErrors;SourceOnly=$sourceOnly;Mode=$(if($sourceOnly){'ProductionProgressSourceWithExplicitChatDtoHostFixture'}else{'ActualProductAssembly'});AssemblyPath=$productPath;AssemblySha256=$assemblyHash;ProductAssemblySha256=$(if($sourceOnly){$null}else{$assemblyHash});Inputs=$inputs;VerifyHoverRequested=[bool]$VerifyHover;WindowShown=$(if($VerifyHover){[ProgressHoverChecks]::WindowShown}else{$false});NativePointerUsed=$(if($VerifyHover){[ProgressHoverChecks]::NativePointerUsed}else{$false});HoverExecuted=$(if($VerifyHover){[ProgressHoverChecks]::HoverExecuted}else{$false});NetworkRequested=$false;PersonalCredentialsRead=$false;NativeExecutionPerformed=$false;CheckedAt=[DateTimeOffset]::Now.ToString('O')}
    $result | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $result | ConvertTo-Json -Depth 7
    if(!$result.Succeeded){throw "진행 표시 검사 실패: $OutputDirectory"}
}finally{$env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
