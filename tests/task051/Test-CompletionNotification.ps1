#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-CompletionNotification
desc         : 고정 제품에서 합성 완료 한 번의 실제 Windows 알림 이벤트와 타이머 정리를 격리 관측한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : OS 표시 관측과 API/수명 성공을 분리하고 개인 설정·계정은 사용하지 않는다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$AssetsPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Use Windows PowerShell 5.1 -STA.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count){throw 'Preserve previous evidence.'}
$null=New-Item -ItemType Directory -Path $output -Force
$original=[IO.Path]::GetFullPath($AssemblyPath);$expected=(Get-FileHash -LiteralPath $original).Hash
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_CLI_DISCOVERY;$oldRuntime=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
$resolve=$null
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $output 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $output 'workspace'
    $env:VS2017_AI_DISABLE_CLI_DISCOVERY='1';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":true}',[Text.UTF8Encoding]::new($false))
    foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Windows.Forms','System.Drawing')){Add-Type -AssemblyName $name}
    $assets=Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $target=$assets.targets.PSObject.Properties | Where-Object{$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'} | Select-Object -First 1
    $dependencies=@{}
    foreach($entry in $target.Value.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($part in $entry.Value.compile.PSObject.Properties.Name){if($part -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($part)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$part)}}}}
    $productDirectory=Split-Path $original -Parent
    $resolve=[ResolveEventHandler]{param($sender,$eventArgs)$key=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;$sibling=Join-Path $productDirectory ($key+'.dll');if(Test-Path -LiteralPath $sibling){return [Reflection.Assembly]::LoadFrom($sibling)};if($dependencies.ContainsKey($key)){return [Reflection.Assembly]::LoadFrom($dependencies[$key])};return $null}
    [AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
    [Reflection.Assembly]::LoadFrom($original) | Out-Null
    $references=@($original,'System.dll','System.Core.dll','System.Windows.Forms.dll','System.Drawing.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'CompletionNotificationChecks.cs') -ReferencedAssemblies $references
    [CompletionNotificationChecks]::Run()
    $success=[CompletionNotificationChecks]::IconCreated -and [CompletionNotificationChecks]::TimerStarted -and [CompletionNotificationChecks]::DuplicateCompletionKeptSameIcon -and [CompletionNotificationChecks]::TimedCleanup -and [CompletionNotificationChecks]::DisposedCleanup -and ![CompletionNotificationChecks]::Failure
    $result=[pscustomobject]@{Succeeded=$success;IconCreated=[CompletionNotificationChecks]::IconCreated;TimerStarted=[CompletionNotificationChecks]::TimerStarted;DuplicateCompletionKeptSameIcon=[CompletionNotificationChecks]::DuplicateCompletionKeptSameIcon;TimedCleanup=[CompletionNotificationChecks]::TimedCleanup;DisposedCleanup=[CompletionNotificationChecks]::DisposedCleanup;BalloonShownEvents=[CompletionNotificationChecks]::BalloonShownEvents;OsDisplayedNotificationObserved=[CompletionNotificationChecks]::BalloonShownEvents -gt 0;Failure=[CompletionNotificationChecks]::Failure;AssemblySha256=$expected;WindowShown=$true;ActualIdeTested=$false;SyntheticRequest=$true;LiveAiUsed=$false;OsNotificationSettingsChanged=$false}
    $result|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
    $result|ConvertTo-Json -Depth 4
    if((Get-FileHash -LiteralPath $original).Hash -ne $expected){throw 'Fixed product changed.'}
    if(!$success){throw 'Notification API/lifetime check failed; preserve original evidence.'}
}finally{
    if($resolve){[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
    $env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_CLI_DISCOVERY=$oldDiscovery;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldRuntime
}
