#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-InstallerCancellation
desc         : 사용자가 IDE 종료 대기 중 중지를 요청하면 공식 설치기 시작 없이 종료하는지 검사한다.
author       : CodexCode
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 최종 배포 버전·패키지 디렉터리를 인수로 받아 검사한다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory,[string]$PackageDirectory,[ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.14.0')
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/task069/cancel-'+[guid]::NewGuid().ToString('N'))}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw '기존 중지 시험 결과를 보존합니다.'}
$null=New-Item -ItemType Directory -Path $output
$source=Join-Path $output 'devenv.cs'
$dummy=Join-Path $output 'devenv.exe'
[IO.File]::WriteAllText($source,'class Stub { static void Main() { System.Threading.Thread.Sleep(30000); } }',(New-Object Text.UTF8Encoding($false)))
$csc=Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
& $csc /nologo /target:exe ('/out:'+$dummy) $source
if($LASTEXITCODE -ne 0){throw '가상 IDE 프로세스 컴파일 실패.'}
$stub=$null;$installer=$null;$started=[DateTimeOffset]::Now
try{
    $stub=Start-Process -FilePath $dummy -WindowStyle Hidden -PassThru
    $script=Join-Path $repo 'scripts/Install-AIAssistant.ps1'
    $packageRoot=if($PackageDirectory){[IO.Path]::GetFullPath($PackageDirectory)}else{Join-Path $repo ('releases/v'+$PackageVersion)}
    $report=Join-Path $output 'report.json';$flag=Join-Path $output 'cancel.flag'
    $installerArguments='-NoProfile -ExecutionPolicy Bypass -File "'+$script+'" -PackageDirectory "'+$packageRoot+'" -PackageVersion '+$PackageVersion+' -RequireTheme -Install -InstallTheme -AllReady -WaitForIdeExit -TestRootSuffix CodexOrig36abcdef12 -CancelFlagPath "'+$flag+'" -ReportPath "'+$report+'" -CompactOutput'
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
    $start.Arguments=$installerArguments;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $installer=[Diagnostics.Process]::Start($start)
    $deadline=[DateTime]::UtcNow.AddSeconds(15)
    while(!(Test-Path -LiteralPath $report) -and !$installer.HasExited -and [DateTime]::UtcNow -lt $deadline){Start-Sleep -Milliseconds 100}
    if(!(Test-Path -LiteralPath $report)){throw '설치 사전 진단 보고서가 생성되지 않았습니다.'}
    Start-Sleep -Milliseconds 500
    if($installer.HasExited){throw '중지 요청 전에 설치 과정이 종료됐습니다.'}
    [IO.File]::WriteAllText($flag,'stop',[Text.Encoding]::ASCII)
    if(!$installer.WaitForExit(10000)){throw '중지 요청 후 10초 안에 종료되지 않았습니다.'}
    $data=Get-Content -LiteralPath $report -Raw -Encoding UTF8|ConvertFrom-Json
    $logs=@(Get-ChildItem -LiteralPath $output -Filter 'VSIXInstaller-*.log' -File)
    $passed=$installer.ExitCode -eq 2 -and $data.Cancelled -and @($data.InstallationResults).Count -eq 0 -and $logs.Count -eq 0
    $result=[pscustomobject]@{Succeeded=$passed;ExitCode=$installer.ExitCode;Cancelled=$data.Cancelled;InstallationCount=@($data.InstallationResults).Count;VsixInstallerLogCount=$logs.Count;ElapsedSeconds=[Math]::Round(([DateTimeOffset]::Now-$started).TotalSeconds,2);StubPid=$stub.Id;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
    $result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
    if(!$passed){throw '중지 요청이 VSIX 실행 전에 안전하게 처리되지 않았습니다.'}
    $result
}finally{
    if($installer -and !$installer.HasExited){$installer.Kill();$null=$installer.WaitForExit(5000)}
    if($stub -and !$stub.HasExited){
        $live=Get-Process -Id $stub.Id -ErrorAction SilentlyContinue
        if($live -and [string]::Equals($live.Path,$dummy,[StringComparison]::OrdinalIgnoreCase)){$stub.Kill();$null=$stub.WaitForExit(5000)}
    }
    if($stub){$stub.Dispose()};if($installer){$installer.Dispose()}
}
