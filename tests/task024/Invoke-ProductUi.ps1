#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Invoke-ProductUi
desc         : 고정 제품 사본에서 UI 경계 검사 또는 안내용 실제 WPF 프레임 캡처를 실행한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK024 DLL·자산 해시 보존, 네트워크·실계정·Window.Show 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[ValidateSet('AssistantUi','Capture')][string]$Mode='AssistantUi')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Use Windows PowerShell 5.1 -NoProfile -STA.'}
$taskRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw 'Existing evidence is preserved. Choose a fresh output folder.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$taskInputs=Join-Path $OutputDirectory 'input';$null=New-Item -ItemType Directory -Path $taskInputs
$taskOriginalHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
$taskProduct=Join-Path $taskInputs 'VS2017.AIAssistant.dll'
Copy-Item -LiteralPath $AssemblyPath -Destination $taskProduct
$taskOriginalDirectory=Split-Path -Parent $AssemblyPath
$taskCopied=@()
foreach($taskAsset in Get-ChildItem -LiteralPath $taskOriginalDirectory -Recurse -File){
 $taskRelative=$taskAsset.FullName.Substring($taskOriginalDirectory.Length).TrimStart('\')
 if($taskAsset.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'A fixed product input is a reparse point.'}
 if($taskRelative -eq 'VS2017.AIAssistant.dll'){continue}
 if($taskRelative -notmatch '^(Resources\\|runtimes\\)' -and $taskRelative -notmatch '^(ICSharpCode\.AvalonEdit|Microsoft\.Web\.WebView2\.(Core|Wpf|WinForms))\.dll$'){continue}
 if($taskAsset.Length -gt 32MB -or $taskCopied.Count -ge 300){throw 'Product input budget exceeded.'}
 $taskDestination=Join-Path $taskInputs $taskRelative;$null=New-Item -ItemType Directory -Force -Path (Split-Path -Parent $taskDestination)
 Copy-Item -LiteralPath $taskAsset.FullName -Destination $taskDestination
 $taskAssetHash=(Get-FileHash -LiteralPath $taskAsset.FullName -Algorithm SHA256).Hash
 if($taskAssetHash -ne (Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash){throw 'Asset copy hash mismatch.'}
 $taskCopied+=[pscustomobject]@{RelativePath=$taskRelative;Sha256=$taskAssetHash}
}
foreach($taskFramework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions','System.Drawing')){Add-Type -AssemblyName $taskFramework}
[Windows.Media.RenderOptions]::ProcessRenderMode=[Windows.Interop.RenderMode]::SoftwareOnly
$taskReferencePaths=@{}
. (Join-Path $PSScriptRoot 'Get-ProductSdkReferences.ps1')
$taskReferencePaths=Get-Task024SdkReferences -RepositoryDirectory $taskRepository -ProductAssemblyPath $taskProduct
$taskResolver=[ResolveEventHandler]{param($sender,$eventArgs)
 $taskName=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
 if($taskName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0){return $null}
 $taskLocal=Join-Path $taskInputs ($taskName+'.dll')
 if(Test-Path -LiteralPath $taskLocal){return [Reflection.Assembly]::LoadFrom($taskLocal)}
 if($taskReferencePaths.ContainsKey($taskName)){return [Reflection.Assembly]::LoadFrom($taskReferencePaths[$taskName])}
 return $null
}
$taskOldSettings=$env:VS2017_AI_SETTINGS_PATH;$taskOldWorkspace=$env:VS2017_AI_WORKSPACE_PATH;$taskOldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
[AppDomain]::CurrentDomain.add_AssemblyResolve($taskResolver)
try{
 $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
 $null=[Reflection.Assembly]::LoadFrom($taskProduct)
 if($taskReferencePaths.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){$null=[Reflection.Assembly]::LoadFrom($taskReferencePaths['Microsoft.VisualStudio.Shell.15.0'])}
 $taskReferences=@($taskProduct,'System.dll','System.Core.dll','System.Web.Extensions.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
 $taskReferences+=Join-Path $taskInputs 'ICSharpCode.AvalonEdit.dll'
 if($Mode -eq 'Capture'){
  Add-Type -Path (Join-Path $PSScriptRoot 'GuideMediaCapture.cs') -ReferencedAssemblies $taskReferences
  [CodexCode.Task024.GuideMediaCapture]::Run((Join-Path $OutputDirectory 'frames'))
  $taskCaptureManifest=Get-Content -LiteralPath (Join-Path $OutputDirectory 'frames\capture-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
  if(@($taskCaptureManifest.Frames).Count -ne 20 -or @(Get-ChildItem -LiteralPath (Join-Path $OutputDirectory 'frames') -Filter '*.png' -Recurse -File).Count -ne 20){throw 'The product capture must contain twenty frames.'}
  $taskPassed=1;$taskFailed=0;$taskChecks=@('Twenty real-product WPF synthetic demonstration frames rendered without showing windows');$taskErrors=@()
 }else{
  Add-Type -Path (Join-Path $PSScriptRoot 'AssistantUiChecks.cs') -ReferencedAssemblies $taskReferences
  [AssistantUiChecks]::Run($OutputDirectory)
  $taskPassed=[AssistantUiChecks]::Checks.Count;$taskFailed=[AssistantUiChecks]::Errors.Count;$taskChecks=@([AssistantUiChecks]::Checks);$taskErrors=@([AssistantUiChecks]::Errors)
 }
 if($taskOriginalHash -ne (Get-FileHash -LiteralPath $taskProduct -Algorithm SHA256).Hash -or $taskOriginalHash -ne (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash){throw 'Fixed product assembly changed during the test.'}
 $taskResult=[ordered]@{Succeeded=$taskFailed -eq 0;Passed=$taskPassed;Failed=$taskFailed;Checks=$taskChecks;Errors=$taskErrors;Mode=$Mode;SourceOnly=$false;ProductAssemblyPath=$AssemblyPath;ProductAssemblySha256=$taskOriginalHash;CopiedInputs=$taskCopied;WindowShown=$false;NetworkRequested=$false;NativePointerUsed=$false;AuthenticationExecuted=$false;InstallerExecuted=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
 $taskResult | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
 $taskResult | ConvertTo-Json -Depth 7
 if($taskFailed -ne 0){exit 1}
}finally{
 [AppDomain]::CurrentDomain.remove_AssemblyResolve($taskResolver)
 $env:VS2017_AI_SETTINGS_PATH=$taskOldSettings;$env:VS2017_AI_WORKSPACE_PATH=$taskOldWorkspace;$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$taskOldDiscovery
}
