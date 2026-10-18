#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-LiveNativeRuntime
desc         : 명시적으로 선택한 실제 Codex 계정으로 격리 C# 수정과 자체 테스트를 수행한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 전체 액세스 실제 연동 검증, 사용량 발생 가능.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$CodexPath,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts\live-native\'+[guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputDirectory){throw '실제 연동 검증은 새 폴더에만 실행합니다.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$CodexPath=(Resolve-Path -LiteralPath $CodexPath).Path
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$copy=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $AssemblyPath -Destination $copy
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$harness=Join-Path $OutputDirectory 'LiveRuntimeChecks.exe'
& $compiler /nologo /target:exe "/out:$harness" /reference:System.Web.Extensions.dll "/reference:$copy" (Join-Path $PSScriptRoot 'LiveRuntimeChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Live runtime checks compile failed.'}
& $harness $CodexPath $OutputDirectory
$result=$LASTEXITCODE
[pscustomobject]@{AssemblySha256=(Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash;ExitCode=$result;Result=(Join-Path $OutputDirectory 'result.json')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
exit $result
