#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-SecureExecution
desc         : 새 PowerShell 자식 안에서만 CWD를 바꾸어 승인 계획 경로 동일성을 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : TASK-021 native 운영 명령·계정·공유 빌드 없는 경로 재현.
update date  : 2026-10-18 CodexCode : 승인 스냅샷·Git 환경 격리·합성 native 취소/timeout 검증 확장.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory, [switch]$Child)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$secureRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $secureRepo ('artifacts\task021\se-' + [guid]::NewGuid().ToString('N').Substring(0,10)) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ($AssemblyPath) { $AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path }
if (!$Child) {
    $secureChildArgs = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, '-Child', '-OutputDirectory', $OutputDirectory)
    if ($AssemblyPath) { $secureChildArgs += @('-AssemblyPath', $AssemblyPath) }
    & (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') @secureChildArgs
    exit $LASTEXITCODE
}
if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Windows PowerShell 5.1로 실행하세요.' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$secureFixture = Join-Path $OutputDirectory 'SecureExecutionFixture.exe'
$secureCsc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$secureCscArguments = @('/nologo', '/target:exe', '/platform:anycpu', ('/out:' + $secureFixture), (Join-Path $PSScriptRoot 'SecureExecutionFixture.cs'))
& $secureCsc @secureCscArguments
if ($LASTEXITCODE -ne 0) { throw '독립 합성 fixture 컴파일이 실패했습니다.' }
$secureReferences = @('System.dll','System.Core.dll','System.Data.dll')
$secureMode = 'SourceOnlyFixture'; $secureAssemblyHash = $null
if ($AssemblyPath) {
    $secureAssemblyHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    $null = [Reflection.Assembly]::LoadFrom($AssemblyPath)
    Add-Type -ReferencedAssemblies ($secureReferences + @($AssemblyPath)) -Path (Join-Path $PSScriptRoot 'SecureExecutionChecks.cs')
    $secureMode = 'FrozenProductAssembly'
}
else {
    Add-Type -ReferencedAssemblies $secureReferences -Path @((Join-Path $secureRepo 'src\VS2017.AIAssistant\Services\WorkspaceActionService.cs'), (Join-Path $secureRepo 'src\VS2017.AIAssistant\Providers\ProcessJob.cs'), (Join-Path $PSScriptRoot 'SecureExecutionChecks.cs'))
}
$secureResult = [CodexCode.Task021.SecureExecutionChecks]::Run((Join-Path $OutputDirectory 'fixture'), $secureFixture)
$secureReport = [pscustomobject]@{
    Succeeded=$secureResult.Succeeded; Mode=$secureMode; Passed=$secureResult.Passed; Failures=$secureResult.Failures
    Evidence=$secureResult.Evidence; Error=$secureResult.Error; ChildCwdRestored=$secureResult.CwdRestored
    ProductAssemblyPath=$AssemblyPath; ProductAssemblySha256=$secureAssemblyHash
    SourceSha256=(Get-FileHash -LiteralPath (Join-Path $secureRepo 'src\VS2017.AIAssistant\Services\WorkspaceActionService.cs') -Algorithm SHA256).Hash
    OperationalCommandExecuted=$false; SyntheticProcessesStarted=$secureResult.SyntheticProcessesStarted
    SyntheticFixturePath=$secureFixture; SyntheticFixtureSha256=(Get-FileHash -LiteralPath $secureFixture -Algorithm SHA256).Hash
    NetworkAccessed=$false; AccountRead=$false; SharedProductBuild=$false
    OutputDirectory=$OutputDirectory; CheckedAt=(Get-Date).ToString('O')
}
$secureReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
$secureReport | ConvertTo-Json -Depth 6
if (!$secureResult.Succeeded -or !$secureResult.CwdRestored) { exit 1 }
exit 0
