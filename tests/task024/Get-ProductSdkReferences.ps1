#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Get-Task024SdkReferences
desc         : 실제 고정 DLL의 Shell 참조 버전에 맞는 SDK 의존성을 읽어 다른 SKU의 SDK 혼입을 방지한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK024 검사 전용, 제품 실행·빌드·전역 설정 변경 없음.
-------------------------------------------------------------------------
#>
function Get-Task024SdkReferences {
 param([Parameter(Mandatory=$true)][string]$RepositoryDirectory,[Parameter(Mandatory=$true)][string]$ProductAssemblyPath)
 $taskMetadata=[Reflection.Assembly]::ReflectionOnlyLoadFrom([IO.Path]::GetFullPath($ProductAssemblyPath))
 $taskShell=@($taskMetadata.GetReferencedAssemblies() | Where-Object{$_.Name -eq 'Microsoft.VisualStudio.Shell.15.0'})
 if($taskShell.Count -ne 1 -or $taskShell[0].Version.Major -notin @(15,16,17)){throw 'The product must identify one supported Visual Studio SDK.'}
 $taskProject=switch($taskShell[0].Version.Major){15{'VS2017.AIAssistant'}16{'VS2019.AIAssistant'}17{'VS2022.AIAssistant'}}
 $taskAssetsPath=Join-Path $RepositoryDirectory ('src\'+$taskProject+'\obj\project.assets.json')
 $taskSdkAssets=Get-Content -LiteralPath $taskAssetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
 $taskSdkPackageRoot=$taskSdkAssets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
 $taskTargets=@($taskSdkAssets.targets.PSObject.Properties | Where-Object{$_.Name.StartsWith('.NETFramework,Version=',[StringComparison]::Ordinal) -and !$_.Name.Contains('/')})
 if($taskTargets.Count -ne 1){throw 'The selected SDK assets must identify one .NET Framework target.'}
 $taskSdkReferences=@{}
 foreach($taskEntry in $taskTargets[0].Value.PSObject.Properties){
  if($taskEntry.Value.PSObject.Properties.Name -notcontains 'compile'){continue}
  foreach($taskItem in $taskEntry.Value.compile.PSObject.Properties.Name){if($taskItem -like '*.dll'){$taskSdkReferences[[IO.Path]::GetFileNameWithoutExtension($taskItem)]=Join-Path $taskSdkPackageRoot ($taskSdkAssets.libraries.($taskEntry.Name).path+'/'+$taskItem)}}
 }
 return $taskSdkReferences
}
