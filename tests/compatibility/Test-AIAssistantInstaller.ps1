#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-AIAssistantInstaller
desc         : 배포 VSIX로 설치 하한·에디션·아키텍처·무결성 및 읽기 전용 진단을 검증한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-036 고객 15.7.1 전용 선택과 15.7/15.9 경계 회귀 추가.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scriptPath=Join-Path $repo 'scripts/Install-AIAssistant.ps1'
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repo ('artifacts/installer/run-'+[guid]::NewGuid().ToString('N')) }
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$packageRoot=Join-Path $repo 'releases/v0.11.1'
. $scriptPath -LibraryOnly
$cases=New-Object 'System.Collections.Generic.List[object]'

<#
-------------------------------------------------------------------------
name         : Assert-InstallerPlan
desc         : 합성 설치 인스턴스를 실제 배포 매니페스트와 대조한다. IDE 설치 성공으로 확대하지 않는다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 독립 expected 결과와 각 실패 원인을 보존.
-------------------------------------------------------------------------
#>
function Assert-InstallerPlan {
    param([string]$Name,[string]$Version,[string]$Product,[string]$Expected,[string]$Architecture='x86',[bool]$Core=$true,[bool]$Complete=$true,[bool]$Reboot=$false,[string]$Directory=$packageRoot)
    $instance=[pscustomobject]@{ instanceId='test_123'; productId="Microsoft.VisualStudio.Product.$Product"; displayName="Fixture $Product"; installationVersion=$Version; installationPath='C:\Fixture VS'; isComplete=$Complete; isLaunchable=$true; isRebootRequired=$Reboot }
    $plan=Get-AIAssistantInstallPlan $instance $Directory $Core $Architecture
    $passed=$plan.Status -eq $Expected
    if($passed -and $Expected -eq 'Ready' -and $Name -like '*legacy*') { $passed=$plan.Package.Flavor -eq '2017-15.7' }
    if($passed -and $Expected -eq 'Ready' -and $Name -like '*15.9*') { $passed=$plan.Package.Flavor -eq '2017' }
    $cases.Add([pscustomobject]@{Name=$Name;Expected=$Expected;Actual=$plan.Status;Passed=$passed;Reason=$plan.Reason})
    if (!$passed) { throw "$Name : expected $Expected, got $($plan.Status): $($plan.Reason)" }
    Write-Host "PASS $Name"
}
try {
    foreach($edition in @('Community','Professional','Enterprise')) {
        Assert-InstallerPlan "2017 $edition customer 15.7.1 legacy package" '15.7.27703.2000' $edition 'Ready'
        Assert-InstallerPlan "2017 $edition 15.8 selects legacy" '15.8.28010.0' $edition 'Ready'
        Assert-InstallerPlan "2017 $edition exact 15.9 RTM" '15.9.28307.0' $edition 'Ready'
        Assert-InstallerPlan "2017 $edition latest installed" '15.9.37627.17' $edition 'Ready'
        Assert-InstallerPlan "2019 $edition matching package" '16.11.37627.13' $edition 'Ready'
        Assert-InstallerPlan "2022 $edition matching x64 package" '17.14.37806.7' $edition 'Ready' 'amd64'
    }
    Assert-InstallerPlan 'Below 15.7 SDK build rejected' '15.6.27702.99' 'Professional' 'UnsupportedVersion'
    Assert-InstallerPlan 'BuildTools is not an IDE' '15.9.37627.17' 'BuildTools' 'UnsupportedProduct'
    Assert-InstallerPlan 'VS2026 not declared' '18.0.10000.0' 'Professional' 'UnsupportedProduct' 'amd64'
    Assert-InstallerPlan 'Missing CoreEditor' '15.9.37627.17' 'Professional' 'MissingCoreEditor' 'x86' $false
    Assert-InstallerPlan 'Incomplete VS installation' '15.9.37627.17' 'Professional' 'IncompleteInstallation' 'x86' $true $false
    Assert-InstallerPlan 'Pending reboot' '15.9.37627.17' 'Professional' 'IncompleteInstallation' 'x86' $true $true $true
    Assert-InstallerPlan 'ARM64 IDE must not receive x64 package' '17.14.37806.7' 'Professional' 'ArchitectureMismatch' 'arm64'
    Assert-InstallerPlan 'Unknown executable architecture fails closed' '17.14.37806.7' 'Professional' 'ArchitectureMismatch' 'unknown'

    $fixture=Join-Path $OutputDirectory 'altered-package'
    $null=New-Item -ItemType Directory -Path $fixture
    Assert-InstallerPlan 'Missing year package' '15.9.37627.17' 'Professional' 'InvalidPackage' -Directory $fixture
    $fixtureFile=Join-Path $fixture 'VS2017.AIAssistant-0.11.1.vsix'
    Copy-Item -LiteralPath (Join-Path $packageRoot 'VS2019.AIAssistant-0.11.1.vsix') -Destination $fixtureFile
    ((Get-FileHash -LiteralPath $fixtureFile).Hash+'  '+[IO.Path]::GetFileName($fixtureFile)) | Set-Content -LiteralPath (Join-Path $fixture 'SHA256SUMS.txt') -Encoding ASCII
    Assert-InstallerPlan 'Renamed 2019 VSIX rejected by real identity' '15.9.37627.17' 'Professional' 'InvalidPackage' -Directory $fixture
    ('0'*64+'  '+[IO.Path]::GetFileName($fixtureFile)) | Set-Content -LiteralPath (Join-Path $fixture 'SHA256SUMS.txt') -Encoding ASCII
    Assert-InstallerPlan 'Tampered package hash rejected' '15.9.37627.17' 'Professional' 'InvalidPackage' -Directory $fixture
    $line=(Get-FileHash -LiteralPath $fixtureFile).Hash+'  '+[IO.Path]::GetFileName($fixtureFile)
    @($line,$line) | Set-Content -LiteralPath (Join-Path $fixture 'SHA256SUMS.txt') -Encoding ASCII
    Assert-InstallerPlan 'Duplicate checksum entry rejected' '15.9.37627.17' 'Professional' 'InvalidPackage' -Directory $fixture

    $reportPath=Join-Path $OutputDirectory 'local-inventory.json'
    $previousPreference=$ErrorActionPreference
    try { $ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -PackageDirectory $packageRoot -ReportPath $reportPath *> (Join-Path $OutputDirectory 'local-inventory.log') }
    finally { $ErrorActionPreference=$previousPreference }
    if ($LASTEXITCODE -ne 0) { throw "Local inventory failed. See $OutputDirectory" }
    $report=Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $ready=@($report.Plans | Where-Object Status -EQ 'Ready')
    if ($ready.Count -ne 3 -or $report.CheckKind -ne 'InstallationPreflight') { throw 'Expected three real local ready instances.' }
    $cases.Add([pscustomobject]@{Name='Real local inventory, diagnostic only';Passed=$true;Actual=$ready.Count;Expected=3})
    $before=(Get-FileHash -LiteralPath $reportPath).Hash
    try { $ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -PackageDirectory $packageRoot -ReportPath $reportPath *> (Join-Path $OutputDirectory 'no-overwrite.log') }
    finally { $ErrorActionPreference=$previousPreference }
    if ($LASTEXITCODE -ne 1 -or (Get-FileHash -LiteralPath $reportPath).Hash -ne $before) { throw 'Existing report must not be overwritten.' }
    $cases.Add([pscustomobject]@{Name='Existing report is preserved';Passed=$true;Actual='Exit1/unchanged';Expected='Exit1/unchanged'})

    # 오래된 vswhere의 -utf8 거부와 한글 설치경로를 별도 프로세스로 재현한다.
    $stubSource=Join-Path $OutputDirectory 'OldVswhereFixture.cs'
    $stubExecutable=Join-Path $OutputDirectory 'OldVswhereFixture.exe'
    @'
using System;
using System.Linq;
using System.Reflection;
[assembly: AssemblyFileVersion("2.4.0.0")]
class OldVswhereFixture {
    static int Main(string[] args) {
        if (args.Contains("-utf8")) return 87;
        if (args.Contains("-requires")) { Console.WriteLine("[]"); return 0; }
        Console.WriteLine("[{\"instanceId\":\"old_157\",\"installationPath\":\"C:\\\\검증 VS 2017\\\\Professional\"}]");
        return 0;
    }
}
'@ | Set-Content -LiteralPath $stubSource -Encoding UTF8
    & (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /codepage:65001 /reference:System.Core.dll "/out:$stubExecutable" $stubSource
    if($LASTEXITCODE -ne 0){throw 'Old vswhere fixture compile failed.'}
    $oldCodePage=[Console]::OutputEncoding.CodePage
    $oldInstances=@(Get-AIAssistantVisualStudioInstances $stubExecutable)
    if($oldInstances.Count -ne 1 -or $oldInstances[0].installationPath -notlike '*검증 VS 2017*' -or [Console]::OutputEncoding.CodePage -ne $oldCodePage){throw 'Old vswhere Unicode/encoding restore failed.'}
    $cases.Add([pscustomobject]@{Name='Old vswhere without utf8 option preserves Unicode and console encoding';Passed=$true})
    $empty=@(Get-AIAssistantVisualStudioInstances $stubExecutable -RequireCoreEditor)
    if($empty.Count -ne 0){throw 'Empty old vswhere inventory was not preserved.'}
    $cases.Add([pscustomobject]@{Name='Old vswhere missing CoreEditor returns no instances';Passed=$true})

    $badDirectory=Join-Path $OutputDirectory 'wrong-explicit-folder';$null=New-Item -ItemType Directory -Path $badDirectory
    $badReport=Join-Path $OutputDirectory 'explicit-folder.json'
    try { $ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -PackageDirectory $badDirectory -ReportPath $badReport *> (Join-Path $OutputDirectory 'explicit-folder.log') }
    finally { $ErrorActionPreference=$previousPreference }
    $bad=Get-Content -LiteralPath $badReport -Raw|ConvertFrom-Json
    if($bad.PackageDirectory -ne [IO.Path]::GetFullPath($badDirectory) -or @($bad.Plans|Where-Object Status -EQ 'Ready').Count){throw 'Explicit package directory silently fell back.'}
    $cases.Add([pscustomobject]@{Name='Explicit missing package directory does not fall back';Passed=$true})
} finally {
    [pscustomobject]@{Task='TASK-036';TestKind='RealPackageWithSyntheticSkuAndReadOnlyLocalInventory';Cases=@($cases.ToArray());Count=$cases.Count;ActualProfessionalIdeTested=$false;CustomerVersion='15.7.1';CustomerCause='OldPackageMinimum15.9;NewLegacyMinimum15.7'} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding UTF8
}
Write-Host "$($cases.Count)/$($cases.Count) PASS; evidence: $OutputDirectory"
