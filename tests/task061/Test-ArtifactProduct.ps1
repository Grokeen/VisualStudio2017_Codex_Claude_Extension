#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ArtifactProduct
desc         : 고정 제품 DLL의 실제 WPF 대화 메뉴·파일 작성기 계약을 격리 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK061 실제 DLL 전용·합성 메시지·CLI 탐색/완료 알림 비활성·개인 evidence.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$AssetsPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Use Windows PowerShell 5.1 -STA.'}
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count){throw 'Preserve previous evidence; use a new empty output folder.'}
$null=New-Item -ItemType Directory -Path $destination -Force
$original=[IO.Path]::GetFullPath($AssemblyPath)
$exactAssets=[IO.Path]::GetFullPath($AssetsPath)
$productHash=(Get-FileHash -LiteralPath $original).Hash
$assetsHash=(Get-FileHash -LiteralPath $exactAssets).Hash
$fixture=Join-Path $PSScriptRoot 'ArtifactProductChecks.cs'
$fixtureHash=(Get-FileHash -LiteralPath $fixture).Hash
$copy=Join-Path $destination ([IO.Path]::GetFileName($original))
Copy-Item -LiteralPath $original -Destination $copy
if((Get-FileHash -LiteralPath $copy).Hash -ne $productHash){throw 'Product snapshot hash differs.'}
Copy-Item -LiteralPath $fixture -Destination (Join-Path $destination 'ArtifactProductChecks.cs')
$oldSettings=$env:VS2017_AI_SETTINGS_PATH
$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH
$oldDiscovery=$env:VS2017_AI_DISABLE_CLI_DISCOVERY
$oldRuntime=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
$resolve=$null
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $destination 'bootstrap-settings.json'
    $env:VS2017_AI_WORKSPACE_PATH=Join-Path $destination 'bootstrap-workspace'
    $env:VS2017_AI_DISABLE_CLI_DISCOVERY='1';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":false}',[Text.UTF8Encoding]::new($false))
    foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem','System.Xml')){Add-Type -AssemblyName $name}
    $assets=Get-Content -LiteralPath $exactAssets -Raw -Encoding UTF8 | ConvertFrom-Json
    $packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $target=$assets.targets.PSObject.Properties | Where-Object{$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'} | Select-Object -First 1
    $dependencies=@{}
    foreach($entry in $target.Value.PSObject.Properties){
        foreach($kind in @('compile','runtime')){
            if($entry.Value.PSObject.Properties.Name -contains $kind){
                foreach($part in $entry.Value.$kind.PSObject.Properties.Name){
                    if($part -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($part)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$part)}
                }
            }
        }
    }
    $productDirectory=Split-Path $original -Parent
    $resolve=[ResolveEventHandler]{
        param($sender,$eventArgs)
        $key=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
        $sibling=Join-Path $productDirectory ($key+'.dll')
        if(Test-Path -LiteralPath $sibling -PathType Leaf){return [Reflection.Assembly]::LoadFrom($sibling)}
        if($dependencies.ContainsKey($key) -and (Test-Path -LiteralPath $dependencies[$key] -PathType Leaf)){return [Reflection.Assembly]::LoadFrom($dependencies[$key])}
        return $null
    }
    [AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
    [Reflection.Assembly]::LoadFrom($copy) | Out-Null
    $references=@($copy,'System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $destination 'ArtifactProductChecks.cs') -ReferencedAssemblies $references
    [ArtifactProductChecks]::Run($destination)
    $result=[ordered]@{
        Succeeded=[ArtifactProductChecks]::Errors.Count -eq 0
        Passed=[ArtifactProductChecks]::Checks.Count
        Failed=[ArtifactProductChecks]::Errors.Count
        Checks=@([ArtifactProductChecks]::Checks)
        Errors=@([ArtifactProductChecks]::Errors)
        PaletteObservations=@([ArtifactProductChecks]::PaletteObservations)
        ProductAssembly=$original;ProductAssemblySha256=$productHash
        AssetsPath=$exactAssets;AssetsSha256=$assetsHash;FixtureSha256=$fixtureHash
        WindowsShown=[ArtifactProductChecks]::WindowsShown;MenusOpened=[ArtifactProductChecks]::MenusOpened
        ActualProductDllTested=$true;StubProductUsed=$false;SyntheticMessagesOnly=$true
        NativeSaveDialogShown=$false;NativeSaveDialogPhysicallyClicked=$false
        ProductCancelApiTested=$true;ProductApprovedWriterApiTested=$true
        ClipboardWritten=$false;LiveAiUsed=$false;ActualIdeTested=$false
        GlobalThemeOrNotificationSettingsChanged=$false
        CheckedAt=[DateTimeOffset]::Now.ToString('o')
    }
    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'result.json') -Encoding UTF8
    $result | ConvertTo-Json -Depth 8
    foreach($inputRecord in @(@{Path=$original;Hash=$productHash},@{Path=$exactAssets;Hash=$assetsHash},@{Path=$fixture;Hash=$fixtureHash})){
        if((Get-FileHash -LiteralPath $inputRecord.Path).Hash -ne $inputRecord.Hash){throw 'Fixed input changed during test; preserve evidence.'}
    }
    if(!$result.Succeeded){throw 'Actual product artifact checks failed; preserve original evidence.'}
}finally{
    if($resolve){[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
    $env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace
    $env:VS2017_AI_DISABLE_CLI_DISCOVERY=$oldDiscovery;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldRuntime
}
