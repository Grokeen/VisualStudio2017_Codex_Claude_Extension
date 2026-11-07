[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
foreach($module in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')){Import-Module -Name (Join-Path $PSHOME ('Modules/'+$module+'/'+$module+'.psd1')) -ErrorAction Stop}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'scripts/Install-AIAssistant.ps1') -LibraryOnly -PackageVersion '0.15.1'
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
    $ai='VS2017.AIAssistant-0.15.1.vsix';$theme='Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix'
    New-SyntheticVsix (Join-Path $root $ai) 'Grokeen.VS2017.AIAssistant' '0.15.1' '[15.0,16.0)' 'x86'
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
function Check([string]$name,[string]$layout,[bool]$requestTheme,[scriptblock]$change,[string]$expected,[string]$expectedReason){
    $root=New-ThemeFixture $name $layout 'Grokeen.VisualStudioThemes.2017' '0.15.1' '[15.0,16.0)' 'x86'
    if($null -ne $change){& $change $root}
    $script:InstallTheme=$requestTheme;$script:RequireTheme=$false
    $plan=Get-AIAssistantInstallPlan -Instance $instance -Directory $root -CoreEditorPresent $true -Architecture 'x86'
    if($plan.Status -cne $expected){throw "$name returned unexpected plan status: $($plan.Status)"}
    if($expected -ne 'Ready' -and !$plan.Reason.Contains($expectedReason)){throw "$name failed for an unexpected reason: $($plan.Reason)"}
    if($expected -eq 'Ready' -and $requestTheme -and $null -eq $plan.ThemePackage){throw "$name omitted the theme"}
    if($expected -eq 'Ready' -and !$requestTheme -and $null -ne $plan.ThemePackage){throw "$name unexpectedly requested the theme"}
    $log=Join-Path $out ($name+'.log');[IO.File]::WriteAllText($log,('status='+$plan.Status+'; reason='+$plan.Reason),(New-Object Text.UTF8Encoding($false)))
    $script:results+=[pscustomobject]@{Case=$name;Status=$plan.Status;ThemeRequested=$requestTheme;ExpectedReason=$expectedReason;Runtime=$PSVersionTable.PSVersion.ToString();Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
}
Check 'flat' 'flat' $true $null 'Ready' ''
Check 'nested' 'nested' $true $null 'Ready' ''
Check 'missing' 'missing' $true $null 'InvalidThemePackage' '테마 설치 파일이 없거나 두 위치에 중복됩니다'
Check 'without-theme-option' 'missing' $false $null 'Ready' ''
Check 'both-locations' 'flat' $true {param($r) $d=Join-Path $r 'Themes';[void][IO.Directory]::CreateDirectory($d);Copy-Item -LiteralPath (Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix') -Destination $d} 'InvalidThemePackage' '테마 설치 파일이 없거나 두 위치에 중복됩니다'
Check 'path-mismatch' 'flat' $true {param($r) $p=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($p)|ForEach-Object {$_ -replace '  Grokeen-','  Themes/Grokeen-'});[IO.File]::WriteAllLines($p,[string[]]$rows)} 'InvalidThemePackage' '테마 파일과 체크섬 경로가 다릅니다'
Check 'tampered' 'flat' $true {param($r) [IO.File]::AppendAllText((Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix'),'tampered')} 'InvalidThemePackage' '테마 파일 체크섬 불일치'
Check 'wrong-id' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Not.Grokeen' '0.15.1' '[15.0,16.0)' 'x86';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage' '테마 VSIX 제품 ID/버전이 다릅니다'
Check 'wrong-version' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Grokeen.VisualStudioThemes.2017' '0.11.1' '[15.0,16.0)' 'x86';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage' '테마 VSIX 제품 ID/버전이 다릅니다'
Check 'wrong-target' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Grokeen.VisualStudioThemes.2017' '0.15.1' '[16.0,17.0)' 'x86';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage' '테마 IDE 대상 범위가 다릅니다'
Check 'wrong-arch' 'flat' $true {param($r) $p=Join-Path $r 'Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix';Remove-Item -LiteralPath $p;New-SyntheticVsix $p 'Grokeen.VisualStudioThemes.2017' '0.15.1' '[15.0,16.0)' 'amd64';$s=Join-Path $r 'SHA256SUMS.txt';$rows=@([IO.File]::ReadAllLines($s));$rows[1]=(Get-FileHash -LiteralPath $p).Hash+'  '+[IO.Path]::GetFileName($p);[IO.File]::WriteAllLines($s,[string[]]$rows)} 'InvalidThemePackage' '테마 IDE 아키텍처가 다릅니다'
function Check-LinkedPlan([string]$name,[string]$directory,[bool]$requestTheme,[string]$expected) {
    $script:InstallTheme=$requestTheme;$script:RequireTheme=$false
    $plan=Get-AIAssistantInstallPlan -Instance $instance -Directory $directory -CoreEditorPresent $true -Architecture 'x86'
    if($plan.Status -cne $expected -or ($expected -ne 'Ready' -and !$plan.Reason.Contains('Reparse paths are not accepted'))){throw "$name unexpected: $($plan.Status) $($plan.Reason)"}
    $log=Join-Path $out ($name+'.log');[IO.File]::WriteAllText($log,('status='+$plan.Status+'; reason='+$plan.Reason),(New-Object Text.UTF8Encoding($false)))
    $script:results+=[pscustomobject]@{Case=$name;Status=$plan.Status;ThemeRequested=$requestTheme;ExpectedReason='Reparse paths are not accepted';Runtime=$PSVersionTable.PSVersion.ToString();Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
}
$outside=Join-Path $out 'outside';[void][IO.Directory]::CreateDirectory($outside)
$sentinel=Join-Path $outside 'sentinel.txt';[IO.File]::WriteAllText($sentinel,'unchanged');$sentinelHash=(Get-FileHash -LiteralPath $sentinel).Hash
$real=New-ThemeFixture 'outside/package' 'flat' 'Grokeen.VisualStudioThemes.2017' '0.15.1' '[15.0,16.0)' 'x86'
$link=Join-Path $out 'package-junction';[void](New-Item -ItemType Junction -Path $link -Target $real)
Check-LinkedPlan 'package-junction' $link $true 'InvalidPackage'
$link=Join-Path $out 'ancestor-junction';[void](New-Item -ItemType Junction -Path $link -Target $outside)
Check-LinkedPlan 'ancestor-junction' (Join-Path $link 'package') $true 'InvalidPackage'
foreach($file in @('SHA256SUMS.txt','VS2017.AIAssistant-0.15.1.vsix','Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix')){
    $name=if($file -eq 'SHA256SUMS.txt'){'checksum-link'}elseif($file.StartsWith('VS')){'ai-link'}else{'theme-link'}
    $root=New-ThemeFixture $name 'flat' 'Grokeen.VisualStudioThemes.2017' '0.15.1' '[15.0,16.0)' 'x86'
    $path=Join-Path $root $file;$target=Join-Path $outside ($name+'.target');Copy-Item -LiteralPath $path -Destination $target
    Remove-Item -LiteralPath $path
    try{[void](New-Item -ItemType SymbolicLink -Path $path -Target $target)}
    catch{
        if($_.FullyQualifiedErrorId -notlike 'NewItemSymbolicLinkElevationRequired*'){throw}
        $log=Join-Path $out ($name+'.log');[IO.File]::WriteAllText($log,'SKIPPED: operating system did not permit creation of the symbolic-link fixture.',(New-Object Text.UTF8Encoding($false)))
        $script:results+=[pscustomobject]@{Case=$name;Status='Skipped';ThemeRequested=$true;ExpectedReason='Symbolic-link fixture creation requires privilege';Runtime=$PSVersionTable.PSVersion.ToString();Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash}
        continue
    }
    $expected=if($name -eq 'theme-link'){'InvalidThemePackage'}else{'InvalidPackage'}
    Check-LinkedPlan $name $root $true $expected
}
$root=New-ThemeFixture 'theme-junction' 'nested' 'Grokeen.VisualStudioThemes.2017' '0.15.1' '[15.0,16.0)' 'x86'
$themePath=Join-Path $root 'Themes';$themeTarget=Join-Path $outside 'themes';Move-Item -LiteralPath $themePath -Destination $themeTarget
[void](New-Item -ItemType Junction -Path $themePath -Target $themeTarget)
Check-LinkedPlan 'theme-junction' $root $true 'InvalidThemePackage'
Check-LinkedPlan 'unrequested-theme-junction' $root $false 'Ready'
if((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash){throw 'Outside sentinel changed.'}
$skipped=@($results|Where-Object Status -eq 'Skipped').Count
$passed=$results.Count-$skipped
if($results.Count -ne 18){throw 'Theme fixture case inventory differs.'}
[pscustomobject]@{Succeeded=$true;ExpectedCases=18;PassedCases=$passed;SkippedCases=$skipped;FailedCases=0;AllCasesExecuted=($skipped -eq 0);Checks=$results;Runtime=$PSVersionTable.PSVersion.ToString();PSHome=$PSHOME;TestScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;ProductScriptSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'scripts/Install-AIAssistant.ps1')).Hash;ActualVsixInstallerExecuted=$false;ActualIDE=$false;NetworkUsed=$false}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
Write-Output ('PASS '+($results.Count-$skipped)+' synthetic theme layout and install-plan cases; skipped '+$skipped+'; no installer was run.')
