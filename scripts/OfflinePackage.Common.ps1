# TASK075 / 2026-11-08 CodexOrig : Verify the exact versioned AI and theme VSIX inventory.
function Get-OfflineExtensionInventory {
    param([Parameter(Mandatory=$true)][string]$ExtensionRoot,[Parameter(Mandatory=$true)][string]$PackageVersion)
    if($PackageVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package version.'}
    $root=[IO.Path]::GetFullPath($ExtensionRoot)
    $names=@("VS2017.15.7.AIAssistant-$PackageVersion.vsix","VS2017.AIAssistant-$PackageVersion.vsix","VS2019.AIAssistant-$PackageVersion.vsix","VS2022.AIAssistant-$PackageVersion.vsix","Grokeen-VisualStudio-Themes-$PackageVersion-VS2017.vsix","Grokeen-VisualStudio-Themes-$PackageVersion-VS2019.vsix","Grokeen-VisualStudio-Themes-$PackageVersion-VS2022.vsix")
    $expected=New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($name in $names){$expected.Add($name,$name)}
    $manifest=Join-Path $root 'SHA256SUMS.txt'
    Assert-OfflinePlainPath $manifest
    if(!(Test-Path -LiteralPath $manifest -PathType Leaf)){throw 'Extension checksum list is missing.'}
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $rows=@(foreach($line in [IO.File]::ReadAllLines($manifest)){
        if($line -cnotmatch '^([A-Fa-f0-9]{64})  ([^/\\:]+\.vsix)$'){throw 'Invalid extension checksum row.'}
        $hash=$matches[1].ToUpperInvariant();$name=$matches[2]
        if(!$expected.ContainsKey($name) -or $name -cne $expected[$name] -or !$seen.Add($name)){throw 'Unexpected or duplicate extension checksum entry.'}
        $path=Join-Path $root $name
        Assert-OfflinePlainPath $path
        if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $hash){throw 'Extension checksum mismatch or file missing.'}
        [pscustomobject]@{Name=$name;Source=$path;Sha256=$hash}
    })
    if($seen.Count -ne $names.Count){throw 'Extension checksum list does not contain all current-version packages.'}
    $actual=@(Get-ChildItem -LiteralPath $root -File -Filter '*.vsix' -Force)
    if($actual.Count -ne $names.Count){throw 'Extension directory contains missing or unexpected VSIX files.'}
    foreach($file in $actual){if(!$expected.ContainsKey($file.Name) -or $file.Name -cne $expected[$file.Name]){throw 'Unexpected VSIX file in extension directory.'}}
    return $rows
}

function Copy-OfflineExtensionInventory {
    param([Parameter(Mandatory=$true)][object[]]$Inventory,[Parameter(Mandatory=$true)][string]$ExtensionRoot,[Parameter(Mandatory=$true)][string]$Destination,[Parameter(Mandatory=$true)][string]$PackageVersion)
    $destination=[IO.Path]::GetFullPath($Destination)
    Assert-OfflinePlainPath $destination
    foreach($name in @($Inventory | ForEach-Object {$_.Name})+@('SHA256SUMS.txt')){
        if(Test-Path -LiteralPath (Join-Path $destination $name)){throw 'Extension destination already contains a target file.'}
    }
    foreach($row in $Inventory){
        $target=Join-Path $destination $row.Name
        Copy-Item -LiteralPath $row.Source -Destination $target
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $row.Sha256){throw 'Extension package changed during copy.'}
    }
    Copy-Item -LiteralPath (Join-Path $ExtensionRoot 'SHA256SUMS.txt') -Destination (Join-Path $destination 'SHA256SUMS.txt')
    $null=Get-OfflineExtensionInventory -ExtensionRoot $destination -PackageVersion $PackageVersion
}
