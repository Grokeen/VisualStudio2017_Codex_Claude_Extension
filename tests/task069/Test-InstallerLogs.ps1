#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-InstallerLogs
desc         : 실제 설치 helper를 로컬 native fixture로 호출해 TEMP 로그·인수·누락·실패 경계를 검증한다.
author       : CodexOrig
create date  : 2026-10-21
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/task069installerlogs/run-'+[guid]::NewGuid().ToString('N'))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Preserve existing log fixture evidence.'}
$null=New-Item -ItemType Directory -Path $output
$original=Join-Path $repo 'scripts/Install-AIAssistant.ps1'
$originalHash=(Get-FileHash -LiteralPath $original).Hash
$sourceCopy=Join-Path $output 'Install-AIAssistant.ps1'
Copy-Item -LiteralPath $original -Destination $sourceCopy
. $sourceCopy -LibraryOnly
$cases=New-Object 'Collections.Generic.List[object]'
function Check([string]$Name,[bool]$Passed){
    $cases.Add([pscustomobject]@{Name=$Name;Passed=$Passed})
    Write-Output ($(if($Passed){'PASS '}else{'FAIL '})+$Name)
    if(!$Passed){throw ('Log fixture failed: '+$Name)}
}
$utf8=New-Object Text.UTF8Encoding($false)
$stubDirectory=Join-Path $output 'native fixture with spaces';$null=New-Item -ItemType Directory -Path $stubDirectory
$stubSource=Join-Path $stubDirectory 'InstallerFixture.cs'
$stubExe=Join-Path $stubDirectory 'InstallerFixture.exe'
$nativeSource=@'
using System;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
internal static class InstallerFixture
{
    private static int Main(string[] args)
    {
        File.WriteAllText(Environment.GetEnvironmentVariable("TASK069_LOG_ARGV"), new JavaScriptSerializer().Serialize(args));
        string mode = Environment.GetEnvironmentVariable("TASK069_LOG_MODE");
        string value = args.Single(x => x.StartsWith("/logFile:", StringComparison.OrdinalIgnoreCase)).Substring(9);
        if (value != Path.GetFileName(value) || value.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return 91;
        if (mode != "missing" && mode != "missing-failure")
            File.WriteAllText(Path.Combine(Path.GetTempPath(), value), "Synthetic installer log; no IDE or official installer invoked.\r\n");
        return mode == "failure" || mode == "missing-failure" ? 7 : 0;
    }
}
'@
[IO.File]::WriteAllText($stubSource,$nativeSource,$utf8)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe ('/out:'+$stubExe) /reference:System.Core.dll /reference:System.Web.Extensions.dll $stubSource 2>&1|Tee-Object (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Private native fixture compilation failed.'}
$environmentNames=@('TEMP','TMP','TASK069_LOG_ARGV','TASK069_LOG_MODE')
$previous=@{};foreach($name in $environmentNames){$previous[$name]=[Environment]::GetEnvironmentVariable($name)}
$reportDirectory=Join-Path $output 'report folder with spaces';$null=New-Item -ItemType Directory -Path $reportDirectory
$reportPath=Join-Path $reportDirectory 'diagnostic report.json'
$packagePath=Join-Path $output 'package with spaces.vsix';[IO.File]::WriteAllText($packagePath,'synthetic package; not a VSIX',$utf8)
$observations=@();$completed=$false
function Run-Native([string]$Mode,[string]$Name){
    $env:TASK069_LOG_ARGV=Join-Path $output ($Name+'-argv.json')
    $env:TASK069_LOG_MODE=$Mode
    $stdout=Join-Path $output ($Name+'-stdout.log');$warnings=Join-Path $output ($Name+'-warnings.log')
    $all=@(& {Invoke-AIAssistantVsixInstaller -Installer $stubExe -InstanceId fixture_123 -PackagePath $packagePath -ReportPath $reportPath -TestRootSuffix CodexOrig36abcdef12} 6>&1 3>&1)
    $result=@($all|Where-Object {$null -ne $_.PSObject.Properties['ExitCode']})[0]
    $stdoutText=(@($all|Where-Object {$_ -is [Management.Automation.InformationRecord]}|ForEach-Object {$_.MessageData.Message}) -join [Environment]::NewLine)
    $warningText=(@($all|Where-Object {$_ -is [Management.Automation.WarningRecord]}|ForEach-Object Message) -join [Environment]::NewLine)
    [IO.File]::WriteAllText($stdout,$stdoutText,$utf8);[IO.File]::WriteAllText($warnings,$warningText,$utf8)
    $argv=Get-Content -LiteralPath $env:TASK069_LOG_ARGV -Raw|ConvertFrom-Json
    return [pscustomobject]@{Result=$result;Argv=$argv;Stdout=$stdoutText;Warnings=$warningText}
}
function Make-Source($Plan,[string]$Text='valid private fixture log'){
    [IO.File]::WriteAllText($Plan.SourcePath,$Text,$utf8)
}
try{
    $env:TEMP=Join-Path $output 'temp';$env:TMP=$env:TEMP;$null=New-Item -ItemType Directory -Path $env:TEMP
    $plan=New-AIAssistantLogPlan $reportPath
    Check 'Unique portable GUID basename and exact TEMP source' ($plan.Name -cmatch '^VSIXInstaller-[a-f0-9]{32}\.log$' -and $plan.SourcePath -ceq (Join-Path ([IO.Path]::GetTempPath()) $plan.Name) -and $plan.ArchivePath -ceq (Join-Path $reportDirectory $plan.Name))
    $first=Run-Native success 'default'
    $observations+=$first
    Check 'Native fixture receives supported basename, exact instance and package argv' ($first.Argv -contains ('/logFile:'+([IO.Path]::GetFileName($first.Result.RequestedInstallerLog))) -and $first.Argv -contains '/instanceIds:fixture_123' -and $first.Argv -contains $packagePath -and $first.Argv -contains '/rootSuffix:CodexOrig36abcdef12' -and $first.Argv -contains '/quiet')
    Check 'Successful exit has verified archive, SHA and retained TEMP source' ($first.Result.ExitCode -eq 0 -and $first.Result.LogAvailable -and $first.Result.LogCopied -and (Test-Path -LiteralPath $first.Result.InstallerLog) -and (Test-Path -LiteralPath $first.Result.InstallerLogSource) -and (Get-FileHash -LiteralPath $first.Result.InstallerLog).Hash -eq $first.Result.LogSha256 -and (Get-FileHash -LiteralPath $first.Result.InstallerLogSource).Hash -eq $first.Result.LogSha256)
    Check 'Stdout references verified existing archive only' ($first.Stdout.Contains($first.Result.InstallerLog) -and !$first.Stdout.Contains($first.Result.RequestedInstallerLog))
    $env:TEMP=Join-Path $output 'temp folder with spaces';$env:TMP=$env:TEMP;$null=New-Item -ItemType Directory -Path $env:TEMP
    $second=Run-Native success 'spaces';$observations+=$second
    Check 'TEMP and report directory spaces preserve logs and package argument' ($second.Result.ExitCode -eq 0 -and $second.Result.LogAvailable -and $second.Result.LogCopied -and $second.Argv -contains $packagePath -and $second.Result.InstallerLogSource.StartsWith($env:TEMP+'\'))
    Check 'Repeated execution uses a new GUID and preserves prior archive' ($first.Result.InstallerLog -cne $second.Result.InstallerLog -and (Get-FileHash -LiteralPath $first.Result.InstallerLog).Hash -eq $first.Result.LogSha256)
    $missing=Run-Native missing 'missing';$observations+=$missing
    Check 'Missing log does not turn exit zero into installation failure' ($missing.Result.ExitCode -eq 0 -and !$missing.Result.LogAvailable -and $null -eq $missing.Result.InstallerLog -and !$missing.Result.LogCopied -and $missing.Result.LogWarning)
    Check 'Missing log emits warning without claiming a nonexistent log path' (!$missing.Stdout.Contains($missing.Result.RequestedInstallerLog) -and $missing.Warnings.Length -gt 0 -and !(Test-Path -LiteralPath $missing.Result.RequestedInstallerLog))
    $failure=Run-Native failure 'failure';$observations+=$failure
    Check 'Failed installation preserves its exit seven and diagnostic log' ($failure.Result.ExitCode -eq 7 -and $failure.Result.LogAvailable -and $failure.Result.LogCopied -and (Test-Path -LiteralPath $failure.Result.InstallerLog))
    $missingFailure=Run-Native missing-failure 'missing-failure';$observations+=$missingFailure
    Check 'Failed installation and unavailable log are independent facts' ($missingFailure.Result.ExitCode -eq 7 -and !$missingFailure.Result.LogAvailable -and $null -eq $missingFailure.Result.InstallerLog)

    $stale=New-AIAssistantLogPlan $reportPath;Make-Source $stale
    [IO.File]::SetCreationTimeUtc($stale.SourcePath,[DateTime]::UtcNow.AddMinutes(-5));[IO.File]::SetLastWriteTimeUtc($stale.SourcePath,[DateTime]::UtcNow.AddMinutes(-5))
    $r=Complete-AIAssistantLog $stale
    Check 'Stale source is rejected without arbitrary TEMP log discovery' (!$r.LogAvailable -and $null -eq $r.InstallerLog -and !(Test-Path -LiteralPath $stale.ArchivePath))
    $future=New-AIAssistantLogPlan $reportPath;Make-Source $future
    [IO.File]::SetLastWriteTimeUtc($future.SourcePath,[DateTime]::UtcNow.AddMinutes(5));$r=Complete-AIAssistantLog $future
    Check 'Future timestamp source is rejected' (!$r.LogAvailable -and $null -eq $r.InstallerLog)
    $empty=New-AIAssistantLogPlan $reportPath;Make-Source $empty ''; $r=Complete-AIAssistantLog $empty
    Check 'Empty log is not announced as available' (!$r.LogAvailable -and $null -eq $r.InstallerLog)
    $large=New-AIAssistantLogPlan $reportPath;$stream=[IO.File]::Create($large.SourcePath);try{$stream.SetLength(16777217)}finally{$stream.Dispose()}
    $r=Complete-AIAssistantLog $large
    Check 'Over 16MB source is rejected before archive creation' (!$r.LogAvailable -and !(Test-Path -LiteralPath $large.ArchivePath))
    $boundary=New-AIAssistantLogPlan $reportPath;$stream=[IO.File]::Create($boundary.SourcePath);try{$stream.SetLength(16777216)}finally{$stream.Dispose()}
    $r=Complete-AIAssistantLog $boundary
    Check 'Exact 16MB source is copied and verified' ($r.LogAvailable -and $r.LogCopied -and $r.LogBytes -eq 16777216 -and (Get-Item -LiteralPath $r.InstallerLog).Length -eq 16777216)
    $outside=New-AIAssistantLogPlan $reportPath;$outside.SourcePath=Join-Path $reportDirectory $outside.Name;Make-Source $outside;$r=Complete-AIAssistantLog $outside
    Check 'Source outside exact TEMP path is rejected' (!$r.LogAvailable -and $null -eq $r.InstallerLog)
    $invalid=New-AIAssistantLogPlan $reportPath;$invalid.Name='../other.log';$r=Complete-AIAssistantLog $invalid
    Check 'Traversal and non-GUID log names are rejected' (!$r.LogAvailable -and $null -eq $r.InstallerLog)
    $existing=New-AIAssistantLogPlan $reportPath;Make-Source $existing;[IO.File]::WriteAllText($existing.ArchivePath,'preserve existing archive',$utf8)
    $oldHash=(Get-FileHash -LiteralPath $existing.ArchivePath).Hash;$r=Complete-AIAssistantLog $existing
    Check 'Archive collision preserves existing bytes and falls back to verified TEMP source' ($r.LogAvailable -and !$r.LogCopied -and $r.InstallerLog -ceq $existing.SourcePath -and $r.LogWarning -and (Get-FileHash -LiteralPath $existing.ArchivePath).Hash -eq $oldHash)
    $locked=New-AIAssistantLogPlan $reportPath;Make-Source $locked;$stream=[IO.File]::Open($locked.SourcePath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    try{$r=Complete-AIAssistantLog $locked}finally{$stream.Dispose()}
    Check 'Unreadable source becomes log warning rather than installation success claim' (!$r.LogAvailable -and $null -eq $r.InstallerLog)
    $same=New-AIAssistantLogPlan (Join-Path $env:TEMP 'same-directory-report.json');Make-Source $same;$r=Complete-AIAssistantLog $same
    Check 'Report in TEMP reports source without copying onto itself' ($r.LogAvailable -and !$r.LogCopied -and $r.InstallerLog -ceq $same.SourcePath -and !$r.LogWarning)
    $target=Join-Path $output 'owned reparse target';$junction=Join-Path $output 'owned junction';$null=New-Item -ItemType Directory -Path $target
    $null=New-Item -ItemType Junction -Path $junction -Target $target
    $rejected=$false;try{$null=New-AIAssistantLogPlan (Join-Path $junction 'report.json')}catch{$rejected=$true}
    Check 'Reparse report directory is rejected' $rejected
    $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($sourceCopy,[ref]$tokens,[ref]$errors)
    $invoke=$ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Invoke-AIAssistantVsixInstaller'},$true)[0].Extent.Text
    Check 'Interactive branch stays visible and only test suffix adds quiet and Hidden' ($errors.Count -eq 0 -and $invoke.Contains("else{"+'$process=Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru}') -and $invoke.Contains('-WindowStyle Hidden') -and $invoke.Contains("'/quiet /rootSuffix:'"))
    Check 'Product and frozen script copy remain byte-identical' ((Get-FileHash -LiteralPath $original).Hash -eq $originalHash -and (Get-FileHash -LiteralPath $sourceCopy).Hash -eq $originalHash)
    $completed=$cases.Count -eq 23
}finally{
    foreach($name in $environmentNames){[Environment]::SetEnvironmentVariable($name,$previous[$name])}
    [ordered]@{Task='TASK069 verified installer log paths';Succeeded=($completed -and @($cases|Where-Object {!$_.Passed}).Count -eq 0);ExpectedCount=23;Passed=@($cases|Where-Object Passed).Count;Failed=@($cases|Where-Object {!$_.Passed}).Count;Cases=$cases.ToArray();Observations=$observations;OriginalScript=$original;OriginalSha256=$originalHash;GuiStarted=$false;OfficialInstallerStarted=$false;IdeStarted=$false;TempOriginalsDeleted=$false;Scope='Actual product PowerShell helpers + private native subprocess; official installer follow-up belongs to Root.'}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
}
Write-Output ('Installer log fixture '+$cases.Count+'/'+$cases.Count+' PASS: '+$output)
