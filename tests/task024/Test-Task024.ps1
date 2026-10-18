#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Task024
desc         : 고정 제품 DLL의 Markdown·변경 상세·안내·계정·응답 연결 회귀를 각각 새 프로세스에서 실행한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : TASK024 source-only와 실제 제품 근거를 구분하고 첫 실패·해시 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$taskRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $taskRepository ('artifacts\task024\actual-'+[guid]::NewGuid().ToString('N').Substring(0,10))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw 'Existing test evidence is preserved; choose a new output directory.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$taskExpectedHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
$taskGroups=@(
 @{Name='markdown';Script='Test-Markdown.ps1'},
 @{Name='hover-viewport';Script='Test-HoverViewport.ps1'},
 @{Name='guide';Script='GuideReadability.ps1'},
 @{Name='account';Script='AccountLogin.ps1'},
 @{Name='assistant-ui';Script='Invoke-ProductUi.ps1'}
)
$taskResults=@()
foreach($taskGroup in $taskGroups){
 $taskChildDirectory=Join-Path $OutputDirectory $taskGroup.Name
 $taskLog=Join-Path $OutputDirectory ($taskGroup.Name+'.log')
 $taskPreviousErrorAction=$ErrorActionPreference
 try{
  # PS5.1 native stderr는 성공·실패 기록을 만들기 전에 wrapper를 중단시키면 안 된다.
  $ErrorActionPreference='Continue'
  & powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $taskGroup.Script) -AssemblyPath $AssemblyPath -OutputDirectory $taskChildDirectory *> $taskLog
  $taskExit=$LASTEXITCODE
 }finally{$ErrorActionPreference=$taskPreviousErrorAction}
 $taskResultPath=Join-Path $taskChildDirectory 'result.json'
 $taskChildResult=$null
 if(Test-Path -LiteralPath $taskResultPath){$taskChildResult=Get-Content -LiteralPath $taskResultPath -Raw -Encoding UTF8 | ConvertFrom-Json}
 $taskValid=$false;$taskPassed=0;$taskFailed=1;$taskFailure='No result JSON from the child process.'
 if($null -ne $taskChildResult){
  $taskSourceOnly=($taskChildResult.PSObject.Properties.Name -contains 'SourceOnly' -and $taskChildResult.SourceOnly)
  $taskActualHash=if($taskChildResult.PSObject.Properties.Name -contains 'ProductAssemblySha256'){$taskChildResult.ProductAssemblySha256}else{$null}
  $taskValid=(!$taskSourceOnly -and $taskActualHash -eq $taskExpectedHash -and $taskExit -eq 0 -and $taskChildResult.Succeeded -and $taskChildResult.Failed -eq 0)
  $taskPassed=[int]$taskChildResult.Passed;$taskFailed=[int]$taskChildResult.Failed;$taskFailure=if($taskValid){$null}else{'The child failed or does not identify the exact frozen product assembly.'}
 }
 $taskResults+=[pscustomobject]@{Name=$taskGroup.Name;Succeeded=$taskValid;ExitCode=$taskExit;Passed=$taskPassed;Failed=$taskFailed;ResultPath=$taskResultPath;Log=$taskLog;Failure=$taskFailure}
 Write-Output ($taskGroup.Name+': '+$taskValid+' ('+$taskPassed+' passed, '+$taskFailed+' failed)')
}
$taskAfterHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
$taskSucceeded=@($taskResults | Where-Object{!$_.Succeeded}).Count -eq 0 -and $taskAfterHash -eq $taskExpectedHash
$taskSummary=[ordered]@{Succeeded=$taskSucceeded;GroupsPassed=@($taskResults | Where-Object Succeeded).Count;GroupsTotal=$taskGroups.Count;Passed=($taskResults | Measure-Object -Property Passed -Sum).Sum;Failed=($taskResults | Measure-Object -Property Failed -Sum).Sum;ProductAssemblyPath=$AssemblyPath;ProductAssemblySha256=$taskExpectedHash;ProductAfterSha256=$taskAfterHash;SourceOnly=$false;Results=$taskResults;WindowShown=$false;NativePointerVerified=$false;LiveAuthenticationVerified=$false;PaidAiInvoked=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$taskSummary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
if(!$taskSucceeded){exit 1}
exit 0
