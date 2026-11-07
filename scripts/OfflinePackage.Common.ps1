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
        if($line -cnotmatch '^([A-Fa-f0-9]{64})  ((?:Themes/)?[^/\\:]+\.vsix)$'){throw 'Invalid extension checksum row.'}
        $hash=$matches[1].ToUpperInvariant();$relative=$matches[2];$name=[IO.Path]::GetFileName($relative)
        if(!$expected.ContainsKey($name) -or $name -cne $expected[$name] -or !$seen.Add($name)){throw 'Unexpected or duplicate extension checksum entry.'}
        if($relative -cne $name -and !($relative -ceq ('Themes/'+$name) -and $name -clike 'Grokeen-VisualStudio-Themes-*')){throw 'Invalid extension checksum location.'}
        $path=Join-Path $root $relative
        Assert-OfflinePlainPath $path
        if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $hash){throw 'Extension checksum mismatch or file missing.'}
        [pscustomobject]@{Name=$name;RelativePath=$relative;Source=$path;Sha256=$hash}
    })
    if($seen.Count -ne $names.Count){throw 'Extension checksum list does not contain all current-version packages.'}
    $actual=@(Get-ChildItem -LiteralPath $root -File -Filter '*.vsix' -Force)
    $themes=Join-Path $root 'Themes'
    if(Test-Path -LiteralPath $themes -PathType Container){Assert-OfflinePlainPath $themes;$actual+=@(Get-ChildItem -LiteralPath $themes -File -Filter '*.vsix' -Force)}
    if($actual.Count -ne $names.Count){throw 'Extension directory contains missing or unexpected VSIX files.'}
    foreach($file in $actual){if(!$expected.ContainsKey($file.Name) -or $file.Name -cne $expected[$file.Name]){throw 'Unexpected VSIX file in extension directory.'}}
    return $rows
}

function Copy-OfflineExtensionInventory {
    param([Parameter(Mandatory=$true)][object[]]$Inventory,[Parameter(Mandatory=$true)][string]$ExtensionRoot,[Parameter(Mandatory=$true)][string]$Destination,[Parameter(Mandatory=$true)][string]$PackageVersion)
    $destination=[IO.Path]::GetFullPath($Destination)
    Assert-OfflinePlainPath $destination
    foreach($name in @($Inventory | ForEach-Object {$_.RelativePath})+@('SHA256SUMS.txt')){
        if(Test-Path -LiteralPath (Join-Path $destination $name)){throw 'Extension destination already contains a target file.'}
    }
    foreach($row in $Inventory){
        $target=Join-Path $destination $row.RelativePath
        $parent=[IO.Path]::GetDirectoryName($target);Assert-OfflinePlainPath $parent
        if(!(Test-Path -LiteralPath $parent)){[void][IO.Directory]::CreateDirectory($parent)}
        Copy-Item -LiteralPath $row.Source -Destination $target
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $row.Sha256){throw 'Extension package changed during copy.'}
    }
    Copy-Item -LiteralPath (Join-Path $ExtensionRoot 'SHA256SUMS.txt') -Destination (Join-Path $destination 'SHA256SUMS.txt')
    $null=Get-OfflineExtensionInventory -ExtensionRoot $destination -PackageVersion $PackageVersion
}

function Resolve-OfflineVerificationTemplate {
    param([Parameter(Mandatory=$true)][string]$RootFile,[Parameter(Mandatory=$true)][string]$TemplateDirectory,[Parameter(Mandatory=$true)][ValidateSet('VerifyOfflineSetup.bat','Verify-OfflineFiles.ps1')][string]$Name)
    Assert-OfflinePlainPath $RootFile
    if(Test-Path -LiteralPath $RootFile -PathType Leaf){return [IO.Path]::GetFullPath($RootFile)}
    if(Test-Path -LiteralPath $RootFile){throw 'Verification root template path is not a file.'}
    $list=Join-Path $TemplateDirectory 'SHA256SUMS.txt'
    Assert-OfflinePlainPath $TemplateDirectory;Assert-OfflinePlainPath $list
    if(!(Test-Path -LiteralPath $list -PathType Leaf)){throw 'Verification template checksum list is missing.'}
    $rows=@([IO.File]::ReadAllLines($list));$names=@('VerifyOfflineSetup.bat','Verify-OfflineFiles.ps1')
    if($rows.Count -ne 2){throw 'Verification template checksum list must contain two entries.'}
    $seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach($row in $rows){
        if($row -cnotmatch '^([A-Fa-f0-9]{64})  (VerifyOfflineSetup\.bat|Verify-OfflineFiles\.ps1)$'){throw 'Invalid verification template checksum entry.'}
        $hash=$matches[1].ToUpperInvariant();$itemName=$matches[2]
        if(!$seen.Add($itemName)){throw 'Duplicate verification template checksum entry.'}
        $path=Join-Path $TemplateDirectory $itemName;Assert-OfflinePlainPath $path
        if(!(Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $hash){throw 'Verification template is missing or checksum mismatch.'}
    }
    if($seen.Count -ne $names.Count){throw 'Verification template checksum list is incomplete.'}
    return [IO.Path]::GetFullPath((Join-Path $TemplateDirectory $Name))
}

function Write-OfflineVersionedLauncher {
    param([Parameter(Mandatory=$true)][string]$Template,[Parameter(Mandatory=$true)][string]$Destination,[Parameter(Mandatory=$true)][string]$PackageVersion,[Parameter(Mandatory=$true)][ValidateSet('OFFLINE_PACKAGE_VERSION','AI_ASSISTANT_PACKAGE_VERSION')][string]$VariableName)
    if($PackageVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package version.'}
    Assert-OfflinePlainPath $Template;Assert-OfflinePlainPath $Destination
    if(!(Test-Path -LiteralPath $Template -PathType Leaf) -or (Test-Path -LiteralPath $Destination)){throw 'Verification launcher source or destination is invalid.'}
    $batchText=[IO.File]::ReadAllText($Template)
    $versionPattern='(?m)^set "'+[regex]::Escape($VariableName)+'=\d+\.\d+\.\d+"(?=\r?$)'
    $forward='-PackageVersion "%'+$VariableName+'%"'
    if([regex]::Matches($batchText,$versionPattern).Count -ne 1 -or !$batchText.Contains($forward)){throw 'Launcher version marker is missing or ambiguous.'}
    $batchText=[regex]::Replace($batchText,$versionPattern,('set "'+$VariableName+'='+$PackageVersion+'"'))
    [IO.File]::WriteAllText($Destination,$batchText,(New-Object Text.ASCIIEncoding))
}

function Write-OfflineVersionedPowerShell {
    param([Parameter(Mandatory=$true)][string]$Template,[Parameter(Mandatory=$true)][string]$Destination,[Parameter(Mandatory=$true)][string]$PackageVersion)
    if($PackageVersion -notmatch '^\d+\.\d+\.\d+$'){throw 'Invalid package version.'}
    Assert-OfflinePlainPath $Template;Assert-OfflinePlainPath $Destination
    if(!(Test-Path -LiteralPath $Template -PathType Leaf) -or (Test-Path -LiteralPath $Destination)){throw 'Versioned script source or destination is invalid.'}
    $text=[IO.File]::ReadAllText($Template)
    $pattern='(?<prefix>\[string\]\$PackageVersion='')\d+\.\d+\.\d+(?<suffix>'')'
    $versionMatches=[regex]::Matches($text,$pattern)
    if($versionMatches.Count -ne 1){throw 'PowerShell package version marker is missing or ambiguous.'}
    $match=$versionMatches[0]
    $replacement=$match.Groups['prefix'].Value+$PackageVersion+$match.Groups['suffix'].Value
    $text=$text.Substring(0,$match.Index)+$replacement+$text.Substring($match.Index+$match.Length)
    [IO.File]::WriteAllText($Destination,$text,(New-Object Text.UTF8Encoding($true)))
}
