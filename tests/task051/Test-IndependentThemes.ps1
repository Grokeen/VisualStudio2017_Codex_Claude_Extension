#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-IndependentThemes
desc         : 독립 테마의 등록 키·토큰 범위·소스 대비·세 SKU 패키지 내용을 설치 없이 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 컴파일된 pkgdef 동일성·원본 라이선스·AI 코드 미포함을 별도 근거로 저장.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$CandidateDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$candidate=[IO.Path]::GetFullPath($CandidateDirectory)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count){throw 'Use a new evidence directory.'}
$null=New-Item -ItemType Directory -Path $output -Force
$report=Get-Content -LiteralPath (Join-Path $candidate 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$checks=New-Object 'System.Collections.Generic.List[object]'
function Confirm-ThemeCheck {
    param([string]$Name,[bool]$Condition)
    $checks.Add([pscustomobject]@{Name=$Name;Passed=$Condition})
    if(!$Condition){throw ('FAIL '+$Name)}
}
function Read-ThemeXml {
    param([IO.Stream]$Stream)
    $settings=New-Object Xml.XmlReaderSettings;$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
    $reader=[Xml.XmlReader]::Create($Stream,$settings)
    try{$document=New-Object Xml.XmlDocument;$document.XmlResolver=$null;$document.Load($reader);return ,$document}finally{$reader.Dispose()}
}
function Get-ThemeLuminance {
    param([string]$Argb)
    $values=foreach($offset in @(2,4,6)){$level=[Convert]::ToInt32($Argb.Substring($offset,2),16)/255.0;if($level -le .04045){$level/12.92}else{[Math]::Pow(($level+.055)/1.055,2.4)}}
    return .2126*$values[0]+.7152*$values[1]+.0722*$values[2]
}
$allKeys=@()
foreach($definition in $report.Definitions){
    $stream=[IO.File]::OpenRead($definition.Definition)
    try{$xml=Read-ThemeXml $stream}finally{$stream.Dispose()}
    $theme=$xml.Themes.Theme
    Confirm-ThemeCheck ($definition.Name+' independent GUID/name') ($theme.GUID -eq $definition.Guid -and $theme.Name -eq $definition.Name -and $definition.Guid -notin @('{de3dbbcd-f642-433c-8353-8f1df4370aba}','{1ded0138-47ce-435e-84ef-9ec1f439b749}','{a4d6a176-b948-4b29-8c66-53c97a1ed7d0}'))
    Confirm-ThemeCheck ($definition.Name+' definition hash') ((Get-FileHash -LiteralPath $definition.Definition).Hash -eq $definition.DefinitionSha256)
    Confirm-ThemeCheck ($definition.Name+' common pinned baseline') ($definition.BaselineSha256 -eq 'B0FCFA9F65736D73175E75E5370D0A9AE27D4EFCB591373D590E1DD7466C9769')
    Confirm-ThemeCheck ($definition.Name+' complete baseline coverage') (@($theme.Category).Count -eq 80 -and @($theme.Category.Color).Count -eq 2470)
    $keys=@(foreach($category in $theme.Category){foreach($color in $category.Color){foreach($role in @('Background','Foreground')){if($color.SelectSingleNode($role)){$category.GUID+'|'+$color.Name+'|'+$role}}}})
    $keys=@($keys | Sort-Object)
    $allKeys+=, $keys
    $invalid=@($xml.SelectNodes('//*[@Type="CT_SYSCOLOR"]') | Where-Object{[Convert]::ToUInt32($_.Source,16) -gt 255})
    Confirm-ThemeCheck ($definition.Name+' real system indices retained') ($invalid.Count -eq 0)
    $pkgdef=Join-Path $candidate ('definitions/'+([IO.Path]::GetFileNameWithoutExtension($definition.Definition))+'.pkgdef')
    $compiled=@(Select-String -LiteralPath $pkgdef -Pattern '^\[' | ForEach-Object{($_.Line -split '\\')[-1].TrimEnd(']')})
    $omitted=@($theme.Category | Where-Object Name -notin $compiled)
    $lostExplicit=@($omitted | Where-Object{@($_.SelectNodes('.//*[@Type!="CT_INVALID"]')).Count -gt 0})
    Confirm-ThemeCheck ($definition.Name+' compiler omits only unspecified categories') ($compiled.Count -eq 70 -and $omitted.Count -eq 10 -and $lostExplicit.Count -eq 0)
    $plain=@($xml.SelectNodes('//Color[@Name="Plain Text"]'))
    $low=@($plain | Where-Object { $a=Get-ThemeLuminance $_.Background.Source; $b=Get-ThemeLuminance $_.Foreground.Source; ([Math]::Max($a,$b)+.05)/([Math]::Min($a,$b)+.05) -lt 4.5 })
    Confirm-ThemeCheck ($definition.Name+' plain editor text contrast') ($plain.Count -gt 0 -and $low.Count -eq 0)
}
Confirm-ThemeCheck 'both palettes define identical category/color/role keys' (@(Compare-Object $allKeys[0] $allKeys[1]).Count -eq 0)
$packageRows=@()
foreach($package in $report.Packages){
    Confirm-ThemeCheck ($package.Year+' exact VSIX hash') ((Get-FileHash -LiteralPath $package.Path).Hash -eq $package.Sha256)
    Confirm-ThemeCheck ($package.Year+' compiler warning/error free') ($package.BuildExitCode -eq 0 -and $package.Warnings -eq 0 -and $package.Errors -eq 0)
    $zip=[IO.Compression.ZipFile]::OpenRead($package.Path)
    try{
        Confirm-ThemeCheck ($package.Year+' no duplicate archive paths') (@($zip.Entries | Group-Object FullName | Where-Object Count -gt 1).Count -eq 0)
        Confirm-ThemeCheck ($package.Year+' pure theme data, no AI/runtime code') (@($zip.Entries | Where-Object FullName -match '(?i)\.(dll|exe|ps1|bat|cmd)$').Count -eq 0)
        $stream=$zip.GetEntry('extension.vsixmanifest').Open();try{$manifest=Read-ThemeXml $stream}finally{$stream.Dispose()}
        $ns=New-Object Xml.XmlNamespaceManager($manifest.NameTable);$ns.AddNamespace('v','http://schemas.microsoft.com/developer/vsx-schema/2011')
        $identity=$manifest.SelectSingleNode('//v:Identity',$ns);$target=$manifest.SelectSingleNode('//v:InstallationTarget',$ns)
        $expectedRange=switch($package.Year){'2017'{'[15.0,16.0)'}'2019'{'[16.0,17.0)'}default{'[17.0,18.0)'}}
        Confirm-ThemeCheck ($package.Year+' independent identity and version') ($identity.Id -eq ('Grokeen.VisualStudioThemes.'+$package.Year) -and $identity.Version -eq $report.Version)
        Confirm-ThemeCheck ($package.Year+' exact IDE version boundary') ($target.Version -eq $expectedRange -and $target.Id -eq 'Microsoft.VisualStudio.Community')
        $architecture=$target.SelectSingleNode('v:ProductArchitecture',$ns)
        Confirm-ThemeCheck ($package.Year+' x86/x64 architecture policy') ($(if($package.Year -eq '2022'){$architecture -and $architecture.InnerText -eq 'amd64'}else{!$architecture}))
        Confirm-ThemeCheck ($package.Year+' no extension/framework dependency') (@($manifest.SelectNodes('//v:Dependency',$ns)).Count -eq 0)
        $assets=@($manifest.SelectNodes('//v:Asset',$ns))
        Confirm-ThemeCheck ($package.Year+' exactly three registered pkgdef assets') ($assets.Count -eq 3 -and @($assets | Where-Object Type -ne 'Microsoft.VisualStudio.VsPackage').Count -eq 0)
        foreach($asset in $assets){
            $entry=$zip.GetEntry($asset.Path.Replace('\','/'));Confirm-ThemeCheck ($package.Year+' packaged '+$asset.Path) ($null -ne $entry)
            $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()}
            $original=Join-Path $candidate ('definitions/'+[IO.Path]::GetFileName($asset.Path))
            Confirm-ThemeCheck ($package.Year+' compiled bytes '+$asset.Path) ($hash -eq (Get-FileHash -LiteralPath $original).Hash)
        }
        foreach($license in @('Licenses/Microsoft-VS-ColorThemes.txt','Licenses/Dracula.txt','THIRD-PARTY-NOTICES.md')){Confirm-ThemeCheck ($package.Year+' license '+$license) ($null -ne $zip.GetEntry($license))}
        $packageRows+=[pscustomobject]@{Year=$package.Year;Path=$package.Path;Sha256=$package.Sha256;Entries=@($zip.Entries).Count}
    }finally{$zip.Dispose()}
}
[pscustomobject]@{Succeeded=$true;Passed=$checks.Count;Failed=0;Checks=$checks;Packages=$packageRows;InstalledIntoIde=$false;UserThemeChanged=$false;NetworkRequested=$false} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
Write-Output ('Independent themes '+$checks.Count+'/'+$checks.Count+' PASS; actual IDE installation is a separate check.')
