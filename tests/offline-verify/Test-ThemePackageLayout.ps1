[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
foreach($module in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')){Import-Module -Name (Join-Path $PSHOME ('Modules/'+$module+'/'+$module+'.psd1')) -ErrorAction Stop}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'scripts/Install-AIAssistant.ps1') -LibraryOnly -PackageVersion '0.15.0'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Use a new output directory.'}
[void][IO.Directory]::CreateDirectory($out)
$results=@()
function New-SyntheticVsix([string]$path,[string]$id,[string]$version,[string]$range,[string]$architecture){
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
    $arch=if($architecture -eq 'x86'){''}else{'<ProductArchitecture>'+ $architecture +'</ProductArchitecture>'}
    $xml='<PackageManifest xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011" Version="2.0.0"><Metadata><Identity Id="'+$id+'" Version="'+$version+'" /></Metadata><Installation><InstallationTarget Id="Microsoft.VisualStudio.Community" Version="'+$range+'">'+$arch+'</InstallationTarget></Installation></PackageManifest>'
    $zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create)
    try{$entry=$zip.CreateEntry('extension.vsixmanifest');$stream=$entry.Open();try{$writer=New-Object IO.StreamWriter($stream,(New-Object Text.UTF8Encoding($false)));try{$writer.Write($xml)}finally{$writer.Dispose()}}finally{$stream.Dispose()}}finally{$zip.Dispose()}
}
function New-ThemeFixture([string]$name,[string]$layout,[string]$id,[string]$version,[string]$range,[string]$arch){
    $root=Join-Path $out $name;[void][IO.Directory]::CreateDirectory($root)
    $ai='VS2017.AIAssistant-0.15.0.vsix';$theme='Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix'
    New-SyntheticVsix (Join-Path $root $ai) 'Grokeen.VS2017.AIAssistant' '0.15.0' '[15.0,16.0)' 'x86'
    $lines=@((Get-FileHash -LiteralPath (Join-Path $root $ai)).Hash+'  '+$ai)
    if($layout -ne 'missing'){
        $relative=if($layout -eq 'nested'){'Themes/'+$theme}else{$theme}
        $path=Join-Path $root $relative
        New-SyntheticVsix $path $id $version $range $arch
        $lines+=@((Get-FileHash -LiteralPath $path).Hash+'  '+$relative)
    }
    [IO.File]::WriteAllLines((Join-Path $root 'SHA256SUMS.txt'),[string[]]$lines)
    return $root
}
$instance=[pscustomobject]@{instanceId='synthetic';productId='Microsoft.VisualStudio.Product.Community';displayName='synthetic';installationVersion='15.9.28307.1';installationPath=$out;isComplete=$true;isLaunchable=$true}
function Check([string]$name,[string]$layout,[bool]$requestTheme,[scriptblock]$change,[string]$expected){
    $root=New-ThemeFixture $name $layout 'Grokeen.VisualStudioThemes.2017' '0.15.0' '[15.0,16.0)' 'x86'
    if($null -ne $change){& $change $root}
    $script:InstallTheme=$requestTheme;$script:RequireTheme=$false
    $plan=Get-AIAssistantInstallPlan -Instance $instance -Directory $root -CoreEditorPresent $true -Architecture 'x86'
    if($plan.Status -cne $expected){throw "$name returned unexpected plan status: $($plan.Status)"}
    if($expected -eq 'Ready' -and $requestTheme -and $null -eq $plan.ThemePackage){throw "$name omitted the theme"}
    if($expected -eq 'Ready' -and !$requestTheme -and $null -ne $plan.ThemePackage){throw "$name unexpectedly requested the theme"}
    $script:results+=[pscustomobject]@{Case=$name;Status=$plan.Status;ThemeRequested=$requestTheme}
}
Check 'flat' 'flat' $true $null 'Ready'
Check 'nested' 'nested' $true $null 'Ready'
Check 'missing' 'missing' $true $null 'InvalidThemePackage'
Check 'without-theme-option' 'missing' $false $null 'Ready'
Check 'both-locations' 'flat' $true {param($r) $d=Join-Path $r 'Themes';[void][IO.Directory]::CreateDirectory($d);Copy-Item -LiteralPath (Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix') -Destination $d} 'InvalidThemePackage'
Check 'path-mismatch' 'flat' $true {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($p)|ForEach-Object {$_ -replace '  Grokeen-','  Themes/Grokeen-'});[IO.File]::WriteAllLines($p,[string[]]$rows)} 'InvalidThemePackage'
Check 'tampered' 'flat' $true {param($r) [IO.File]::AppendAllText((Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix'),'tampered')} 'InvalidThemePackage'
Check 'wrong-id' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Not.Grokeen' '0.15.0' '[15.0,16.0)' 'x86';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage'
Check 'wrong-version' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Grokeen.VisualStudioThemes.2017' '0.11.1' '[15.0,16.0)' 'x86';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage'
Check 'wrong-target' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Grokeen.VisualStudioThemes.2017' '0.15.0' '[16.0,17.0)' 'x86';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage'
Check 'wrong-arch' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.0-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Grokeen.VisualStudioThemes.2017' '0.15.0' '[15.0,16.0)' 'amd64';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage'
[pscustomobject]@{Succeeded=$true;Checks=$results;ProductScriptSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'scripts/Install-AIAssistant.ps1')).Hash;ActualVsixInstallerExecuted=$false;ActualIDE=$false;NetworkUsed=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' synthetic theme layout and install-plan cases; no installer was run.')
