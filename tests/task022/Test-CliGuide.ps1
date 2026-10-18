#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-CliGuide
desc         : 실제 설치 없이 CLI 안내 서비스·승인 경계·문서·실제 DLL 또는 source-only host를 격리 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 고정 제품·동봉 의존성/문서 사본과 해시 보존, 선택 WebView 검사는 별도 예약.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory,[Alias('VerifyWebViewRender')][switch]$VerifyWebView)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task022/cli-guide-'+[guid]::NewGuid().ToString('N').Substring(0,10))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){if(@(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw '새 결과 경로 또는 빈 디렉터리를 지정하세요.'}}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$sourceOnly=[String]::IsNullOrWhiteSpace($AssemblyPath)
if($VerifyWebView -and $sourceOnly){throw '실제 WebView 선택 검사는 고정 제품 -AssemblyPath가 필요합니다.'}
$productDirectory=Join-Path $OutputDirectory 'product';New-Item -ItemType Directory -Path $productDirectory | Out-Null
$inputs=@();$copiedInputs=@()
<#
name         : Copy-CliGuideTree
desc         : 연결 폴더를 따라가지 않고 고정 의존성·문서 트리의 상대 경로와 실제 해시를 보존한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 원본은 읽기 전용, 새 product 사본에만 기록.
#>
function Copy-CliGuideTree([string]$From,[string]$To){
    if(!(Test-Path -LiteralPath $From -PathType Container)){return}
    $rootItem=Get-Item -LiteralPath $From
    if(($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '고정 자산의 연결 폴더는 복사하지 않습니다.'}
    New-Item -ItemType Directory -Path $To -Force | Out-Null
    foreach($entry in Get-ChildItem -LiteralPath $From -Force){
        if(($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '고정 자산의 연결 요소는 복사하지 않습니다.'}
        $target=Join-Path $To $entry.Name
        if($entry.PSIsContainer){Copy-CliGuideTree $entry.FullName $target}
        else{Copy-Item -LiteralPath $entry.FullName -Destination $target;$originalHash=(Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash;$copyHash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash;if($originalHash -ne $copyHash){throw '고정 자산 사본 해시가 다릅니다.'};$script:copiedInputs+=[pscustomobject]@{Original=$entry.FullName;Copy=$target;Sha256=$copyHash}}
    }
}
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Drawing','System.Web.Extensions')){Add-Type -AssemblyName $framework}
$webViewRoot=Join-Path $env:USERPROFILE '.nuget/packages/microsoft.web.webview2/1.0.4191.47/lib/net462'
if($sourceOnly){
    $productPath=Join-Path $productDirectory 'CliGuideProductionSourceWithHostFixture.dll'
    $compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
    $referenceDir='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
    $compilerArgs=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$productPath),('/lib:'+$referenceDir))
    foreach($name in @('mscorlib','System','System.Core','System.Drawing','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$compilerArgs+=('/reference:'+$name+'.dll')}
    foreach($name in @('Core','Wpf')){$dependency=Join-Path $webViewRoot ('Microsoft.Web.WebView2.'+$name+'.dll');$compilerArgs+=('/reference:'+$dependency);Copy-Item -LiteralPath $dependency -Destination $productDirectory}
    foreach($path in @((Join-Path $repositoryDir 'src/VS2017.AIAssistant/Services/CliGuideService.cs'),(Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/CliGuideDialog.cs'),(Join-Path $PSScriptRoot 'CliGuideHostFixture.cs'))){$compilerArgs+=$path;$inputs+=Get-FileHash -LiteralPath $path -Algorithm SHA256 | Select-Object Path,Hash}
    & $compiler @compilerArgs
    if($LASTEXITCODE -ne 0){throw 'CLI 안내 독립 source-only 컴파일 실패'}
}else{
    $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
    $productPath=Join-Path $productDirectory 'VS2017.AIAssistant.dll'
    Copy-Item -LiteralPath $AssemblyPath -Destination $productPath
    $sourceDirectory=Split-Path -Parent $AssemblyPath
    foreach($name in @('Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.Wpf.dll','WebView2Loader.dll')){
        $dependency=Join-Path $sourceDirectory $name
        if(Test-Path -LiteralPath $dependency){
            if(((Get-Item -LiteralPath $dependency).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '고정 의존성의 연결 파일은 복사하지 않습니다.'}
            $target=Join-Path $productDirectory $name;Copy-Item -LiteralPath $dependency -Destination $target
            $originalHash=(Get-FileHash -LiteralPath $dependency -Algorithm SHA256).Hash;$copyHash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
            if($originalHash -ne $copyHash){throw '고정 의존성 사본 해시 불일치'}
            $copiedInputs+=[pscustomobject]@{Original=$dependency;Copy=$target;Sha256=$copyHash}
        }
    }
    foreach($name in @('Resources','runtimes','x86','x64','arm64')){Copy-CliGuideTree (Join-Path $sourceDirectory $name) (Join-Path $productDirectory $name)}
    if((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $productPath -Algorithm SHA256).Hash){throw '고정 제품 DLL 사본 해시 불일치'}
}
$assemblyHash=(Get-FileHash -LiteralPath $productPath -Algorithm SHA256).Hash
$fixtureDirectory=Join-Path $OutputDirectory 'fixtures';$goodResources=Join-Path $fixtureDirectory 'valid/Resources'
New-Item -ItemType Directory -Path $goodResources -Force | Out-Null
$junctionRoot=Join-Path $fixtureDirectory 'junction-install';New-Item -ItemType Directory -Path $junctionRoot -Force | Out-Null
New-Item -ItemType Junction -Path (Join-Path $junctionRoot 'Resources') -Target $goodResources | Out-Null
$referencePaths=@{}
if(!$sourceOnly){
    $assets=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repositoryDir 'src/VS2017.AIAssistant/obj/project.assets.json') -Raw | ConvertFrom-Json
    $packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    foreach($entry in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$referencePaths[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}}}
}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;$local=Join-Path $productDirectory ($name+'.dll');if(Test-Path -LiteralPath $local){return [Reflection.Assembly]::LoadFrom($local)};if($referencePaths.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($referencePaths[$name])};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'isolated-settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'isolated-workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"FontSize":14}',(New-Object Text.UTF8Encoding($true)))
    [Reflection.Assembly]::LoadFrom($productPath) | Out-Null
    foreach($name in @('Core','Wpf')){[Reflection.Assembly]::LoadFrom((Join-Path $productDirectory ('Microsoft.Web.WebView2.'+$name+'.dll'))) | Out-Null}
    if($referencePaths.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){[Reflection.Assembly]::LoadFrom($referencePaths['Microsoft.VisualStudio.Shell.15.0']) | Out-Null}
    $refs=@($productPath,'System.dll','System.Core.dll','System.Drawing.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    foreach($name in @('Core','Wpf')){$refs+=Join-Path $productDirectory ('Microsoft.Web.WebView2.'+$name+'.dll')}
    Add-Type -Path (Join-Path $PSScriptRoot 'CliGuideChecks.cs') -ReferencedAssemblies $refs
    [CliGuideChecks]::Run($fixtureDirectory,(Join-Path $repositoryDir 'InstallCLI.bat'),$productDirectory,$sourceOnly)
    $allChecks=@([CliGuideChecks]::Checks);$allErrors=@([CliGuideChecks]::Errors);$optionalWebView=$null
    if($VerifyWebView){
        Add-Type -Path (Join-Path $PSScriptRoot 'CliGuideWebViewChecks.cs') -ReferencedAssemblies $refs
        try{[CliGuideWebViewChecks]::Run($productDirectory,$OutputDirectory)}catch{[CliGuideWebViewChecks]::Errors.Add('WebView could not complete: '+$_.Exception.Message)}
        $optionalWebView=[pscustomobject]@{Passed=[CliGuideWebViewChecks]::Checks.Count;Failed=[CliGuideWebViewChecks]::Errors.Count;Checks=@([CliGuideWebViewChecks]::Checks);Errors=@([CliGuideWebViewChecks]::Errors);RuntimeVersion=[CliGuideWebViewChecks]::RuntimeVersion;Source=[CliGuideWebViewChecks]::Source;ScreenshotPath=[CliGuideWebViewChecks]::ScreenshotPath;WindowShown=$true;InstallerExecuted=$false}
        $allChecks+=@([CliGuideWebViewChecks]::Checks);$allErrors+=@([CliGuideWebViewChecks]::Errors)
    }
    $result=[pscustomobject]@{Succeeded=$allErrors.Count -eq 0;Passed=$allChecks.Count;Failed=$allErrors.Count;Checks=$allChecks;Errors=$allErrors;SourceOnly=$sourceOnly;Mode=$(if($sourceOnly){'ProductionCliGuideSourceWithExplicitDialogHostFixture'}else{'ActualProductAssemblyAndPackagedAssets'});AssemblyPath=$productPath;AssemblySha256=$assemblyHash;ProductAssemblySha256=$(if($sourceOnly){$null}else{$assemblyHash});Inputs=$inputs;CopiedAssetInputs=$copiedInputs;InstallerLaunchSimulated=$true;InstallerExecutionPerformed=$false;WindowShown=[bool]$VerifyWebView;OptionalWebView=$optionalWebView;NetworkRequested=$false;PersonalCredentialsRead=$false;CheckedAt=[DateTimeOffset]::Now.ToString('O')}
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $result | ConvertTo-Json -Depth 8
    if(!$result.Succeeded){throw "CLI 안내 검사 실패: $OutputDirectory"}
}finally{$env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
