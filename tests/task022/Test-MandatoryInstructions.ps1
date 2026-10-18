#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-MandatoryInstructions
desc         : 합성 workspace에서 실제 숨김 사본과 내장 규칙을 독립 또는 고정 제품 DLL로 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : TASK-022 소스 독립·제품 해시·파일 보존·동시 생성·reparse 회귀.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath,[string]$OutputDirectory,[string]$CompilerPath='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$mandatoryRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $mandatoryRepo ('artifacts\task022\mandatory-' + [guid]::NewGuid().ToString('N').Substring(0,10)) }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$mandatoryFixture=Join-Path $OutputDirectory 'fixture'; $null=New-Item -ItemType Directory -Path $mandatoryFixture -Force
$mandatoryTarget=Join-Path $mandatoryFixture 'reparse-target'; $null=New-Item -ItemType Directory -Path $mandatoryTarget -Force
$mandatoryLinkRoot=Join-Path $mandatoryFixture 'reparse-root'; $mandatoryLinkLeaf=Join-Path $mandatoryFixture 'reparse-leaf'
$null=New-Item -ItemType Directory -Path $mandatoryLinkLeaf -Force
if (!(Test-Path -LiteralPath $mandatoryLinkRoot)) { $null=New-Item -ItemType Junction -Path $mandatoryLinkRoot -Target $mandatoryTarget }
$mandatoryLeaf=Join-Path $mandatoryLinkLeaf '.mandatory-instructions.md'
if (!(Test-Path -LiteralPath $mandatoryLeaf)) { $null=New-Item -ItemType Junction -Path $mandatoryLeaf -Target $mandatoryTarget }
$mandatorySource=Join-Path $mandatoryRepo 'src\VS2017.AIAssistant\Chat\MandatoryInstructionStore.cs'
$mandatorySources=@((Join-Path $PSScriptRoot 'MandatoryInstructionChecks.cs'))
$mandatorySelected='-'
if ($AssemblyPath) { $mandatorySelected=(Resolve-Path -LiteralPath $AssemblyPath).Path }
else { $mandatorySources=@($mandatorySource) + $mandatorySources }
$mandatoryExe=Join-Path $OutputDirectory 'MandatoryInstructionChecks.exe'
$mandatoryArgs=@('/nologo','/target:exe','/langversion:7','/reference:System.dll','/reference:System.Core.dll','/reference:System.Web.Extensions.dll',('/out:' + $mandatoryExe)) + $mandatorySources
& $CompilerPath @mandatoryArgs
if ($LASTEXITCODE -ne 0) { throw '필수 규칙 독립 검사 컴파일이 실패했습니다.' }
& $mandatoryExe $mandatoryFixture $mandatorySelected (Join-Path $OutputDirectory 'result.json')
$mandatoryExit=$LASTEXITCODE
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'result.json')) {
    $mandatoryReport=Get-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $mandatoryReport | Add-Member -NotePropertyName CurrentSourceSha256 -NotePropertyValue (Get-FileHash -LiteralPath $mandatorySource -Algorithm SHA256).Hash
    $mandatoryReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
}
exit $mandatoryExit
