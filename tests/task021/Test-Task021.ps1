#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Task021
desc         : 고정 제품 DLL의 TASK-021 여덟 검사를 격리된 Windows PowerShell STA 프로세스에서 순차 실행한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 실제 계정·네트워크 탐색 없이 각 결과·로그·묶음 요약과 DLL 해시를 보존.
update date  : 2026-10-18 CodexCode : SecureExecution·WorkerOutput 그룹과 배열형 통과 목록·Success 결과 계약 지원.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$AssemblyPath, [string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
-------------------------------------------------------------------------
name         : ConvertTo-Task021Argument
desc         : Windows 파일 경로를 한 인수로 인용하고 닫는 인용부호 앞 역슬래시를 보존한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공백·한글·작은따옴표 경로가 명령 코드로 해석되지 않게 직접 프로세스 인수 구성.
-------------------------------------------------------------------------
#>
function ConvertTo-Task021Argument {
    param([Parameter(Mandatory = $true)][string]$Value)
    if ($Value.IndexOfAny([char[]]@('"', "`r", "`n")) -ge 0) { throw '명령 인수에 잘못된 경로 문자가 있습니다.' }
    return '"' + [regex]::Replace($Value, '(\\+)$', '$1$1') + '"'
}

<#
-------------------------------------------------------------------------
name         : Invoke-Task021Suite
desc         : 한 검사만 별도 프로세스에서 실행하고 성공 여부를 exit code·JSON·고정 DLL 해시로 확인한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 한 검사 오류에도 나머지 그룹을 실행하며 누락 결과를 명시적인 실행 실패로 기록.
-------------------------------------------------------------------------
#>
function Invoke-Task021Suite {
    param($Suite, [string]$FrozenAssembly, [string]$FrozenHash, [string]$SuiteOutput, [string]$PowerShellPath)
    $null = New-Item -ItemType Directory -Path $SuiteOutput -Force
    $scriptPath = Join-Path $PSScriptRoot $Suite.Script
    $resultPath = Join-Path $SuiteOutput 'result.json'
    $stdoutPath = Join-Path $SuiteOutput 'stdout.log'
    $stderrPath = Join-Path $SuiteOutput 'stderr.log'
    $arguments = @('-NoLogo', '-NoProfile', '-NonInteractive', '-STA', '-ExecutionPolicy', 'Bypass', '-File', $scriptPath, '-AssemblyPath', $FrozenAssembly, '-OutputDirectory', $SuiteOutput)
    $argumentLine = ($arguments | ForEach-Object { ConvertTo-Task021Argument $_ }) -join ' '
    $process = New-Object Diagnostics.Process
    $startedAt = [DateTimeOffset]::UtcNow
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $exitCode = -1; $passed = 0; $failed = 0; $errorText = ''; $succeeded = $false
    try {
        if (!(Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw "검사 스크립트가 없습니다: $scriptPath" }
        $process.StartInfo = New-Object Diagnostics.ProcessStartInfo
        $process.StartInfo.FileName = $PowerShellPath
        $process.StartInfo.Arguments = $argumentLine
        $process.StartInfo.WorkingDirectory = $SuiteOutput
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        $process.StartInfo.EnvironmentVariables['VS2017_AI_SETTINGS_PATH'] = Join-Path $SuiteOutput 'isolated-settings.json'
        $process.StartInfo.EnvironmentVariables['VS2017_AI_WORKSPACE_PATH'] = Join-Path $SuiteOutput 'isolated-workspace'
        $process.StartInfo.EnvironmentVariables['VS2017_AI_DISABLE_RUNTIME_DISCOVERY'] = '1'
        if (!$process.Start()) { throw '별도 Windows PowerShell 검사 프로세스를 시작하지 못했습니다.' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        [IO.File]::WriteAllText($stdoutPath, $stdout.GetAwaiter().GetResult(), (New-Object Text.UTF8Encoding($true)))
        [IO.File]::WriteAllText($stderrPath, $stderr.GetAwaiter().GetResult(), (New-Object Text.UTF8Encoding($true)))
        if (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw "검사가 결과 JSON을 생성하지 않았습니다. exit=$exitCode" }
        $report = Get-Content -LiteralPath $resultPath -Encoding UTF8 -Raw | ConvertFrom-Json
        if ($null -eq $report -or $report.PSObject.Properties.Name -notcontains 'Passed') { throw '결과 JSON에 Passed 계수가 없습니다.' }
        if ($report.Passed -is [Collections.IEnumerable] -and $report.Passed -isnot [string]) { $passed = @($report.Passed).Count }
        else { $passed = [int]$report.Passed }
        if ($passed -lt 0) { throw '결과 JSON의 Passed 계수가 음수입니다.' }
        $hasFailed = $report.PSObject.Properties.Name -contains 'Failed'
        $hasFailures = $report.PSObject.Properties.Name -contains 'Failures'
        $hasSucceeded = $report.PSObject.Properties.Name -contains 'Succeeded'
        $hasSuccess = $report.PSObject.Properties.Name -contains 'Success'
        if (!$hasFailed -and !$hasFailures -and !$hasSucceeded -and !$hasSuccess) { throw '결과 JSON에 실패 계수 또는 성공 상태가 없습니다.' }
        if ($hasFailed) { $failed = [int]$report.Failed; if ($failed -lt 0) { throw '결과 JSON의 Failed 계수가 음수입니다.' } }
        elseif ($hasFailures) { $failed = @($report.Failures).Count }
        $reportSucceeded = $failed -eq 0 -and (!$hasSucceeded -or $report.Succeeded -eq $true) -and (!$hasSuccess -or $report.Success -eq $true)
        if ($report.PSObject.Properties.Name -contains 'Total') { $reportSucceeded = $reportSucceeded -and $passed -eq [int]$report.Total }
        if ((Get-FileHash -LiteralPath $FrozenAssembly -Algorithm SHA256).Hash -ne $FrozenHash) { throw '고정 입력 DLL이 검사 도중 변경되었습니다.' }
        $suiteCopy = Join-Path $SuiteOutput 'VS2017.AIAssistant.dll'
        if ((Test-Path -LiteralPath $suiteCopy -PathType Leaf) -and (Get-FileHash -LiteralPath $suiteCopy -Algorithm SHA256).Hash -ne $FrozenHash) { throw '하위 검사 DLL 사본이 고정 입력과 다릅니다.' }
        $succeeded = $exitCode -eq 0 -and $reportSucceeded -and $passed -gt 0
        if (!$succeeded) {
            $errorText = "검사 실패: exit=$exitCode, passed=$passed, failed=$failed"
            if ($report.PSObject.Properties.Name -contains 'Error') { $errorText += ' ' + [string]$report.Error }
            if ($report.PSObject.Properties.Name -contains 'Errors') { $errorText += ' ' + (@($report.Errors) -join ' | ') }
            if ($hasFailures) { $errorText += ' ' + (@($report.Failures) -join ' | ') }
            if ($report.PSObject.Properties.Name -contains 'Failure') { $errorText += ' ' + [string]$report.Failure }
        }
    }
    catch { $errorText = $_.Exception.Message }
    finally { $stopwatch.Stop(); $process.Dispose() }
    $reportedFailed = $failed
    if (!$succeeded -and $failed -lt 1) { $failed = 1 }
    if (!(Test-Path -LiteralPath $resultPath -PathType Leaf)) {
        [pscustomobject]@{ Succeeded = $false; Passed = 0; Failed = 1; RunnerError = $errorText; ExitCode = $exitCode } | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    }
    return [pscustomobject]@{
        Name = $Suite.Name; Succeeded = $succeeded; Passed = $passed; Failed = $failed; ReportedFailed = $reportedFailed; ExecutionFailure = !$succeeded -and $reportedFailed -eq 0; ExitCode = $exitCode; Error = $errorText
        ScriptPath = $scriptPath; ResultPath = $resultPath; StandardOutputPath = $stdoutPath; StandardErrorPath = $stderrPath
        ProcessPath = $PowerShellPath; Arguments = $arguments; StartedAt = $startedAt.ToString('O'); ElapsedSeconds = [Math]::Round($stopwatch.Elapsed.TotalSeconds, 3)
    }
}

if ($PSVersionTable.PSEdition -ne 'Desktop' -or $PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -lt 1) { throw 'Windows PowerShell 5.1에서 실행하세요.' }
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$resolvedAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
if ([IO.Path]::GetExtension($resolvedAssembly) -ine '.dll' -or [Reflection.AssemblyName]::GetAssemblyName($resolvedAssembly).Name -ne 'VS2017.AIAssistant') { throw '실제 VS2017.AIAssistant 제품 DLL을 지정하세요. 소스 전용 검사는 지원하지 않습니다.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir ('artifacts\task021\bundle-' + [guid]::NewGuid().ToString('N').Substring(0, 10)) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $OutputDirectory) -and !(Test-Path -LiteralPath $OutputDirectory -PathType Container)) { throw '묶음 결과 경로는 디렉터리여야 합니다.' }
foreach ($reservedName in @('summary.json', 'input', 'selection', 'diff', 'help', 'images', 'fan', 'secure', 'secure-execution', 'worker-output')) {
    if (Test-Path -LiteralPath (Join-Path $OutputDirectory $reservedName)) { throw "이전 묶음 결과를 덮어쓰지 않도록 새 결과 경로를 지정하세요: $reservedName" }
}
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$powerShellPath = Join-Path $PSHOME 'powershell.exe'
if (!(Test-Path -LiteralPath $powerShellPath -PathType Leaf)) { throw 'Windows PowerShell 5.1 실행 파일을 찾을 수 없습니다.' }
$inputDirectory = Join-Path $OutputDirectory 'input'
$null = New-Item -ItemType Directory -Path $inputDirectory -Force
$frozenAssembly = Join-Path $inputDirectory 'VS2017.AIAssistant.dll'
$originalHash = (Get-FileHash -LiteralPath $resolvedAssembly -Algorithm SHA256).Hash
Copy-Item -LiteralPath $resolvedAssembly -Destination $frozenAssembly
$frozenHash = (Get-FileHash -LiteralPath $frozenAssembly -Algorithm SHA256).Hash
if ($frozenHash -ne $originalHash) { throw '입력 DLL 복사 중 내용이 변경되었습니다. 고정 후보를 다시 지정하세요.' }
$suites = @(
    [pscustomobject]@{ Name = 'SelectionContext'; Script = 'Test-SelectionContext.ps1'; Directory = 'selection' },
    [pscustomobject]@{ Name = 'DiffReview'; Script = 'Test-DiffReview.ps1'; Directory = 'diff' },
    [pscustomobject]@{ Name = 'HelpContent'; Script = 'Test-HelpContent.ps1'; Directory = 'help' },
    [pscustomobject]@{ Name = 'ImageFollowup'; Script = 'Test-ImageFollowup.ps1'; Directory = 'images' },
    [pscustomobject]@{ Name = 'FanGeometry'; Script = 'Test-FanGeometry.ps1'; Directory = 'fan' },
    [pscustomobject]@{ Name = 'SecureCoding'; Script = 'Test-SecureCoding.ps1'; Directory = 'secure' },
    [pscustomobject]@{ Name = 'SecureExecution'; Script = 'Test-SecureExecution.ps1'; Directory = 'secure-execution' },
    [pscustomobject]@{ Name = 'WorkerOutput'; Script = 'Test-WorkerOutput.ps1'; Directory = 'worker-output' }
)
$startedAt = [DateTimeOffset]::UtcNow
$suiteResults = @()
foreach ($suite in $suites) {
    Write-Output ('TASK-021 검사 시작: ' + $suite.Name)
    $suiteResult = Invoke-Task021Suite -Suite $suite -FrozenAssembly $frozenAssembly -FrozenHash $frozenHash -SuiteOutput (Join-Path $OutputDirectory $suite.Directory) -PowerShellPath $powerShellPath
    $suiteResults += $suiteResult
    Write-Output ($suite.Name + ': ' + $(if ($suiteResult.Succeeded) { 'PASS' } else { 'FAIL' }) + ' (exit=' + $suiteResult.ExitCode + ')')
}
$successfulSuites = @($suiteResults | Where-Object { $_.Succeeded }).Count
$summary = [pscustomobject]@{
    Succeeded = $successfulSuites -eq $suites.Count; Passed = ($suiteResults | Measure-Object Passed -Sum).Sum; Failed = ($suiteResults | Measure-Object Failed -Sum).Sum
    PassedSuites = $successfulSuites; FailedSuites = $suites.Count - $successfulSuites; Suites = $suiteResults
    AssemblyPath = $resolvedAssembly; FrozenAssemblyPath = $frozenAssembly; AssemblySha256 = $frozenHash
    SourceOnly = $false; Sequential = $true; SeparateWindowsPowerShellProcesses = $true; RuntimeDiscoveryDisabled = $true; PersonalSettingsIsolated = $true
    StartedAt = $startedAt.ToString('O'); FinishedAt = [DateTimeOffset]::UtcNow.ToString('O'); OutputDirectory = $OutputDirectory
}
$summary | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
Write-Output ('TASK-021 묶음 결과: ' + (Join-Path $OutputDirectory 'summary.json'))
if (!$summary.Succeeded) { exit 1 }
