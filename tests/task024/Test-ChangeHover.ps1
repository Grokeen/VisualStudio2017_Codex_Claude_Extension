#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ChangeHover
desc         : 고정 제품 tree를 복사·해시 확인하고 명시적 선택 때만 물리 변경 hover를 검사한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : CompileOnly 기본, GUI 예약 후 UseNativePointer 명시 시만 실제 입력.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$UseNativePointer,[switch]$CompileOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
if($UseNativePointer -and $CompileOnly){throw 'CompileOnly와 UseNativePointer는 함께 지정할 수 없습니다.'}
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$sourceDirectory=[IO.Path]::GetDirectoryName($AssemblyPath)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$separator=[IO.Path]::DirectorySeparatorChar
if($OutputDirectory.StartsWith($sourceDirectory.TrimEnd($separator)+$separator,[StringComparison]::OrdinalIgnoreCase) -or $sourceDirectory.StartsWith($OutputDirectory.TrimEnd($separator)+$separator,[StringComparison]::OrdinalIgnoreCase) -or $OutputDirectory -eq $sourceDirectory){throw '입력 tree와 출력 디렉터리는 겹치지 않아야 합니다.'}
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -gt 0){throw '기존 증거를 보존합니다. 새 출력 디렉터리를 지정하세요.'}
$sourceFiles=New-Object 'System.Collections.Generic.List[System.IO.FileInfo]'
$pending=New-Object 'System.Collections.Generic.Stack[System.IO.DirectoryInfo]'
$pending.Push((New-Object IO.DirectoryInfo($sourceDirectory)))
$enumerated=0;$totalBytes=0L
while($pending.Count -gt 0){$current=$pending.Pop();if(($current.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '입력 tree의 reparse point는 복사하지 않습니다.'};foreach($item in $current.EnumerateFileSystemInfos()){$enumerated++;if($enumerated -gt 2048){throw '입력 tree의 탐색 상한을 초과했습니다.'};if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '입력 tree의 reparse point는 복사하지 않습니다.'};if($item -is [IO.DirectoryInfo]){$pending.Push($item)}else{$totalBytes+=$item.Length;if($sourceFiles.Count -ge 1024 -or $totalBytes -gt 128MB){throw '고정 입력 tree의 1024파일/128MiB 상한을 초과했습니다.'};$sourceFiles.Add($item)}}}
$identity=[Reflection.AssemblyName]::GetAssemblyName($AssemblyPath)
if($UseNativePointer -and $identity.Name -ne 'VS2017.AIAssistant'){throw '실제 native 검사는 제품 VS2017.AIAssistant 고정 DLL만 허용합니다.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$productDirectory=Join-Path $OutputDirectory 'product';New-Item -ItemType Directory -Path $productDirectory|Out-Null
$frozen=@(foreach($item in $sourceFiles){$relative=$item.FullName.Substring($sourceDirectory.Length).TrimStart($separator);$destination=[IO.Path]::GetFullPath((Join-Path $productDirectory $relative));if(!$destination.StartsWith($productDirectory+$separator,[StringComparison]::OrdinalIgnoreCase)){throw '복사 대상 경로 이탈'};$hash=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash;[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null;Copy-Item -LiteralPath $item.FullName -Destination $destination;if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $hash){throw '고정 tree 사본 해시 불일치'};[pscustomobject]@{RelativePath=$relative;Source=$item.FullName;Copy=$destination;Sha256=$hash}})
$productPath=Join-Path $productDirectory ([IO.Path]::GetFileName($AssemblyPath))
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions')){Add-Type -AssemblyName $name}
$references=@{}
if($identity.Name -eq 'VS2017.AIAssistant'){
    . (Join-Path $PSScriptRoot 'Get-ProductSdkReferences.ps1')
    $references=Get-Task024SdkReferences -RepositoryDirectory $repositoryDir -ProductAssemblyPath $productPath
}
$resolver=[ResolveEventHandler]{param($sender,$args)$name=(New-Object Reflection.AssemblyName($args.Name)).Name;$local=Join-Path $productDirectory ($name+'.dll');if(Test-Path -LiteralPath $local){return [Reflection.Assembly]::LoadFrom($local)};if($references.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($references[$name])};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
$failure=$null
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [Reflection.Assembly]::LoadFrom($productPath)|Out-Null
    $refs=@($productPath,'System.dll','System.Core.dll','System.Web.Extensions.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'ChangeHoverChecks.cs') -ReferencedAssemblies $refs
    if($UseNativePointer){[ChangeHoverChecks]::Run($OutputDirectory)}
}catch{$failure=$_.Exception.ToString()}
finally{
    $env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)
    $changed=@(foreach($entry in $frozen){try{if((Get-FileHash -LiteralPath $entry.Source -Algorithm SHA256).Hash -ne $entry.Sha256 -or (Get-FileHash -LiteralPath $entry.Copy -Algorithm SHA256).Hash -ne $entry.Sha256){$entry.RelativePath}}catch{$entry.RelativePath}})
    $type='ChangeHoverChecks' -as [type]
    $passed=0;$errors=@();$checks=@();$shown=$false;$pointer=$false;$hover=$false;$nativeWheel=$false;$syntheticWheel=$false;$blocked=$false;$kind='CompilationOnly'
    if($type){$passed=[ChangeHoverChecks]::Checks.Count;$errors=@([ChangeHoverChecks]::Errors);$checks=@([ChangeHoverChecks]::Checks);$shown=[ChangeHoverChecks]::WindowShown;$pointer=[ChangeHoverChecks]::NativePointerUsed;$hover=[ChangeHoverChecks]::HoverExecuted;$nativeWheel=[ChangeHoverChecks]::NativeWheelExecuted;$syntheticWheel=[ChangeHoverChecks]::SyntheticWheelExecuted;$blocked=[ChangeHoverChecks]::PreconditionBlocked;$kind=[ChangeHoverChecks]::FailureKind}
    if($failure){$errors+=@($failure)};if($changed.Count){$errors+=@('Fixed input or dependency copy changed during verification.')}
    $hash=(Get-FileHash -LiteralPath $productPath -Algorithm SHA256).Hash
    $result=[pscustomobject]@{Succeeded=$errors.Count -eq 0;Passed=$passed;Failed=$errors.Count;Checks=$checks;Errors=$errors;SourceOnly=$identity.Name -ne 'VS2017.AIAssistant';CompilationOnly=!$UseNativePointer;NativePointerRequested=$UseNativePointer.IsPresent;WindowShown=$shown;NativePointerUsed=$pointer;HoverExecuted=$hover;NativeWheelExecuted=$nativeWheel;SyntheticWheelExecuted=$syntheticWheel;PreconditionBlocked=$blocked;FailureKind=$kind;AssemblyPath=$AssemblyPath;FrozenAssemblyPath=$productPath;AssemblySha256=$hash;ProductAssemblySha256=$hash;FrozenTree=$frozen;FrozenTreeUnchanged=$changed.Count -eq 0;RuntimeDiscoveryDisabled=$true;PersonalSettingsIsolated=$true;AiInvoked=$false;NetworkRequested=$false;DesktopChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
    $result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $result|Select-Object Succeeded,Passed,Failed,SourceOnly,CompilationOnly,WindowShown,NativePointerUsed,HoverExecuted,NativeWheelExecuted,SyntheticWheelExecuted,PreconditionBlocked,FailureKind,FrozenTreeUnchanged,AssemblySha256|ConvertTo-Json
}
if(!$result.Succeeded){throw 'Change hover verification failed; original result and diagnostics preserved.'}
