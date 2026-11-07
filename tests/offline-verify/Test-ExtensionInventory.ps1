[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'scripts/OfflineCli.Common.ps1')
. (Join-Path $repo 'scripts/OfflinePackage.Common.ps1')
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Use a new output directory.'}
[void][IO.Directory]::CreateDirectory($out)
$results=@()
function New-InventoryFixture([string]$name,[string]$version){
    $root=Join-Path $out $name;[void][IO.Directory]::CreateDirectory($root)
    $names=@("VS2017.15.7.AIAssistant-$version.vsix","VS2017.AIAssistant-$version.vsix","VS2019.AIAssistant-$version.vsix","VS2022.AIAssistant-$version.vsix","Grokeen-VisualStudio-Themes-$version-VS2017.vsix","Grokeen-VisualStudio-Themes-$version-VS2019.vsix","Grokeen-VisualStudio-Themes-$version-VS2022.vsix")
    $lines=@(foreach($item in $names){$p=Join-Path $root $item;[IO.File]::WriteAllText($p,'synthetic '+$item);(Get-FileHash -LiteralPath $p).Hash+'  '+$item})
    [IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'),[string[]]$lines)
    return $root
}
function Check-Inventory([string]$name,[string]$fixtureVersion,[scriptblock]$change,[bool]$expected){
    $root=New-InventoryFixture $name $fixtureVersion
    if($null -ne $change){& $change $root}
    $accepted=$true
    try {$inventory=@(Get-OfflineExtensionInventory -ExtensionRoot $root -PackageVersion '0.15.1')}
    catch {$accepted=$false}
    if($accepted -ne $expected){throw "$name returned unexpected acceptance"}
    if($accepted){
        $destination=Join-Path $out ($name+'-copy');[void][IO.Directory]::CreateDirectory($destination)
        Copy-OfflineExtensionInventory -Inventory $inventory -ExtensionRoot $root -Destination $destination -PackageVersion '0.15.1'
        $files=@(Get-ChildItem -LiteralPath $destination -File)
        if($files.Count -ne 8 -or @($files|Where-Object {$_.Name -like 'Grokeen-VisualStudio-Themes-*.vsix'}).Count -ne 3){throw 'Copy omitted required theme packages.'}
        $null=Get-OfflineExtensionInventory -ExtensionRoot $destination -PackageVersion '0.15.1'
    }
    $script:results+=[pscustomobject]@{Case=$name;Expected=$expected;Accepted=$accepted}
}
Check-Inventory 'normal' '0.15.1' $null $true
Check-Inventory 'old-version' '0.11.1' $null $false
Check-Inventory 'missing-theme' '0.15.1' {param($r) Remove-Item -LiteralPath (Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2019.vsix')} $false
Check-Inventory 'tampered' '0.15.1' {param($r) [IO.File]::AppendAllText((Join-Path $r 'VS2019.AIAssistant-0.15.1.vsix'),'changed')} $false
Check-Inventory 'duplicate' '0.15.1' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$lines=[IO.File]::ReadAllLines($p);[IO.File]::AppendAllText($p,$lines[0]+[Environment]::NewLine)} $false
Check-Inventory 'extra-vsix' '0.15.1' {param($r) [IO.File]::WriteAllText((Join-Path $r 'unexpected.vsix'),'unexpected')} $false
Check-Inventory 'extra-row' '0.15.1' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  unexpected.vsix'+[Environment]::NewLine))} $false
Check-Inventory 'traversal' '0.15.1' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  ../outside.vsix'+[Environment]::NewLine))} $false
$receipt=[pscustomobject]@{Succeeded=$true;Checks=$results;InventoryScriptSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'scripts/OfflinePackage.Common.ps1')).Hash;NetworkUsed=$false;InstallerExecuted=$false}
$receipt|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' versioned AI/theme inventory synthetic cases.')
