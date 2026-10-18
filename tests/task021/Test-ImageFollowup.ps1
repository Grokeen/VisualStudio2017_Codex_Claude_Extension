#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ImageFollowup
desc         : 고정 제품 DLL의 이미지 후속 요구를 실제 WPF에서 격리 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 런타임 탐색·SCM 차단, 폭별 PNG와 결과 JSON 보관.
               2026-10-18 CodexOrig : 격리 WPF 검사는 software 렌더로 고정해 비활성 desktop GPU 정지와 제품 시계를 분리.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory,[switch]$UseHardwareRendering)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -STA로 실행하세요.'}
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task021/image-followup-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions')){Add-Type -AssemblyName $framework}
# 이 검사 프로세스의 WPF 렌더만 고정한다. 실제 마우스·WebView/IDE 검사는 원래 렌더 경로를 별도로 사용한다.
if(!$UseHardwareRendering){[Windows.Media.RenderOptions]::ProcessRenderMode=[Windows.Interop.RenderMode]::SoftwareOnly}
$referencePaths=@{}
$assets=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repositoryDir 'src/VS2017.AIAssistant/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
foreach($entry in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$referencePaths[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}}}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;if($referencePaths.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($referencePaths[$name])};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'isolated-settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'isolated-workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [Reflection.Assembly]::LoadFrom($AssemblyPath) | Out-Null
    if($referencePaths.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){[Reflection.Assembly]::LoadFrom($referencePaths['Microsoft.VisualStudio.Shell.15.0']) | Out-Null}
    $refs=@($AssemblyPath,'System.dll','System.Core.dll','System.Web.Extensions.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'ImageFollowupChecks.cs') -ReferencedAssemblies $refs
    [ImageFollowupChecks]::Run($OutputDirectory)
    $result=[pscustomobject]@{Succeeded=[ImageFollowupChecks]::Errors.Count -eq 0;Passed=[ImageFollowupChecks]::Checks.Count;Failed=[ImageFollowupChecks]::Errors.Count;Checks=@([ImageFollowupChecks]::Checks);Errors=@([ImageFollowupChecks]::Errors);AssemblyPath=$AssemblyPath;AssemblySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $AssemblyPath).Hash;RenderMode=[Windows.Media.RenderOptions]::ProcessRenderMode.ToString();NetworkRequested=$false;PersonalCredentialsRead=$false;NativeExecutionPerformed=$false;CheckedAt=[DateTimeOffset]::Now.ToString('O')}
    $result | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'result.json')
    $result | ConvertTo-Json -Depth 6
    if(!$result.Succeeded){throw "이미지 후속 UI 검사 실패: $OutputDirectory"}
}finally{$env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
