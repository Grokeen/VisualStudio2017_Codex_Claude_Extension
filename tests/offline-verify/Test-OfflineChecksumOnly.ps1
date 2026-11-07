[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
foreach($module in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')){Import-Module -Name (Join-Path $PSHOME ('Modules/'+$module+'/'+$module+'.psd1')) -ErrorAction Stop}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a new output directory.'}
[void][IO.Directory]::CreateDirectory($output)
$verifier=Join-Path $repo 'scripts/Verify-OfflineFiles.ps1'
$batch=Join-Path $repo 'VerifyOfflineSetup.bat'
$results=@()
$runtimeVersions=@{}
foreach($shell in @('powershell.exe','pwsh.exe')){
    $runtimeVersions[$shell]=(& $shell -NoProfile -Command '$PSVersionTable.PSVersion.ToString()')
    if($LASTEXITCODE -ne 0){throw "Could not identify runtime: $shell"}
}
function Invoke-OwnedProcess([string]$exe,[string[]]$arguments,[string]$rawArguments){
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$exe;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    if($rawArguments){$start.Arguments=$rawArguments}else{
        $start.Arguments=(@($arguments|ForEach-Object {if($_.Contains('"')){throw 'Unexpected quote in synthetic test argument.'};'"'+($_ -replace '(\\+)$','$1$1')+'"'}) -join ' ')
    }
    $process=New-Object Diagnostics.Process;$process.StartInfo=$start
    try{
        if(!$process.Start()){throw 'Synthetic process failed to start.'}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(30000)){$process.Kill();$process.WaitForExit();throw 'Synthetic process timed out.'}
        $process.WaitForExit()
        return [pscustomobject]@{ExitCode=$process.ExitCode;Output=@($stdout.GetAwaiter().GetResult(),$stderr.GetAwaiter().GetResult())}
    }finally{$process.Dispose()}
}
function New-Fixture([string]$name){
    $root=Join-Path $output $name
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'cli'))
    $items=@()
    $paths=@('cli/codex/codex-package.json','cli/codex/bin/codex.exe','cli/codex/bin/codex-code-mode-host.exe','cli/codex/codex-path/rg.exe','cli/codex/codex-resources/codex-command-runner.exe','cli/codex/codex-resources/codex-windows-sandbox-setup.exe','cli/claude/claude.exe')
    for($i=0;$i -lt 39;$i++){$paths+=('cli/synthetic-{0:d2}.bin' -f $i)}
    foreach($relative in $paths){
        $path=Join-Path $root $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
        [IO.File]::WriteAllText($path,('synthetic content '+$relative),[Text.Encoding]::UTF8)
        $items+=@{Path=$relative;Bytes=(Get-Item -LiteralPath $path).Length;Sha256=(Get-FileHash -LiteralPath $path).Hash}
    }
    @{SchemaVersion=1;Architecture='amd64';CodexVersion='1.2.3';ClaudeVersion='4.5.6';BundleId='codex-1.2.3_claude-4.5.6_win-x64';Files=$items;ProfilesIncluded=$false;AuthenticationCopied=$false;CliExecutedDuringPreparation=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'offline-manifest.json') -Encoding UTF8
    Copy-Item -LiteralPath $verifier -Destination (Join-Path $root 'Verify-OfflineFiles.ps1')
    Copy-Item -LiteralPath $batch -Destination (Join-Path $root 'VerifyOfflineSetup.bat')
    $version='0.15.1'
    $extensions=@("VS2017.15.7.AIAssistant-$version.vsix","VS2017.AIAssistant-$version.vsix","VS2019.AIAssistant-$version.vsix","VS2022.AIAssistant-$version.vsix","Grokeen-VisualStudio-Themes-$version-VS2017.vsix","Grokeen-VisualStudio-Themes-$version-VS2019.vsix","Grokeen-VisualStudio-Themes-$version-VS2022.vsix")
    $extensionRows=@(foreach($name in $extensions){$file=Join-Path $root $name;[IO.File]::WriteAllText($file,'synthetic VSIX '+$name);(Get-FileHash -LiteralPath $file).Hash+'  '+$name})
    [IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'),[string[]]$extensionRows)
    # These files would create a marker if incorrectly invoked; the checksum tool must only read them.
    [IO.File]::WriteAllText((Join-Path $root 'Test-OfflineSetupOnTarget.ps1'),'Set-Content -LiteralPath (Join-Path $PSScriptRoot executed-marker.txt) -Value incorrect')
    [IO.File]::WriteAllText((Join-Path $root 'Install-OfflineCli.ps1'),'Set-Content -LiteralPath (Join-Path $PSScriptRoot executed-marker.txt) -Value incorrect')
    Write-Sums $root
    return $root
}
function Write-Sums([string]$root){
    $lines=@(Get-ChildItem -LiteralPath $root -Recurse -File | Sort-Object FullName | ForEach-Object { if($_.Name -ne 'OFFLINE_SHA256SUMS.txt'){(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash+'  '+$_.FullName.Substring($root.Length+1).Replace('\','/')} })
    [IO.File]::WriteAllLines((Join-Path $root 'OFFLINE_SHA256SUMS.txt'),[string[]]$lines,(New-Object Text.UTF8Encoding($false)))
}
function Get-Snapshot([string]$root){
    return @(Get-ChildItem -LiteralPath $root -Recurse -File -Force | Sort-Object FullName | ForEach-Object { $_.FullName.Substring($root.Length+1)+'|'+$_.Length+'|'+(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }) -join "`n"
}
function Check([string]$name,[scriptblock]$change,[bool]$expected,[string]$expectedReason){
    $root=New-Fixture $name
    if($null -ne $change){& $change $root}
    $before=Get-Snapshot $root
    foreach($shell in @('powershell.exe','pwsh.exe')){
        $process=Invoke-OwnedProcess $shell @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $root 'Verify-OfflineFiles.ps1'),'-BundleDirectory',$root) ''
        $text=@($process.Output);$code=$process.ExitCode
        if(($code -eq 0) -ne $expected){throw "$name unexpected exit for $shell"}
        if(!$expected -and ($text -join "`n") -notmatch [regex]::Escape($expectedReason)){throw "$name failed for an unexpected reason in $shell"}
        if((Get-Snapshot $root) -cne $before -or (Test-Path -LiteralPath (Join-Path $root 'executed-marker.txt'))){throw "$name wrote files or ran installer"}
        $log=Join-Path $output ($name+'-'+$shell+'.log')
        [IO.File]::WriteAllLines($log,[string[]]@($text|ForEach-Object {$_.ToString()}),(New-Object Text.UTF8Encoding($false)))
        $script:results+= [pscustomobject]@{Case=$name;Shell=$shell;Version=$runtimeVersions[$shell];ExitCode=$code;ExpectedSuccess=$expected;ExpectedReason=$expectedReason;InputsUnchanged=$true;InstallerMarkerAbsent=$true;Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
    }
    if($name -eq 'normal' -or $name -eq 'tampered'){
        $bat=Join-Path $root 'VerifyOfflineSetup.bat'
        $process=Invoke-OwnedProcess 'cmd.exe' @() ('/d /c ""'+$bat+'" --noninteractive"')
        $text=@($process.Output);$code=$process.ExitCode
        if((($code -eq 0) -ne $expected) -or (Get-Snapshot $root) -cne $before -or (Test-Path -LiteralPath (Join-Path $root 'executed-marker.txt'))){throw 'Batch did not perform read-only verification or preserve the failure code.'}
        if(!$expected -and ($text -join "`n") -notmatch [regex]::Escape($expectedReason)){throw 'Batch failed for an unexpected reason.'}
        $log=Join-Path $output ('batch-'+$name+'-cmd.exe.log');[IO.File]::WriteAllLines($log,[string[]]@($text|ForEach-Object {$_.ToString()}),(New-Object Text.UTF8Encoding($false)))
        $script:results+= [pscustomobject]@{Case=('batch-'+$name);Shell='cmd.exe';ExitCode=$code;ExpectedSuccess=$expected;ExpectedReason=$expectedReason;InputsUnchanged=$true;InstallerMarkerAbsent=$true;Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
    }
}
Check 'normal' $null $true ''
Check 'tampered' {param($r) [IO.File]::AppendAllText((Join-Path $r 'cli/synthetic-00.bin'),'changed')} $false 'Listed file hash mismatch.'
Check 'missing' {param($r) Remove-Item -LiteralPath (Join-Path $r 'cli/synthetic-01.bin')} $false 'Listed file missing or outside bundle.'
Check 'duplicate' {param($r) $p=Join-Path $r 'OFFLINE_SHA256SUMS.txt';$rows=[IO.File]::ReadAllLines($p);[IO.File]::AppendAllText($p,$rows[0]+[Environment]::NewLine)} $false 'Unsafe or duplicate checksum path.'
Check 'traversal' {param($r) $p=Join-Path $r 'OFFLINE_SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  ../outside.txt'+[Environment]::NewLine))} $false 'Unsafe or duplicate checksum path.'
Check 'absolute' {param($r) $p=Join-Path $r 'OFFLINE_SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  C:/outside.txt'+[Environment]::NewLine))} $false 'Unsafe or duplicate checksum path.'
Check 'extra' {param($r) [IO.File]::WriteAllText((Join-Path $r 'unexpected.txt'),'extra')} $false 'Bundle has unlisted or missing files.'
Check 'manifest-size' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.Files[0].Bytes++;$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Write-Sums $r} $false 'Offline CLI manifest byte/hash mismatch.'
Check 'dynamic-cli-count' {param($r) $p=Join-Path $r 'cli/extra.bin';[IO.File]::WriteAllText($p,'new runtime file');$manifest=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json;$j.Files+=@([pscustomobject]@{Path='cli/extra.bin';Bytes=(Get-Item -LiteralPath $p).Length;Sha256=(Get-FileHash -LiteralPath $p).Hash});$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $manifest -Encoding UTF8;Write-Sums $r} $true ''
Check 'cli-unlisted-in-manifest' {param($r) [IO.File]::WriteAllText((Join-Path $r 'cli/extra.bin'),'unlisted runtime file');Write-Sums $r} $false 'Unlisted CLI manifest file.'
Check 'required-cli-missing' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.Files=@($j.Files|Where-Object {$_.Path -ne 'cli/codex/bin/codex.exe'});$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Remove-Item -LiteralPath (Join-Path $r 'cli/codex/bin/codex.exe');Write-Sums $r} $false 'Required CLI manifest file missing.'
Check 'missing-safety-flag' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.PSObject.Properties.Remove('ProfilesIncluded');$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Write-Sums $r} $false 'Offline CLI manifest safety flag is missing or invalid.'
Check 'wrong-safety-flag-type' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.ProfilesIncluded='false';$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Write-Sums $r} $false 'Offline CLI manifest safety flag is missing or invalid.'
Check 'vsix-file-and-row-removed' {param($r) $name='VS2019.AIAssistant-0.15.1.vsix';Remove-Item -LiteralPath (Join-Path $r $name);$p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::WriteAllLines($p,[string[]]@([IO.File]::ReadAllLines($p)|Where-Object {$_ -notlike "*$name"}));Write-Sums $r} $false 'Extension checksum list must contain exactly seven entries.'
Check 'vsix-only-row-removed' {param($r) $name='VS2019.AIAssistant-0.15.1.vsix';$p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::WriteAllLines($p,[string[]]@([IO.File]::ReadAllLines($p)|Where-Object {$_ -notlike "*$name"}));Write-Sums $r} $false 'Extension checksum list must contain exactly seven entries.'
Check 'vsix-only-file-removed' {param($r) Remove-Item -LiteralPath (Join-Path $r 'VS2019.AIAssistant-0.15.1.vsix');Write-Sums $r} $false 'Extension package missing from full checksums.'
Check 'theme-file-and-row-removed' {param($r) $name='Grokeen-VisualStudio-Themes-0.15.1-VS2022.vsix';Remove-Item -LiteralPath (Join-Path $r $name);$p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::WriteAllLines($p,[string[]]@([IO.File]::ReadAllLines($p)|Where-Object {$_ -notlike "*$name"}));Write-Sums $r} $false 'Extension checksum list must contain exactly seven entries.'
Check 'vsix-sums-removed' {param($r) Remove-Item -LiteralPath (Join-Path $r 'SHA256SUMS.txt');Write-Sums $r} $false 'Extension checksum list is missing.'
Check 'vsix-unlisted' {param($r) [IO.File]::WriteAllText((Join-Path $r 'unexpected.vsix'),'unlisted extension');Write-Sums $r} $false 'Unlisted extension VSIX file.'
Check 'vsix-duplicate' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$lines=[IO.File]::ReadAllLines($p);$lines[1]=$lines[0];[IO.File]::WriteAllLines($p,[string[]]$lines);Write-Sums $r} $false 'Unexpected or duplicate extension checksum entry.'
Check 'vsix-checksum-mismatch' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$lines=[IO.File]::ReadAllLines($p);$lines[0]=('A'*64)+$lines[0].Substring(64);[IO.File]::WriteAllLines($p,[string[]]$lines);Write-Sums $r} $false 'Extension and full checksums disagree.'
Check 'vsix-wrong-version' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$lines=@([IO.File]::ReadAllLines($p)|ForEach-Object {$_.Replace('0.15.1','0.11.1')});[IO.File]::WriteAllLines($p,[string[]]$lines);Get-ChildItem -LiteralPath $r -File -Filter '*.vsix'|ForEach-Object {Rename-Item -LiteralPath $_.FullName -NewName ($_.Name.Replace('0.15.1','0.11.1'))};Write-Sums $r} $false 'Unexpected or duplicate extension checksum entry.'
$link=Join-Path $output 'ancestor-link'
[void](New-Item -ItemType Junction -Path $link -Target (Join-Path $output 'normal'))
foreach($shell in @('powershell.exe','pwsh.exe')){
    $process=Invoke-OwnedProcess $shell @('-NoProfile','-ExecutionPolicy','Bypass','-File',$verifier,'-BundleDirectory',(Join-Path $link 'cli')) ''
    $text=@($process.Output);$code=$process.ExitCode
    if($code -eq 0 -or (($text -join ' ') -notmatch 'ancestor reparse')){throw "Ancestor reparse was not refused by $shell"}
    $log=Join-Path $output ('ancestor-reparse-'+$shell+'.log');[IO.File]::WriteAllLines($log,[string[]]@($text|ForEach-Object {$_.ToString()}),(New-Object Text.UTF8Encoding($false)))
    $results+= [pscustomobject]@{Case='ancestor-reparse';Shell=$shell;Version=$runtimeVersions[$shell];ExitCode=$code;ExpectedSuccess=$false;ExpectedReason='ancestor reparse';InputsUnchanged=$true;InstallerMarkerAbsent=$true;Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
}
$originalModulePath=$env:PSModulePath
try{
    $env:PSModulePath=Join-Path $PSHOME 'Modules'
    $root=Join-Path $output 'normal';$before=Get-Snapshot $root
    $process=Invoke-OwnedProcess 'powershell.exe' @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $root 'Verify-OfflineFiles.ps1'),'-BundleDirectory',$root) ''
    $text=@($process.Output);$code=$process.ExitCode
    if($code -ne 0 -or ($text -join ' ') -notmatch 'PASS checksum-only' -or (Get-Snapshot $root) -cne $before){throw 'PowerShell 5 failed with a PowerShell 7-only inherited module path.'}
    $log=Join-Path $output 'inherited-module-path-powershell.exe.log'
    [IO.File]::WriteAllLines($log,[string[]]@($text|ForEach-Object {$_.ToString()}),(New-Object Text.UTF8Encoding($false)))
    $results+=[pscustomobject]@{Case='inherited-module-path';Shell='powershell.exe';ExitCode=$code;ExpectedSuccess=$true;InputsUnchanged=$true;InstallerMarkerAbsent=$true;Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
}finally{$env:PSModulePath=$originalModulePath}
$result=[pscustomobject]@{Succeeded=$true;Checks=$results;RuntimeVersions=$runtimeVersions;OuterRuntime=$PSVersionTable.PSVersion.ToString();OuterEdition=$PSVersionTable.PSEdition;VerifierSHA256=(Get-FileHash -LiteralPath $verifier).Hash;BatchSHA256=(Get-FileHash -LiteralPath $batch).Hash;RealInstallerExecuted=$false;RealCliExecuted=$false;NetworkUsed=$false;NetworkIsolationVerified=$false;VsixIdentityOrSkuVerified=$false;AuthenticityVerified=$false}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' checksum-only synthetic checks; installers and CLI were not run.')
