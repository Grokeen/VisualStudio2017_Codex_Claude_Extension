#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-NativeRuntime
desc         : 실제 자식 프로세스를 사용하는 JSON 프로토콜·직접 편집·취소·미제공 경계를 격리 검증한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 실제 계정과 운영 서버 없이 네이티브 계약 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts\runtime\'+[guid]::NewGuid().ToString('N'))}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$copy=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'
Copy-Item -LiteralPath $AssemblyPath -Destination $copy
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=Join-Path $OutputDirectory 'NativeProtocolFixture.exe'
$harness=Join-Path $OutputDirectory 'RuntimeChecks.exe'
& $compiler /nologo /target:exe "/out:$fixture" /reference:System.Web.Extensions.dll (Join-Path $PSScriptRoot 'RuntimeProtocolFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Native fixture compile failed.'}
& $compiler /nologo /target:exe "/out:$harness" /reference:System.Web.Extensions.dll "/reference:$copy" (Join-Path $PSScriptRoot 'RuntimeChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Runtime checks compile failed.'}
& $harness $fixture $OutputDirectory
$result=$LASTEXITCODE
[pscustomobject]@{AssemblyPath=$AssemblyPath;AssemblySha256=(Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash;ExitCode=$result;Result=(Join-Path $OutputDirectory 'result.json')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
exit $result
