# TASK074 / 2026-11-08 CodexOrig8 : Pure checksum verification for an extracted bundle.
[CmdletBinding()]
param([string]$BundleDirectory=$PSScriptRoot,[ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.15.0')
$ErrorActionPreference='Stop'
# Use modules for this PowerShell runtime even when a parent process supplied another runtime's PSModulePath.
foreach($module in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')){
    $modulePath=Join-Path $PSHOME ('Modules/'+$module+'/'+$module+'.psd1')
    Import-Module -Name $modulePath -ErrorAction Stop
}
$root=[IO.Path]::GetFullPath($BundleDirectory).TrimEnd('\')
if($root -notmatch '^[A-Za-z]:\\.+'){throw 'A local extracted bundle directory is required.'}
if(!(Test-Path -LiteralPath $root -PathType Container)){throw 'Bundle directory missing.'}
$rootItem=Get-Item -LiteralPath $root -Force
for($ancestor=$rootItem;$null -ne $ancestor;$ancestor=$ancestor.Parent){
    if($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Bundle root or ancestor reparse point refused.'}
}
function Assert-PlainBundlePath([string]$Path){
    $item=Get-Item -LiteralPath $Path -Force
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Bundle reparse point refused.'}
    $directory=if($item.PSIsContainer){$item}else{$item.Directory}
    while($null -ne $directory -and $directory.FullName.Length -ge $root.Length){
        if($directory.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Bundle reparse point refused.'}
        if([String]::Equals($directory.FullName,$root,[StringComparison]::OrdinalIgnoreCase)){return}
        $directory=$directory.Parent
    }
    throw 'Bundle path escaped root.'
}
$sums=Join-Path $root 'OFFLINE_SHA256SUMS.txt'
Assert-PlainBundlePath $sums
$lines=@([IO.File]::ReadAllLines($sums))
$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$fullHashes=New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
foreach($line in $lines){
    if($line -cnotmatch '^([A-Fa-f0-9]{64})  (.+)$'){throw 'Invalid checksum row.'}
    $expected=$matches[1].ToUpperInvariant();$relative=$matches[2].Replace('\','/')
    $parts=@($relative.Split('/'))
    if($relative.StartsWith('/') -or @($parts|Where-Object {$_ -in @('','.','..') -or $_ -match ':' -or $_ -match '[ .]$'}).Count -gt 0 -or !$seen.Add($relative)){throw 'Unsafe or duplicate checksum path.'}
    $file=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$file.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $file -PathType Leaf)){throw 'Listed file missing or outside bundle.'}
    Assert-PlainBundlePath $file
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -cne $expected){throw 'Listed file hash mismatch.'}
    $fullHashes.Add($relative,$expected)
}
$pending=New-Object 'System.Collections.Generic.Queue[string]'
$pending.Enqueue($root)
$actual=@()
while($pending.Count -gt 0){
    $directory=$pending.Dequeue()
    foreach($item in Get-ChildItem -LiteralPath $directory -Force){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Bundle reparse point refused.'}
        if($item.PSIsContainer){$pending.Enqueue($item.FullName)}
        elseif(![String]::Equals($item.FullName,$sums,[StringComparison]::OrdinalIgnoreCase)){$actual+=$item}
    }
}
if($actual.Count -ne $seen.Count){throw 'Bundle has unlisted or missing files.'}
foreach($file in $actual){$relative=$file.FullName.Substring($root.Length+1).Replace('\','/');if(!$seen.Contains($relative)){throw 'Bundle has unlisted file.'}}
$manifestPath=Join-Path $root 'offline-manifest.json'
if((Get-Item -LiteralPath $manifestPath).Length -gt 1048576){throw 'Offline CLI manifest is too large.'}
$manifest=Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8|ConvertFrom-Json
$manifestFiles=@($manifest.Files)
if($manifest.SchemaVersion -ne 1 -or $manifest.Architecture -cne 'amd64' -or $manifest.CodexVersion -notmatch '^\d+\.\d+\.\d+$' -or $manifest.ClaudeVersion -notmatch '^\d+\.\d+\.\d+$' -or $manifest.BundleId -cne ('codex-'+$manifest.CodexVersion+'_claude-'+$manifest.ClaudeVersion+'_win-x64') -or $manifestFiles.Count -lt 1 -or $manifestFiles.Count -gt 4096){throw 'Offline CLI manifest scope differs.'}
foreach($name in @('ProfilesIncluded','AuthenticationCopied','CliExecutedDuringPreparation')){
    $property=$manifest.PSObject.Properties[$name]
    if($null -eq $property -or $property.Value -isnot [bool] -or $property.Value){throw 'Offline CLI manifest safety flag is missing or invalid.'}
}
$cliPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($item in $manifestFiles){
    $relative=[string]$item.Path
    if(!$relative.StartsWith('cli/',[StringComparison]::OrdinalIgnoreCase) -or !$cliPaths.Add($relative) -or !$seen.Contains($relative)){throw 'Offline CLI manifest path missing or duplicated.'}
    $file=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if($item.Sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $item.Bytes -le 0 -or $item.Bytes -gt 1073741824 -or !$file.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-Item -LiteralPath $file).Length -ne $item.Bytes -or (Get-FileHash -LiteralPath $file).Hash -cne $item.Sha256.ToUpperInvariant()){throw 'Offline CLI manifest byte/hash mismatch.'}
}
$required=@('cli/codex/codex-package.json','cli/codex/bin/codex.exe','cli/codex/bin/codex-code-mode-host.exe','cli/codex/codex-path/rg.exe','cli/codex/codex-resources/codex-command-runner.exe','cli/codex/codex-resources/codex-windows-sandbox-setup.exe','cli/claude/claude.exe')
foreach($name in $required){if(!$cliPaths.Contains($name)){throw 'Required CLI manifest file missing.'}}
foreach($relative in $seen){if($relative.StartsWith('cli/',[StringComparison]::OrdinalIgnoreCase) -and !$cliPaths.Contains($relative)){throw 'Unlisted CLI manifest file.'}}

# Filenames and checksums are a consistency check, not proof of signed VSIX identity or SKU.
$aiNames=@("VS2017.15.7.AIAssistant-$PackageVersion.vsix","VS2017.AIAssistant-$PackageVersion.vsix","VS2019.AIAssistant-$PackageVersion.vsix","VS2022.AIAssistant-$PackageVersion.vsix")
$themeNames=@("Grokeen-VisualStudio-Themes-$PackageVersion-VS2017.vsix","Grokeen-VisualStudio-Themes-$PackageVersion-VS2019.vsix","Grokeen-VisualStudio-Themes-$PackageVersion-VS2022.vsix")
$expectedNames=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach($name in @($aiNames)+@($themeNames)){[void]$expectedNames.Add($name)}
$packageSums=Join-Path $root 'SHA256SUMS.txt'
if(!$seen.Contains('SHA256SUMS.txt')){throw 'Extension checksum list is missing.'}
$extensionRows=@([IO.File]::ReadAllLines($packageSums))
if($extensionRows.Count -ne 7){throw 'Extension checksum list must contain exactly seven entries.'}
$extensionSeen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$extensionPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($line in $extensionRows){
    if($line -cnotmatch '^([A-Fa-f0-9]{64})  (.+)$'){throw 'Invalid extension checksum row.'}
    $hash=$matches[1].ToUpperInvariant();$relative=$matches[2].Replace('\','/')
    $name=[IO.Path]::GetFileName($relative)
    if(!$expectedNames.Contains($name) -or !$extensionSeen.Add($name)){throw 'Unexpected or duplicate extension checksum entry.'}
    if($relative -cne $name -and !($relative -ceq ('Themes/'+$name) -and $themeNames -ccontains $name)){throw 'Unsafe extension checksum path.'}
    if(!$extensionPaths.Add($relative) -or !$fullHashes.ContainsKey($relative)){throw 'Extension package missing from full checksums.'}
    if($fullHashes[$relative] -cne $hash){throw 'Extension and full checksums disagree.'}
}
if($extensionSeen.Count -ne 7){throw 'Extension checksum list is incomplete.'}
foreach($relative in $seen){
    if($relative -match '(?i)\.vsix$' -and !$relative.StartsWith('cli/',[StringComparison]::OrdinalIgnoreCase) -and !$extensionPaths.Contains($relative)){throw 'Unlisted extension VSIX file.'}
}
Write-Output ('PASS checksum-only bundle verification: '+$seen.Count+' files; '+$cliPaths.Count+' CLI files; seven '+$PackageVersion+' VSIX filenames/checksums. VSIX identity and installation were not verified.')
