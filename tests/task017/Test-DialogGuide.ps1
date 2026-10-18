#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-DialogGuide
desc         : 고정 어셈블리의 안내와 설정 목차를 WPF STA에서 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 격리 설정·PNG·결과 JSON을 별도 artifacts 폴더에 보관.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -STA로 실행하세요.'}
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task017/dialog-guide-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $framework}
$referencePaths=@{}
$assets=Get-Content -Encoding UTF8 -LiteralPath (Join-Path $repositoryDir 'src/VS2017.AIAssistant/obj/project.assets.json') -Raw | ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
foreach($entry in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$referencePaths[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}}}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;if($referencePaths.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($referencePaths[$name])};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'isolated-settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'isolated-workspace'
    [Reflection.Assembly]::LoadFrom($AssemblyPath) | Out-Null
    if($referencePaths.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){[Reflection.Assembly]::LoadFrom($referencePaths['Microsoft.VisualStudio.Shell.15.0']) | Out-Null}
    $refs=@($AssemblyPath,'System.dll','System.Core.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'DialogGuideChecks.cs') -ReferencedAssemblies $refs
    [DialogGuideChecks]::Run($OutputDirectory)
    $result=[pscustomobject]@{Succeeded=[DialogGuideChecks]::Errors.Count -eq 0;Passed=[DialogGuideChecks]::Checks.Count;Failed=[DialogGuideChecks]::Errors.Count;Checks=@([DialogGuideChecks]::Checks);Errors=@([DialogGuideChecks]::Errors);AssemblyPath=$AssemblyPath;AssemblySha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $AssemblyPath).Hash;NetworkRequested=$false;PersonalCredentialsRead=$false;CheckedAt=[DateTimeOffset]::Now.ToString('O')}
    $result | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'result.json')
    $result | ConvertTo-Json -Depth 6
    if(!$result.Succeeded){throw "안내 UI 검사 실패: $OutputDirectory"}
}finally{$env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
