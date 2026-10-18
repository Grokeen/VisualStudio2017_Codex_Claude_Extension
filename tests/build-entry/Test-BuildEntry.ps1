#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-BuildEntry
desc         : 격리된 실행 파일/빌드 fixture로 배치의 선택·호출·오류 전달을 검사한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : PS5.1/PS7 및 한글/공백/셸 특수문자 경로 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runDirectory = Join-Path $repository ('artifacts\build-entry\run-' + [guid]::NewGuid().ToString('N'))
$fixture = Join-Path $runDirectory '한글 & 공백! (경로)'
$scripts = Join-Path $fixture 'scripts'
$null = New-Item -ItemType Directory -Path $scripts -Force
$utf8 = New-Object Text.UTF8Encoding($true)
foreach ($name in @('Build-Vsix.cmd', 'Invoke-VsixBuild.ps1')) {
    Copy-Item -LiteralPath (Join-Path $repository "scripts\$name") -Destination $scripts
}
$inventoryPath = Join-Path $runDirectory 'inventory.json'
$argumentsPath = Join-Path $runDirectory 'vswhere-arguments.txt'
$markerPath = Join-Path $runDirectory 'build-called.json'
$vswhere = Join-Path $fixture 'vswhere.exe'
$results = New-Object 'System.Collections.Generic.List[object]'

@'
param([string]$Configuration, [string]$MSBuildPath)
[pscustomobject]@{ Configuration = $Configuration; MSBuildPath = $MSBuildPath; Script = $PSCommandPath } |
    ConvertTo-Json | Set-Content -LiteralPath $env:CODEXORIG_ENTRY_MARKER -Encoding UTF8
Write-Output 'BUILD_OUTPUT_SENTINEL'
if ($env:CODEXORIG_ENTRY_FAIL -eq '1') { throw 'BUILD_FAILURE_SENTINEL' }
'@ | Set-Content -LiteralPath (Join-Path $scripts 'build.ps1') -Encoding UTF8

# Framework에 포함된 컴파일러로 파일 버전이 있는 작은 fixture를 만든다.
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$sourceTemplate = @'
using System;
using System.IO;
using System.Text;
using System.Reflection;
[assembly: AssemblyFileVersion("__MAJOR__.9.0.0")]
/// ---------------------------------------------------------------------
/// <summary>
/// name         : InventoryFixture
/// desc         : Emit deterministic vswhere JSON and record arguments.
/// author       : CodexOrig
/// create date  : 2026-10-18
/// update date  : 2026-10-18 CodexOrig : test-only native process fixture.
/// </summary>
/// ---------------------------------------------------------------------
internal static class InventoryFixture
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : Return controlled inventory or native failure without launching an IDE.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : preserve UTF-8 and argument boundaries.
    /// </summary>
    /// ---------------------------------------------------------------------
    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        File.WriteAllLines(Environment.GetEnvironmentVariable("CODEXORIG_ENTRY_ARGUMENTS"), args, Encoding.UTF8);
        Console.Write(File.ReadAllText(Environment.GetEnvironmentVariable("CODEXORIG_ENTRY_INVENTORY"), Encoding.UTF8));
        return Int32.Parse(Environment.GetEnvironmentVariable("CODEXORIG_ENTRY_NATIVE_EXIT") ?? "0");
    }
}
'@
$engines = @{}
$instances = @{}
foreach ($year in @('2017', '2019', '2022')) {
    $major = switch ($year) { '2017' { 15 } '2019' { 16 } '2022' { 17 } }
    $installation = Join-Path $fixture "VS $year"
    $relative = if ($year -eq '2017') { 'MSBuild\15.0\Bin\MSBuild.exe' } else { 'MSBuild\Current\Bin\MSBuild.exe' }
    $engine = Join-Path $installation $relative
    $null = New-Item -ItemType Directory -Path (Split-Path -Parent $engine) -Force
    $source = Join-Path $runDirectory "Fixture$major.cs"
    [IO.File]::WriteAllText($source, $sourceTemplate.Replace('__MAJOR__', [string]$major), $utf8)
    & $compiler /nologo /target:exe "/out:$engine" $source
    if ($LASTEXITCODE -ne 0) { throw "Fixture compilation failed: $LASTEXITCODE" }
    $engines[$year] = $engine
    $instances[$year] = [pscustomobject]@{
        installationPath = $installation; installationVersion = "$major.9.0.0"
        isComplete = $true; isLaunchable = $true
    }
}
Copy-Item -LiteralPath $engines['2017'] -Destination $vswhere

<#
-------------------------------------------------------------------------
name         : Set-TestInventory
desc         : 원본 IDE 설치 대신 fixture 인벤토리만 변경한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : 빈 배열과 단일 설치 JSON 생성.
-------------------------------------------------------------------------
#>
function Set-TestInventory {
    param([string]$Year = '2017', [switch]$Empty)
    $json = if ($Empty) { '[]' } else { '[' + ($instances[$Year] | ConvertTo-Json) + ']' }
    [IO.File]::WriteAllText($inventoryPath, $json, $utf8)
}

<#
-------------------------------------------------------------------------
name         : Invoke-TestEntry
desc         : 실제 .cmd 또는 현재 PowerShell 엔진에서 진입점을 격리 실행한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : 셸 재해석 최소화 및 stdout/stderr 동시 수집.
-------------------------------------------------------------------------
#>
function Invoke-TestEntry {
    param([string]$Arguments = '', [switch]$Batch, [string]$NativeExit = '0', [switch]$FailBuild)
    $info = New-Object Diagnostics.ProcessStartInfo
    if ($Batch) {
        $info.FileName = $env:ComSpec
        $info.Arguments = '/d /v:off /s /c ""' + (Join-Path $scripts 'Build-Vsix.cmd') + '" ' + $Arguments + '"'
    }
    else {
        $info.FileName = (Get-Process -Id $PID).Path
        $info.Arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
            (Join-Path $scripts 'Invoke-VsixBuild.ps1') + '" ' + $Arguments
    }
    $info.WorkingDirectory = $runDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = [Text.Encoding]::UTF8
    $info.StandardErrorEncoding = [Text.Encoding]::UTF8
    $info.EnvironmentVariables['CODEXORIG_ENTRY_INVENTORY'] = $inventoryPath
    $info.EnvironmentVariables['CODEXORIG_ENTRY_ARGUMENTS'] = $argumentsPath
    $info.EnvironmentVariables['CODEXORIG_ENTRY_MARKER'] = $markerPath
    $info.EnvironmentVariables['CODEXORIG_ENTRY_NATIVE_EXIT'] = $NativeExit
    $info.EnvironmentVariables['CODEXORIG_ENTRY_FAIL'] = $(if ($FailBuild) { '1' } else { '0' })
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $stdout.Result; Error = $stderr.Result }
    }
    finally { $process.Dispose() }
}

<#
-------------------------------------------------------------------------
name         : Assert-Entry
desc         : 테스트 결과를 저장하고 실패를 후속 검사와 함께 보고한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : 실제 종료 코드와 출력에 대한 검증 결과 기록.
-------------------------------------------------------------------------
#>
function Assert-Entry {
    param([string]$Name, [bool]$Condition, [object]$Detail)
    $results.Add([pscustomobject]@{ Name = $Name; Passed = $Condition; Detail = $Detail })
    Write-Host "$(if ($Condition) { 'PASS' } else { 'FAIL' }): $Name"
}

Set-TestInventory
$discovery = '-VsWherePath "' + $vswhere + '"'
$result = Invoke-TestEntry -Batch -Arguments "$discovery -PlanOnly"
$plan = if ($result.ExitCode -eq 0) { $result.Output | ConvertFrom-Json } else { $null }
Assert-Entry 'Batch default engine, Korean/special path, unrelated cwd' ($result.ExitCode -eq 0 -and $plan.MSBuildPath -eq $engines['2017']) $result
Assert-Entry 'Plan does not run build or IDE' ($null -ne $plan -and -not $plan.LaunchesIde -and -not $plan.ChecksIdeLicense -and -not (Test-Path -LiteralPath $markerPath)) $plan

foreach ($year in @('2019', '2022')) {
    Set-TestInventory -Year $year
    $result = Invoke-TestEntry -Arguments "$discovery -VisualStudioVersion $year -Configuration Debug -PlanOnly"
    $plan = if ($result.ExitCode -eq 0) { $result.Output | ConvertFrom-Json } else { $null }
    Assert-Entry "VS$year engine selection" ($result.ExitCode -eq 0 -and $plan.MSBuildPath -eq $engines[$year] -and $plan.Configuration -eq 'Debug') $result
}
$recorded = [IO.File]::ReadAllLines($argumentsPath)
Assert-Entry 'Native discovery arguments preserve range/products/components' ($recorded -contains '[17.0,18.0)' -and $recorded -contains 'Microsoft.VisualStudio.Product.Community' -and $recorded -contains 'Microsoft.VisualStudio.Component.VSSDK' -and $recorded -contains '-utf8') $recorded

Set-TestInventory -Empty
$result = Invoke-TestEntry -Arguments "$discovery -PlanOnly"
Assert-Entry 'Empty PS5 inventory fails without fallback' ($result.ExitCode -eq 1 -and ($result.Error -replace '\s', '') -match 'Noversionfallback') $result

Set-TestInventory
$result = Invoke-TestEntry -Arguments "$discovery -VisualStudioVersion 2019 -PlanOnly"
Assert-Entry 'Unexpected inventory version is rejected' ($result.ExitCode -eq 1) $result

$instances['2017'].isComplete = $false
Set-TestInventory
$result = Invoke-TestEntry -Arguments "$discovery -PlanOnly"
Assert-Entry 'Incomplete instance is rejected' ($result.ExitCode -eq 1) $result
$instances['2017'].isComplete = $true
Set-TestInventory

$result = Invoke-TestEntry -Arguments "$discovery -PlanOnly" -NativeExit '23'
Assert-Entry 'Native discovery failure is returned' ($result.ExitCode -eq 1 -and $result.Error -match '23') $result
[IO.File]::WriteAllText($inventoryPath, '{broken', $utf8)
$result = Invoke-TestEntry -Arguments "$discovery -PlanOnly"
Assert-Entry 'Malformed inventory is rejected' ($result.ExitCode -eq 1) $result
Set-TestInventory

$explicit = '-MSBuildPath "' + $engines['2017'] + '" -VsWherePath "missing.exe"'
$result = Invoke-TestEntry -Batch -Arguments "$explicit -PlanOnly"
Assert-Entry 'Explicit engine bypasses unavailable discovery' ($result.ExitCode -eq 0) $result
$result = Invoke-TestEntry -Arguments ('-MSBuildPath "' + $engines['2022'] + '" -PlanOnly')
Assert-Entry 'Wrong MSBuild file major version is rejected' ($result.ExitCode -eq 1 -and ($result.Error -replace '\s', '') -match 'requiresMSBuild15') $result
$result = Invoke-TestEntry -Arguments '-MSBuildPath "missing.exe" -PlanOnly'
Assert-Entry 'Missing engine is rejected' ($result.ExitCode -eq 1) $result

$result = Invoke-TestEntry -Batch -Arguments "$explicit -Configuration Debug"
$marker = if (Test-Path -LiteralPath $markerPath) { Get-Content -LiteralPath $markerPath -Raw -Encoding UTF8 | ConvertFrom-Json } else { $null }
Assert-Entry 'Batch forwards configuration/path and preserves build output' ($result.ExitCode -eq 0 -and $null -ne $marker -and $marker.Configuration -eq 'Debug' -and $marker.MSBuildPath -eq $engines['2017'] -and $result.Output -match 'BUILD_OUTPUT_SENTINEL') $result
$result = Invoke-TestEntry -Batch -Arguments $explicit -FailBuild
Assert-Entry 'Batch preserves failure status and error output' ($result.ExitCode -eq 1 -and ($result.Error -replace '\s', '') -match 'BUILD_FAILURE_SENTINEL' -and $result.Output -match 'BUILD_OUTPUT_SENTINEL') $result
$result = Invoke-TestEntry -Arguments '-VisualStudioVersion 2026 -PlanOnly'
Assert-Entry 'Unsupported year fails parameter validation' ($result.ExitCode -ne 0) $result

$failures = @($results | Where-Object { -not $_.Passed })
$report = [pscustomobject]@{
    CheckedAt = [DateTimeOffset]::Now.ToString('o'); PowerShell = $PSVersionTable.PSVersion.ToString()
    Passed = $results.Count - $failures.Count; Total = $results.Count; Tests = @($results.ToArray())
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDirectory 'results.json') -Encoding UTF8
Write-Host "$($report.Passed)/$($report.Total) PASS. Results: $runDirectory"
if ($failures.Count -gt 0) { exit 1 }
exit 0
