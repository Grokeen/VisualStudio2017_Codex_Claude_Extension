#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-StorageBoundaries
desc         : 실제 저장 소스만 개인 사본으로 컴파일해 페이지·경합·원자 커밋·시간대·길이 상한을 검증한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK068 GUI와 공유 빌드 출력 없이 입력 해시와 실패 원문 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$CompilerPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRoot = Join-Path $repo 'artifacts\task068storage'
$run = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $outputRoot ('run-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $run) { throw 'Existing storage evidence must not be overwritten.' }
$source = Join-Path $run 'source'
$null = New-Item -ItemType Directory -Path $source -Force
$hashes = @()
foreach ($name in @('MarkdownWorkspaceStore.cs', 'MarkdownWorkspaceStore.Committed.cs', 'ChatWorkspace.cs')) {
    $path = Join-Path $repo ('src\VS2017.AIAssistant\Chat\' + $name)
    $before = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $copy = Join-Path $source $name
    Copy-Item -LiteralPath $path -Destination $copy
    $snapshot = (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash
    $after = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    $hashes += [pscustomobject]@{ Path = $path; Before = $before; Snapshot = $snapshot; After = $after; Stable = ($before -eq $snapshot -and $before -eq $after) }
}
$hashes | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $run 'source-hashes.json') -Encoding UTF8
if (@($hashes | Where-Object { !$_.Stable }).Count -gt 0) { throw 'Product changed during source snapshot; preserve evidence and use a new run.' }
$chat = [IO.File]::ReadAllText((Join-Path $source 'ChatWorkspace.cs'))
$match = [regex]::Match($chat, '(?s)    public sealed class ChatMessage\r?\n.*?(?=    /// -+\r?\n    /// <summary>\r?\n    /// name         : ChatSession)')
if (!$match.Success) { throw 'Actual ChatMessage extraction boundary changed.' }
$message = 'using System;' + "`nnamespace VS2017.AIAssistant.Chat {`n" + $match.Value + "`n}`n"
$messagePath = Join-Path $source 'ChatMessage.actual.cs'
[IO.File]::WriteAllText($messagePath, $message, [Text.UTF8Encoding]::new($false))
[pscustomobject]@{ ExactClassSubstring = $true; Index = $match.Index; Length = $match.Length; Hash = (Get-FileHash -LiteralPath $messagePath -Algorithm SHA256).Hash } | ConvertTo-Json | Set-Content (Join-Path $run 'extraction.json') -Encoding UTF8
foreach ($name in @('StorageProductChecks.cs', 'StorageBoundaryChecks.cs')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $source $name) }
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$arguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe')
foreach ($name in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions')) { $arguments += '/reference:' + (Join-Path $framework ($name + '.dll')) }
foreach ($name in @('MarkdownWorkspaceStore.cs', 'MarkdownWorkspaceStore.Committed.cs', 'ChatMessage.actual.cs', 'StorageProductChecks.cs', 'StorageBoundaryChecks.cs')) { $arguments += Join-Path $source $name }
$failures = @()
foreach ($entry in @(@{ Main = 'StorageProductChecks'; Result = 'result.json' }, @{ Main = 'StorageBoundaryChecks'; Result = 'boundary-result.json' })) {
    $exe = Join-Path $run ($entry.Main + '.exe')
    & $CompilerPath @arguments ('/main:' + $entry.Main) ('/out:' + $exe) *> (Join-Path $run ($entry.Main + '-compile.log'))
    if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $run ($entry.Main + '-compile.log')); throw "Storage source compile failed: $run" }
    $fixtures = Join-Path $outputRoot ('f\' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    & $exe $run $fixtures *> (Join-Path $run ($entry.Main + '-results.log'))
    if ($LASTEXITCODE -ne 0) { $failures += $entry.Main }
    Get-Content (Join-Path $run ($entry.Main + '-results.log'))
}
Write-Output "Storage source evidence: $run"
if ($failures.Count -gt 0) { throw ('Storage checks failed: ' + ($failures -join ', ')) }
