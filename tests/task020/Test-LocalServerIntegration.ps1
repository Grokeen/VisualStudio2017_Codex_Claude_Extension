#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-LocalServerIntegration
desc         : 준비한 실제 Subversion 도구로 로컬 서버·승인 작업·격리 배포를 확인한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 실제 운영 서버와 구분하는 선택 실행 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$SubversionBin,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts\server-integration\'+[guid]::NewGuid().ToString('N'))}
if(Test-Path -LiteralPath $OutputDirectory){throw '서버 연동 검증은 새 폴더에 실행합니다.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory);$SubversionBin=(Resolve-Path -LiteralPath $SubversionBin).Path
$copy=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $AssemblyPath -Destination $copy
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe';$harness=Join-Path $OutputDirectory 'SvnIntegrationChecks.exe'
& $compiler /nologo /target:exe "/out:$harness" /reference:System.Web.Extensions.dll "/reference:$copy" (Join-Path $PSScriptRoot 'SvnIntegrationChecks.cs')
if($LASTEXITCODE -ne 0){throw 'Local server checks compile failed.'}
& $harness $OutputDirectory $SubversionBin
exit $LASTEXITCODE
