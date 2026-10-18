#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-TestLauncher
desc         : 실행기 CLI·실패 전파·선택 VSIX·하위 프로세스 정리와 GUI 초기 상태를 격리 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 실제 제품 파일과 기존 검사 스크립트는 변경하지 않는다.
update date  : 2026-10-18 CodexOrig : TASK-018 드롭다운·이미지 요구 및 기본14개 검사 반영.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$launcherRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$launcherOriginal = Join-Path $launcherRepository 'VisualStudio_AIChat_ExtentionTest.exe'
$smokeDir = Join-Path $launcherRepository ('artifacts\ls\' + [guid]::NewGuid().ToString('N').Substring(0,10))
$fixtureDir = Join-Path $smokeDir 'fixture'
$fixtureLauncherDir = Join-Path $fixtureDir 'tests\launcher'
$fixtureCompatibilityDir = Join-Path $fixtureDir 'tests\compatibility'
$fixtureMarkdownDir = Join-Path $fixtureDir 'tests\workspace'
foreach ($path in @($fixtureLauncherDir,$fixtureCompatibilityDir,$fixtureMarkdownDir)) { $null = New-Item -ItemType Directory -Path $path -Force }
$fixtureExe = Join-Path $fixtureDir 'VisualStudio_AIChat_ExtentionTest.exe'
Copy-Item -LiteralPath $launcherOriginal -Destination $fixtureExe
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Invoke-LauncherSuite.ps1') -Destination $fixtureLauncherDir
$utf8 = New-Object Text.UTF8Encoding($true)
$smokeResults = New-Object 'System.Collections.Generic.List[object]'

<#
-------------------------------------------------------------------------
name         : Invoke-LauncherProbe
desc         : 셸 없이 지정한 자기 fixture 프로세스를 실행하고 출력·종료 코드를 수집한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 출력 교착과 무한 대기를 제한한다.
-------------------------------------------------------------------------
#>
function Invoke-LauncherProbe {
    param([string]$Executable,[string]$Arguments)
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $Executable; $start.Arguments = $Arguments; $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.StandardOutputEncoding = New-Object Text.UTF8Encoding($false); $start.StandardErrorEncoding = New-Object Text.UTF8Encoding($false)
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $start
    try {
        $null = $process.Start(); $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw '자기 launcher fixture 검사 제한 시간 초과' }
        [pscustomobject]@{ ExitCode=$process.ExitCode; Output=$stdout.GetAwaiter().GetResult(); Error=$stderr.GetAwaiter().GetResult() }
    } finally { $process.Dispose() }
}

<#
-------------------------------------------------------------------------
name         : Assert-LauncherProbe
desc         : 스모크 결과를 기록하고 기대와 다른 종료 상태는 즉시 실패 처리한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 결과 JSON과 콘솔 기록을 함께 생성한다.
-------------------------------------------------------------------------
#>
function Assert-LauncherProbe {
    param([string]$Name,[bool]$Passed,$Evidence)
    $smokeResults.Add([pscustomobject]@{ Name=$Name; Passed=$Passed; Evidence=$Evidence })
    if (!$Passed) { throw "FAIL $Name : $Evidence" }; Write-Output "PASS $Name"
}

$result = Invoke-LauncherProbe $launcherOriginal '--list'
Assert-LauncherProbe 'CLI lists fourteen default and two opt-in suites' ($result.ExitCode -eq 0 -and ([regex]::Matches($result.Output,'\tdefault')).Count -eq 14 -and ([regex]::Matches($result.Output,'\topt-in')).Count -eq 2) $result
$result = Invoke-LauncherProbe $launcherOriginal '--run compatibility'
Assert-LauncherProbe 'Real compatibility suite succeeds through launcher' ($result.ExitCode -eq 0 -and $result.Output.Contains('44/44 passed.')) $result
$result = Invoke-LauncherProbe $fixtureExe '--run unknown-suite'
Assert-LauncherProbe 'Unknown suite rejected' ($result.ExitCode -eq 2) $result
$fixtureScript = Join-Path $fixtureCompatibilityDir 'Test-VsixCompatibility.Tests.ps1'
[IO.File]::WriteAllText($fixtureScript, "Write-Output 'EXPLICIT_EXIT_FIXTURE'`r`nexit 7`r`n", $utf8)
$result = Invoke-LauncherProbe $fixtureExe '--run compatibility'
Assert-LauncherProbe 'Script exit seven becomes failed suite and launcher exit one' ($result.ExitCode -eq 1 -and $result.Output.Contains('종료 코드 7')) $result
$ps = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$result = Invoke-LauncherProbe $ps ('-NoProfile -STA -ExecutionPolicy Bypass -File "' + (Join-Path $fixtureLauncherDir 'Invoke-LauncherSuite.ps1') + '" -Suite compatibility')
Assert-LauncherProbe 'PowerShell wrapper preserves exact script exit seven' ($result.ExitCode -eq 7) $result
$result = Invoke-LauncherProbe $fixtureExe '--run markdown --vsix missing.vsix'
Assert-LauncherProbe 'Explicit missing VSIX rejected without fallback' ($result.ExitCode -ne 0 -and $result.Output.Contains('missing.vsix')) $result

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$chosenVsix = Join-Path $fixtureDir 'chosen.vsix'
$archive = [IO.Compression.ZipFile]::Open($chosenVsix,[IO.Compression.ZipArchiveMode]::Create)
try { $entry=$archive.CreateEntry('VS2017.AIAssistant.dll'); $stream=$entry.Open(); try { $bytes=[Text.Encoding]::UTF8.GetBytes('CHOSEN_PACKAGE'); $stream.Write($bytes,0,$bytes.Length) } finally { $stream.Dispose() } } finally { $archive.Dispose() }
$decoyRelease = Join-Path $fixtureDir 'releases\v0.8.0'; $null=New-Item -ItemType Directory -Path $decoyRelease -Force
[IO.File]::WriteAllText((Join-Path $decoyRelease 'VS2017.AIAssistant.dll'),'WRONG_DEFAULT',$utf8)
[IO.File]::WriteAllText((Join-Path $fixtureMarkdownDir 'Test-MarkdownWorkspace.ps1'), 'param([string]$AssemblyPath)' + "`r`n" + '[IO.File]::ReadAllText($AssemblyPath)' + "`r`n", $utf8)
$result = Invoke-LauncherProbe $fixtureExe '--run markdown --vsix chosen.vsix'
Assert-LauncherProbe 'Explicit VSIX payload wins over release DLL decoy' ($result.ExitCode -eq 0 -and $result.Output.Contains('CHOSEN_PACKAGE') -and !$result.Output.Contains('WRONG_DEFAULT')) $result

$marker = Join-Path $fixtureDir 'child-pid.txt'
[IO.File]::WriteAllText($fixtureScript, '[IO.File]::WriteAllText((Join-Path $PSScriptRoot ''..\..\child-pid.txt''),[string]$PID)' + "`r`nStart-Sleep -Seconds 30`r`n", $utf8)
$owned = Start-Process -FilePath $fixtureExe -ArgumentList '--run compatibility' -WindowStyle Hidden -PassThru
try {
    $deadline=[DateTime]::UtcNow.AddSeconds(10)
    while (!(Test-Path -LiteralPath $marker) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 50 }
    if (!(Test-Path -LiteralPath $marker)) { throw '자기 child PID marker가 생성되지 않았습니다.' }
    $childId=[int][IO.File]::ReadAllText($marker); $owned.Kill(); $owned.WaitForExit()
    $child = Get-Process -Id $childId -ErrorAction SilentlyContinue
    $gone = !$child
    if ($child) { $gone=$child.WaitForExit(3000); $child.Dispose() }
    Assert-LauncherProbe 'Launcher exit closes its own child process job' $gone $childId
} finally { if (!$owned.HasExited) { $owned.Kill() }; $owned.Dispose() }

$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$uiExe=Join-Path $smokeDir 'LauncherUiSmoke.exe'; $uiPng=Join-Path $smokeDir 'launcher-dark.png'
& $compiler /nologo /target:exe /langversion:5 "/out:$uiExe" /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll (Join-Path $PSScriptRoot 'LauncherUiSmoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'GUI smoke compile failed' }
$result=Invoke-LauncherProbe $uiExe ('"' + $fixtureExe + '" "' + $uiPng + '"')
Assert-LauncherProbe 'GUI starts idle and renders black interface' ($result.ExitCode -eq 0 -and (Test-Path -LiteralPath $uiPng)) $result
$report=[pscustomobject]@{ CheckedAt=[DateTimeOffset]::Now.ToString('o'); LauncherHash=(Get-FileHash -LiteralPath $launcherOriginal -Algorithm SHA256).Hash; Passed=$smokeResults.Count; Tests=@($smokeResults.ToArray()) }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $smokeDir 'results.json') -Encoding UTF8
Write-Output "Launcher smoke $($smokeResults.Count)/$($smokeResults.Count) PASS: $smokeDir"
