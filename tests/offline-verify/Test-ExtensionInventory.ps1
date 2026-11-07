[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
foreach($module in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')){Import-Module -Name (Join-Path $PSHOME ('Modules/'+$module+'/'+$module+'.psd1')) -ErrorAction Stop}
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
        $files=@(Get-ChildItem -LiteralPath $destination -File -Recurse)
        if($files.Count -ne 8 -or @($files|Where-Object {$_.Name -like 'Grokeen-VisualStudio-Themes-*.vsix'}).Count -ne 3){throw 'Copy omitted required theme packages.'}
        $null=Get-OfflineExtensionInventory -ExtensionRoot $destination -PackageVersion '0.15.1'
    }
    $script:results+=[pscustomobject]@{Case=$name;Expected=$expected;Accepted=$accepted}
}
Check-Inventory 'normal' '0.15.1' $null $true
Check-Inventory 'themes-layout' '0.15.1' {param($r) $dir=Join-Path $r 'Themes';[void][IO.Directory]::CreateDirectory($dir); Get-ChildItem -LiteralPath $r -File -Filter 'Grokeen-VisualStudio-Themes-*.vsix'|ForEach-Object {Move-Item -LiteralPath $_.FullName -Destination (Join-Path $dir $_.Name)}; $p=Join-Path $r 'SHA256SUMS.txt';$lines=@([IO.File]::ReadAllLines($p)|ForEach-Object {$_ -replace '  Grokeen-','  Themes/Grokeen-'});[IO.File]::WriteAllLines($p,[string[]]$lines)} $true
Check-Inventory 'both-theme-locations' '0.15.1' {param($r) $dir=Join-Path $r 'Themes';[void][IO.Directory]::CreateDirectory($dir);Copy-Item -LiteralPath (Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2019.vsix') -Destination $dir} $false
Check-Inventory 'theme-location-mismatch' '0.15.1' {param($r) $dir=Join-Path $r 'Themes';[void][IO.Directory]::CreateDirectory($dir);Move-Item -LiteralPath (Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2019.vsix') -Destination $dir} $false
Check-Inventory 'old-version' '0.11.1' $null $false
Check-Inventory 'missing-theme' '0.15.1' {param($r) Remove-Item -LiteralPath (Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2019.vsix')} $false
Check-Inventory 'tampered' '0.15.1' {param($r) [IO.File]::AppendAllText((Join-Path $r 'VS2019.AIAssistant-0.15.1.vsix'),'changed')} $false
Check-Inventory 'duplicate' '0.15.1' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$lines=[IO.File]::ReadAllLines($p);[IO.File]::AppendAllText($p,$lines[0]+[Environment]::NewLine)} $false
Check-Inventory 'extra-vsix' '0.15.1' {param($r) [IO.File]::WriteAllText((Join-Path $r 'unexpected.vsix'),'unexpected')} $false
Check-Inventory 'extra-row' '0.15.1' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  unexpected.vsix'+[Environment]::NewLine))} $false
Check-Inventory 'traversal' '0.15.1' {param($r) $p=Join-Path $r 'SHA256SUMS.txt';[IO.File]::AppendAllText($p,(('A'*64)+'  ../outside.vsix'+[Environment]::NewLine))} $false
$launcherDir=Join-Path $out 'versioned-launchers';[void][IO.Directory]::CreateDirectory($launcherDir)
foreach($pair in @(@('VerifyOfflineSetup.bat','OFFLINE_PACKAGE_VERSION'),@('InstallAIAssistant.bat','AI_ASSISTANT_PACKAGE_VERSION'))){
    $template=Join-Path $repo $pair[0];$destination=Join-Path $launcherDir $pair[0]
    Write-OfflineVersionedLauncher -Template $template -Destination $destination -PackageVersion '0.15.1' -VariableName $pair[1]
    $content=[IO.File]::ReadAllText($destination)
    if(!$content.Contains(('set "'+$pair[1]+'=0.15.1"')) -or !$content.Contains(('-PackageVersion "%'+$pair[1]+'%"'))){throw 'Prepared launcher did not bind the requested package version.'}
    if($pair[0] -eq 'VerifyOfflineSetup.bat' -and $content -match '(?i)\s-Install(?:\s|$)'){throw 'Verification launcher invokes installation.'}
    $before=(Get-FileHash -LiteralPath $destination).Hash
    $rejected=$false;try{Write-OfflineVersionedLauncher -Template $template -Destination $destination -PackageVersion '0.15.1' -VariableName $pair[1]}catch{$rejected=$true}
    if(!$rejected -or (Get-FileHash -LiteralPath $destination).Hash -ne $before){throw 'Existing launcher was not preserved.'}
    $results+=[pscustomobject]@{Case=('launcher-'+$pair[1]);Expected=$true;Accepted=$true}
}
foreach($name in @('Install-AIAssistant.ps1','Test-OfflineSetupOnTarget.ps1','Verify-OfflineFiles.ps1')){
    $template=Join-Path $repo ('scripts/'+$name);$destination=Join-Path $launcherDir $name
    Write-OfflineVersionedPowerShell -Template $template -Destination $destination -PackageVersion '0.15.1'
    $content=[IO.File]::ReadAllText($destination)
    if(!$content.Contains("[string]`$PackageVersion='0.15.1'")){throw 'Prepared script default did not bind the requested package version.'}
    $tokens=$null;$errors=$null;[void][Management.Automation.Language.Parser]::ParseFile($destination,[ref]$tokens,[ref]$errors)
    if(@($errors).Count -ne 0){throw 'Version-stamped script is not valid PowerShell.'}
    $results+=[pscustomobject]@{Case=('script-'+$name);Expected=$true;Accepted=$true}
}
$receipt=[pscustomobject]@{Succeeded=$true;Checks=$results;InventoryScriptSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'scripts/OfflinePackage.Common.ps1')).Hash;NetworkUsed=$false;InstallerExecuted=$false}
$receipt|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' versioned AI/theme inventory synthetic cases.')
