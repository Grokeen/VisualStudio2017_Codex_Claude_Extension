[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a new output directory.'}
[void][IO.Directory]::CreateDirectory($output)
$verifier=Join-Path $repo 'scripts/Verify-OfflineFiles.ps1'
$batch=Join-Path $repo 'VerifyOfflineSetup.bat'
$results=@()
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
function Check([string]$name,[scriptblock]$change,[bool]$expected){
    $root=New-Fixture $name
    if($null -ne $change){& $change $root}
    $before=Get-Snapshot $root
    foreach($shell in @('powershell.exe','pwsh.exe')){
        $text=@(& $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'Verify-OfflineFiles.ps1') -BundleDirectory $root 2>&1)
        $code=$LASTEXITCODE
        if(($code -eq 0) -ne $expected){throw "$name unexpected exit for $shell"}
        if((Get-Snapshot $root) -cne $before -or (Test-Path -LiteralPath (Join-Path $root 'executed-marker.txt'))){throw "$name wrote files or ran installer"}
        $script:results+= [pscustomobject]@{Case=$name;Shell=$shell;ExitCode=$code;ExpectedSuccess=$expected;InputsUnchanged=$true;InstallerMarkerAbsent=$true}
    }
    if($name -eq 'normal' -or $name -eq 'tampered'){
        $text=@(& cmd.exe /d /c (Join-Path $root 'VerifyOfflineSetup.bat') --noninteractive 2>&1)
        $code=$LASTEXITCODE
        if((($code -eq 0) -ne $expected) -or (Get-Snapshot $root) -cne $before -or (Test-Path -LiteralPath (Join-Path $root 'executed-marker.txt'))){throw 'Batch did not perform read-only verification or preserve the failure code.'}
        $script:results+= [pscustomobject]@{Case=('batch-'+$name);Shell='cmd.exe';ExitCode=$code;ExpectedSuccess=$expected;InputsUnchanged=$true;InstallerMarkerAbsent=$true}
    }
}
Check 'normal' $null $true
Check 'tampered' {param($r) [IO.File]::AppendAllText((Join-Path $r 'cli/synthetic-00.bin'),'changed')} $false
Check 'missing' {param($r) Remove-Item -LiteralPath (Join-Path $r 'cli/synthetic-01.bin')} $false
Check 'duplicate' {param($r) $p=Join-Path $r 'OFFLINE_SHA256SUMS.txt';$rows=[IO.File]::ReadAllLines($p);[IO.File]::AppendAllText($p,$rows[0]+[Environment]::NewLine)} $false
Check 'traversal' {param($r) $p=Join-Path $r 'OFFLINE_SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  ../outside.txt'+[Environment]::NewLine))} $false
Check 'absolute' {param($r) $p=Join-Path $r 'OFFLINE_SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  C:/outside.txt'+[Environment]::NewLine))} $false
Check 'extra' {param($r) [IO.File]::WriteAllText((Join-Path $r 'unexpected.txt'),'extra')} $false
Check 'manifest-size' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.Files[0].Bytes++;$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Write-Sums $r} $false
Check 'dynamic-cli-count' {param($r) $p=Join-Path $r 'cli/extra.bin';[IO.File]::WriteAllText($p,'new runtime file');$manifest=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json;$j.Files+=@([pscustomobject]@{Path='cli/extra.bin';Bytes=(Get-Item -LiteralPath $p).Length;Sha256=(Get-FileHash -LiteralPath $p).Hash});$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $manifest -Encoding UTF8;Write-Sums $r} $true
Check 'cli-unlisted-in-manifest' {param($r) [IO.File]::WriteAllText((Join-Path $r 'cli/extra.bin'),'unlisted runtime file');Write-Sums $r} $false
Check 'required-cli-missing' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.Files=@($j.Files|Where-Object {$_.Path -ne 'cli/codex/bin/codex.exe'});$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Remove-Item -LiteralPath (Join-Path $r 'cli/codex/bin/codex.exe');Write-Sums $r} $false
Check 'missing-safety-flag' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.PSObject.Properties.Remove('ProfilesIncluded');$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Write-Sums $r} $false
Check 'wrong-safety-flag-type' {param($r) $p=Join-Path $r 'offline-manifest.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.ProfilesIncluded='false';$j|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $p -Encoding UTF8;Write-Sums $r} $false
$link=Join-Path $output 'ancestor-link'
[void](New-Item -ItemType Junction -Path $link -Target (Join-Path $output 'normal'))
foreach($shell in @('powershell.exe','pwsh.exe')){
    $text=@(& $shell -NoProfile -ExecutionPolicy Bypass -File $verifier -BundleDirectory (Join-Path $link 'cli') 2>&1)
    if($LASTEXITCODE -eq 0 -or (($text -join ' ') -notmatch 'ancestor reparse')){throw "Ancestor reparse was not refused by $shell"}
    $results+= [pscustomobject]@{Case='ancestor-reparse';Shell=$shell;ExitCode=$LASTEXITCODE;ExpectedSuccess=$false;InputsUnchanged=$true;InstallerMarkerAbsent=$true}
}
$result=[pscustomobject]@{Succeeded=$true;Checks=$results;VerifierSHA256=(Get-FileHash -LiteralPath $verifier).Hash;BatchSHA256=(Get-FileHash -LiteralPath $batch).Hash;RealInstallerExecuted=$false;RealCliExecuted=$false;NetworkUsed=$false}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' checksum-only synthetic checks; installers and CLI were not run.')
