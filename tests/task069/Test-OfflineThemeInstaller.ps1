#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-OfflineThemeInstaller
desc         : EXE 내장 패키지 무결성, VS 버전 선택, 테마 오류 차단 및 선택적 실제 IDE 격리 설치를 검사한다.
author       : CodexCode
create date  : 2026-10-20
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$InstallerExe,
    [string]$OutputDirectory,
    [string]$PackageDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.14.0',
    [switch]$ActualInstall
)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scriptPath=Join-Path $repo 'scripts/Install-AIAssistant.ps1'
$packageRoot=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $repo ('releases/v'+$PackageVersion)}
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/task069/test-'+[guid]::NewGuid().ToString('N'))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw '기존 테스트 결과를 보존합니다.'}
$null=New-Item -ItemType Directory -Path $output
. $scriptPath -LibraryOnly -RequireTheme -PackageVersion $PackageVersion
$results=New-Object 'System.Collections.Generic.List[object]'

<#
-------------------------------------------------------------------------
name         : Assert-Case
desc         : 기대 상태를 테스트 기록에 남기고 실패 시 다음 설치로 진행하지 않는다.
author       : CodexCode
create date  : 2026-10-20
-------------------------------------------------------------------------
#>
function Assert-Case([string]$Name,[bool]$Passed,[object]$Evidence){
    $results.Add([pscustomobject]@{Name=$Name;Passed=$Passed;Evidence=$Evidence})
    if(!$Passed){throw "검사 실패: $Name"}
    Write-Host "PASS $Name"
}

try{
    $assembly=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($InstallerExe))
    $resourceNames=@($assembly.GetManifestResourceNames())
    $sums=[IO.File]::ReadAllLines((Join-Path $packageRoot 'SHA256SUMS.txt'))
    foreach($line in $sums){
        if($line -notmatch '^([A-Fa-f0-9]{64})\s+\*?([^\\/]+\.vsix)$'){throw '체크섬 입력 형식이 다릅니다.'}
        $expected=$matches[1];$name=$matches[2]
        $resource='Payload.'+$name
        if($resourceNames -cnotcontains $resource){throw "EXE의 VSIX 리소스 누락: $name"}
        $stream=$assembly.GetManifestResourceStream($resource);$sha=[Security.Cryptography.SHA256]::Create()
        try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}
        finally{$sha.Dispose();$stream.Dispose()}
        Assert-Case ("Embedded VSIX $name") ($actual -eq $expected) $actual
    }
    Assert-Case 'Embedded installer script' ($resourceNames -ccontains 'Payload.Install-AIAssistant.ps1') 'present'
    Assert-Case 'Embedded checksum list' ($resourceNames -ccontains 'Payload.SHA256SUMS.txt') 'present'
    foreach($case in @(
        @{Name='VS2017 15.7.1 selects legacy AI and VS2017 theme';Version='15.7.27703.2000';Product='Professional';Arch='x86';Expected='Ready';Flavor='2017-15.7'},
        @{Name='VS2017 15.9 selects standard AI and VS2017 theme';Version='15.9.37627.17';Product='Community';Arch='x86';Expected='Ready';Flavor='2017'},
        @{Name='VS2019 selects matching AI and theme';Version='16.11.37627.13';Product='Enterprise';Arch='x86';Expected='Ready';Flavor='2019'},
        @{Name='VS2022 x64 selects matching AI and theme';Version='17.14.37806.7';Product='Community';Arch='amd64';Expected='Ready';Flavor='2022'},
        @{Name='VS2022 ARM64 rejected';Version='17.14.37806.7';Product='Community';Arch='arm64';Expected='ArchitectureMismatch';Flavor=''}
    )){
        $instance=[pscustomobject]@{instanceId='synthetic';productId=('Microsoft.VisualStudio.Product.'+$case.Product);displayName=$case.Name;installationVersion=$case.Version;installationPath='C:\Fixture';isComplete=$true;isLaunchable=$true;isRebootRequired=$false}
        $plan=Get-AIAssistantInstallPlan -Instance $instance -Directory $packageRoot -CoreEditorPresent $true -Architecture $case.Arch
        $passed=$plan.Status -eq $case.Expected
        if($case.Expected -eq 'Ready'){$passed=$passed -and $plan.Package.Flavor -eq $case.Flavor -and $plan.ThemePackage.Year -eq $case.Flavor.Substring(0,4)}
        Assert-Case $case.Name $passed ([pscustomobject]@{Status=$plan.Status;AI=$plan.Package.Path;Theme=$(if($plan.ThemePackage){$plan.ThemePackage.Path}else{$null})})
    }
    $fixture=Join-Path $output 'tampered-theme';$null=New-Item -ItemType Directory -Path $fixture
    foreach($name in @("VS2019.AIAssistant-$PackageVersion.vsix","Grokeen-VisualStudio-Themes-$PackageVersion-VS2019.vsix",'SHA256SUMS.txt')){Copy-Item -LiteralPath (Join-Path $packageRoot $name) -Destination $fixture}
    Add-Content -LiteralPath (Join-Path $fixture "Grokeen-VisualStudio-Themes-$PackageVersion-VS2019.vsix") -Value 'tamper' -NoNewline
    $instance=[pscustomobject]@{instanceId='synthetic';productId='Microsoft.VisualStudio.Product.Community';displayName='fixture';installationVersion='16.11.37627.13';installationPath='C:\Fixture';isComplete=$true;isLaunchable=$true;isRebootRequired=$false}
    $plan=Get-AIAssistantInstallPlan -Instance $instance -Directory $fixture -CoreEditorPresent $true -Architecture 'x86'
    Assert-Case 'Tampered theme blocks installer plan' ($plan.Status -eq 'InvalidThemePackage') $plan.Reason

    $preflight=Join-Path $output 'real-preflight.json'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -PackageDirectory $packageRoot -PackageVersion $PackageVersion -RequireTheme -ReportPath $preflight *> (Join-Path $output 'preflight.log')
    $exit=$LASTEXITCODE;$report=Get-Content -LiteralPath $preflight -Raw -Encoding UTF8|ConvertFrom-Json
    Assert-Case 'Real installed VS inventory with themes' ($exit -eq 0 -and @($report.Plans|Where-Object Status -EQ 'Ready').Count -ge 1) @($report.Plans|Select-Object InstanceId,InstallationVersion,Status)
    if($ActualInstall){
        $suffix='CodexOrig36'+[guid]::NewGuid().ToString('N').Substring(0,8)
        $installReport=Join-Path $output 'actual-install.json'
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -PackageDirectory $packageRoot -PackageVersion $PackageVersion -RequireTheme -Install -InstallTheme -AllReady -TestRootSuffix $suffix -ReportPath $installReport *> (Join-Path $output 'actual-install.log')
        $exit=$LASTEXITCODE;$data=Get-Content -LiteralPath $installReport -Raw -Encoding UTF8|ConvertFrom-Json
        $ready=@($data.Plans|Where-Object Status -EQ 'Ready').Count
        $installed=@($data.InstallationResults)
        Assert-Case 'Official isolated VSIXInstaller installs AI and themes' ($exit -eq 0 -and $installed.Count -eq 2*$ready -and @($installed|Where-Object ExitCode -NE 0).Count -eq 0) ([pscustomobject]@{Suffix=$suffix;Expected=2*$ready;Actual=$installed.Count;Results=$installed})
    }
}finally{
    [pscustomobject]@{Succeeded=@($results|Where-Object Passed -EQ $false).Count -eq 0;Count=$results.Count;ActualInstall=$ActualInstall.IsPresent;Cases=@($results.ToArray())}|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
}
Write-Host "검사 $($results.Count)/$($results.Count) PASS: $output"
