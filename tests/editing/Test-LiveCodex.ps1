#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-LiveCodex
desc         : 로그인된 실제 Codex CLI에 합성 코드를 보내 빌드 DLL의 수정안 생성 경로를 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 실제 계정 호출은 별도 명령에서만 수행하고 기본 회귀와 분리.
-------------------------------------------------------------------------
.SYNOPSIS
    Explicit live test. Uses the CLI's logged-in account and sends only synthetic source.
.DESCRIPTION
    This does not instantiate EnvDTE or apply changes in a real IDE. Regular tests never call an AI account.
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$CodexPath)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Windows PowerShell 5.1로 실행하세요.' }
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$assemblyPath = Join-Path $repositoryDir 'src\VS2017.AIAssistant\bin\Release\VS2017.AIAssistant.dll'
Add-Type -AssemblyName System.Web.Extensions
[Reflection.Assembly]::LoadFrom($assemblyPath) | Out-Null
$testDir = Join-Path $repositoryDir ('artifacts\source-editing\live-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDir -Force
$samplePath = Join-Path $testDir 'Probe.cs'
$source = "// Synthetic integration test only.`r`nclass Probe { public int GetValue() { return 1; } }`r`n"
[IO.File]::WriteAllText($samplePath, $source, (New-Object Text.UTF8Encoding($true)))
$snapshot = New-Object VS2017.AIAssistant.Editing.DocumentSnapshot($samplePath, $source, 'synthetic-disk-version', 'C# 7.0 / .NET Framework 4.6.2', $false, $false)
$provider = New-Object VS2017.AIAssistant.Providers.CliEditProvider([TimeSpan]::FromSeconds(60))
$task = $provider.GenerateAsync('Codex', (Resolve-Path -LiteralPath $CodexPath).Path, $snapshot,
    'GetValue 메서드가 1 대신 2를 반환하도록 return 1; 한 곳만 수정하세요. 다른 텍스트는 보존하세요.', [Threading.CancellationToken]::None)
$proposal = $task.GetAwaiter().GetResult()
$expected = $source.Replace('return 1;', 'return 2;')
if ($proposal.ProposedText -ne $expected -or !$proposal.HasChanges) { throw '실제 Codex 수정안이 기대한 단일 변경과 다릅니다.' }
if ([IO.File]::ReadAllText($samplePath) -ne $source) { throw '승인 전에 합성 원본 파일이 변경되었습니다.' }
$result = [pscustomobject]@{
    Succeeded = $true
    Provider = 'Codex'
    CheckKind = 'LiveCliProposalOnly'
    UserProjectSourceSent = $false
    OriginalDiskFileUnchanged = $true
    ExpectedSingleChangeMatched = $true
    Summary = $proposal.Summary
    IdeRuntimeTest = $false
}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $testDir 'result.json') -Encoding UTF8
Write-Output 'PASS: 실제 Codex CLI가 합성 코드의 정확한 단일 수정안을 반환했고 승인 전 원본 파일은 그대로입니다.'
Write-Output "결과: $testDir"
