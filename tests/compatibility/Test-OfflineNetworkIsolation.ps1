#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-OfflineNetworkIsolation
desc         : 배포된 완성 ZIP의 설치·CLI 시작을 네트워크 capability 없는 Windows 토큰에서 확인한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-041 TCP 거부·복사·멱등·CLI 실행과 정리 결과를 분리 기록.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$BundleZip,[switch]$UseWorkingInstaller)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$BundleZip){$BundleZip=Join-Path $repo 'artifacts/task038/VisualStudio_AI_Assistant-0.11.1-FullOffline-win-x64.zip'}
$taskRoot=Join-Path $repo 'artifacts/task041'
$null=New-Item -ItemType Directory -Path $taskRoot -Force
$root=Join-Path $taskRoot ('run-'+[guid]::NewGuid().ToString('N'))
$null=New-Item -ItemType Directory -Path $root
[IO.File]::WriteAllText((Join-Path $root 'TASK041.marker'),[IO.Path]::GetFileName($root))
$root|Set-Content (Join-Path $taskRoot 'latest-network-run.txt') -Encoding UTF8
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory([IO.Path]::GetFullPath($BundleZip),(Join-Path $root 'bundle'))
if($UseWorkingInstaller){Copy-Item -LiteralPath (Join-Path $repo 'scripts/Install-OfflineCli.ps1') -Destination (Join-Path $root 'bundle/Install-OfflineCli.ps1')}
$support=Join-Path $root 'OfflineNetworkIsolation.dll'
Add-Type -Path (Join-Path $PSScriptRoot 'OfflineNetworkIsolation.cs') -OutputAssembly $support
Add-Type -Path $support
$addresses=@([Net.Dns]::GetHostAddresses('github.com')|Where-Object AddressFamily -EQ InterNetwork)
if(!$addresses.Count){throw 'A host-side TCP control address is required.'}
$address=$addresses[0].ToString()
$control=New-Object Net.Sockets.TcpClient
try{$connected=$control.ConnectAsync($address,443).Wait(5000);if(!$connected -or !$control.Connected){throw 'Host-side TCP control failed.'}}finally{$control.Dispose()}
@{Address=$address;Port=443;HostConnected=$true;HostToken=[OfflineNetworkIsolation]::TokenState();BundleZip=[IO.Path]::GetFullPath($BundleZip);BundleSha256=(Get-FileHash -LiteralPath $BundleZip).Hash}|ConvertTo-Json|Set-Content (Join-Path $root 'control.json') -Encoding UTF8
$child=@'
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$results=New-Object 'System.Collections.Generic.List[object]'
try {
 Add-Type -Path (Join-Path $root 'OfflineNetworkIsolation.dll')
 $token=[OfflineNetworkIsolation]::TokenState()
 if($token[0] -ne 1 -or $token[1] -ne 0){throw 'The worker is not a zero-capability AppContainer.'}
 $results.Add([pscustomobject]@{Name='Worker token is AppContainer with zero capabilities';Passed=$true;Token=$token})
 $control=Get-Content (Join-Path $root 'control.json') -Raw -Encoding UTF8|ConvertFrom-Json
 foreach($phase in @('BeforeInstallation','AfterInstallation')) {
  if($phase -eq 'AfterInstallation') {
   $bundle=Join-Path $root 'bundle'
   $install=Join-Path $bundle 'Install-OfflineCli.ps1'
   $destination=Join-Path $root 'installed'
   & $install -BundleDirectory $bundle -DestinationRoot $destination -ReportPath (Join-Path $root 'verify-only.json') *> (Join-Path $root 'verify-only.log')
   & $install -BundleDirectory $bundle -DestinationRoot $destination -Install -ReportPath (Join-Path $root 'installed.json') *> (Join-Path $root 'installed.log')
   & $install -BundleDirectory $bundle -DestinationRoot $destination -Install -ReportPath (Join-Path $root 'repeated.json') *> (Join-Path $root 'repeated.log')
   $receipt=Get-Content (Join-Path $root 'installed.json') -Raw -Encoding UTF8|ConvertFrom-Json
   $repeat=Get-Content (Join-Path $root 'repeated.json') -Raw -Encoding UTF8|ConvertFrom-Json
   if($receipt.Status -ne 'Installed' -or $receipt.FilesVerified -ne 46 -or $repeat.Status -ne 'AlreadyInstalled'){throw 'Offline installation receipts differ.'}
   $results.Add([pscustomobject]@{Name='Verify, install and idempotent reinstall succeed without networking';Passed=$true;Files=46})
   foreach($tool in @(@('Codex',$receipt.CodexExecutable,'^codex-cli 0\.163\.0'),@('Claude',$receipt.ClaudeExecutable,'^2\.1\.302 \(Claude Code\)'))) {
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$tool[1];$info.Arguments='--version';$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process;$process.StartInfo=$info
    try {
     if(!$process.Start()){throw 'Installed CLI process did not start.'}
     $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
     if(!$process.WaitForExit(15000)){$process.Kill();throw 'Installed CLI version timed out.'}
     $version=$stdout.GetAwaiter().GetResult().Trim()
     if($process.ExitCode -ne 0 -or $version -notmatch $tool[2]){throw ('Installed CLI did not start: '+$tool[0])}
    }finally{$process.Dispose()}
    $results.Add([pscustomobject]@{Name=($tool[0]+' starts under the same network-disabled token');Passed=$true;Version=$version})
   }
  }
  $client=New-Object Net.Sockets.TcpClient
  $denied=$false;$errorCode=0
  try{$client.Connect([string]$control.Address,443)}catch{$socketException=$_.Exception;while($socketException.InnerException){$socketException=$socketException.InnerException};if($socketException -is [Net.Sockets.SocketException]){$errorCode=$socketException.NativeErrorCode;$denied=($errorCode -eq 10013)}}finally{$client.Dispose()}
  if(!$denied){throw ('TCP was not denied with WSAEACCES: '+$phase+' / '+$errorCode)}
  $results.Add([pscustomobject]@{Name=($phase+' direct TCP is blocked by Windows');Passed=$true;NativeSocketError=$errorCode})
 }
 $results|ConvertTo-Json -Depth 7|Set-Content (Join-Path $root 'worker-results.json') -Encoding UTF8
 exit 0
}catch{($_|Format-List * -Force|Out-String)|Set-Content (Join-Path $root 'worker-failure.txt') -Encoding UTF8;$results|ConvertTo-Json -Depth 7|Set-Content (Join-Path $root 'worker-partial.json') -Encoding UTF8;exit 1}
'@
$script=Join-Path $root 'Worker.ps1';[IO.File]::WriteAllText($script,$child,[Text.UTF8Encoding]::new($true))
$powershell=Join-Path $env:WINDIR 'System32/WindowsPowerShell/v1.0/powershell.exe'
$exit=[OfflineNetworkIsolation]::Run($root,$powershell,('-NoProfile -ExecutionPolicy Bypass -File "'+$script+'"'),60000)
[pscustomobject]@{Task='TASK-041';OutputDirectory=$root;WorkerExitCode=$exit;WorkingInstallerUsed=[bool]$UseWorkingInstaller;AppContainerProfile=[OfflineNetworkIsolation]::LastProfile;ProfileCleanupHResult=[OfflineNetworkIsolation]::LastCleanup;FolderAclRestored=[OfflineNetworkIsolation]::LastAclRestored;HostNetworkSettingsChanged=$false;HostFirewallRulesChanged=$false;WorkerNetworkCapabilities=0;PhysicalCableDisconnected=$false;CustomerPcTested=$false;AuthenticationPerformed=$false;ModelRequestPerformed=$false}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $root 'summary.json') -Encoding UTF8
if($exit -ne 0){throw ('Network-isolated worker failed; evidence preserved in '+$root)}
if([OfflineNetworkIsolation]::LastCleanup -ne 0){throw 'Temporary AppContainer profile cleanup failed.'}
if(![OfflineNetworkIsolation]::LastAclRestored){throw 'Temporary test folder ACL differs.'}
Get-Content (Join-Path $root 'worker-results.json') -Raw -Encoding UTF8|ConvertFrom-Json|Format-Table Name,Passed,Version,NativeSocketError -AutoSize
Write-Host ('Network-isolated evidence: '+$root)
