#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ImageRequirements
desc         : 이미지 요구의 A~Z 이름 라우팅과 안내·목차 검사를 지정한 고정 제품으로 실행한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-018 루트 실행기의 이미지 요구 회귀 묶음 추가.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -STA로 실행하세요.'}
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$imageRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $imageRepository ('artifacts\task018\image-requirements-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$global:LASTEXITCODE=0
& (Join-Path $imageRepository 'tests\collaboration\Test-AlphabetAgentRouting.ps1') -AssemblyPath $AssemblyPath
if(!$? -or $LASTEXITCODE -ne 0){throw 'A~Z 이름 라우팅 검사가 실패했습니다.'}
& (Join-Path $imageRepository 'tests\task017\Test-DialogGuide.ps1') -AssemblyPath $AssemblyPath -OutputDirectory (Join-Path $OutputDirectory 'guide')
if(!$? -or $LASTEXITCODE -ne 0){throw '안내·목차 검사가 실패했습니다.'}
Write-Output '이미지 요구 검사 묶음 완료: A~Z 실제 저장 경계 + 안내·목차 WPF'
