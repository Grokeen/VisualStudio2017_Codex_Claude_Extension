[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
foreach($m in @('Microsoft.PowerShell.Management','Microsoft.PowerShell.Utility')){Import-Module (Join-Path $PSHOME ('Modules/'+$m+'/'+$m+'.psd1'))}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $out){throw 'Use a new output directory.'}
[void][IO.Directory]::CreateDirectory($out)
$source=Join-Path $out 'light';[void][IO.Directory]::CreateDirectory($source)
foreach($name in @('Prepare-OfflineSetup.ps1','OfflineCli.Common.ps1','OfflinePackage.Common.ps1','Install-OfflineCli.ps1','Install-AIAssistant.ps1','Test-OfflineSetupOnTarget.ps1')){Copy-Item -LiteralPath (Join-Path $repo ('scripts/'+$name)) -Destination $source}
foreach($name in @('Install-AIAssistant.ps1','Test-OfflineSetupOnTarget.ps1')){[IO.File]::WriteAllText((Join-Path $source $name),'param([string]$PackageVersion=''0.15.0'') Set-Content -LiteralPath (Join-Path $PSScriptRoot ''executed-marker.txt'') -Value incorrect')}
[IO.File]::WriteAllText((Join-Path $source 'Install-OfflineCli.ps1'),'Set-Content -LiteralPath (Join-Path $PSScriptRoot ''executed-marker.txt'') -Value incorrect')
foreach($name in @('InstallOfflineCLI.bat','InstallAIAssistant.bat')){Copy-Item -LiteralPath (Join-Path $repo $name) -Destination $source}
Copy-Item -LiteralPath (Join-Path $repo 'docs/OFFLINE_INSTALL.md') -Destination $source
$templates=Join-Path $source 'offline-templates';[void][IO.Directory]::CreateDirectory($templates)
Copy-Item -LiteralPath (Join-Path $repo 'VerifyOfflineSetup.bat') -Destination $templates
Copy-Item -LiteralPath (Join-Path $repo 'scripts/Verify-OfflineFiles.ps1') -Destination $templates
$names=@('Verify-OfflineFiles.ps1','VerifyOfflineSetup.bat')
$rows=@(foreach($name in $names){(Get-FileHash -LiteralPath (Join-Path $templates $name)).Hash+'  '+$name})
[IO.File]::WriteAllLines((Join-Path $templates 'SHA256SUMS.txt'),[string[]]$rows)
foreach($name in $names){if(Test-Path -LiteralPath (Join-Path $source $name)){throw 'Lightweight root unexpectedly contains a runnable verification tool.'}}
$extension=Join-Path $out 'extensions';[void][IO.Directory]::CreateDirectory($extension)
$names=@('VS2017.15.7.AIAssistant-0.15.1.vsix','VS2017.AIAssistant-0.15.1.vsix','VS2019.AIAssistant-0.15.1.vsix','VS2022.AIAssistant-0.15.1.vsix','Grokeen-VisualStudio-Themes-0.15.1-VS2017.vsix','Grokeen-VisualStudio-Themes-0.15.1-VS2019.vsix','Grokeen-VisualStudio-Themes-0.15.1-VS2022.vsix')
$rows=@(foreach($name in $names){$p=Join-Path $extension $name;[IO.File]::WriteAllText($p,'synthetic '+$name);(Get-FileHash -LiteralPath $p).Hash+'  '+$name})
[IO.File]::WriteAllLines((Join-Path $extension 'SHA256SUMS.txt'),[string[]]$rows)
$codex=Join-Path $out 'codex';[void][IO.Directory]::CreateDirectory($codex)
@{layoutVersion=1;version='1.2.3';target='x86_64-pc-windows-msvc';entrypoint='bin/codex.exe';resourcesDir='codex-resources';pathDir='codex-path'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $codex 'codex-package.json') -Encoding UTF8
$fakePe=New-Object byte[] 70;$fakePe[0]=0x4d;$fakePe[1]=0x5a;$fakePe[0x3c]=64;$fakePe[64]=0x50;$fakePe[65]=0x45;$fakePe[68]=0x64;$fakePe[69]=0x86
foreach($name in @('bin/codex.exe','bin/codex-code-mode-host.exe','codex-path/rg.exe','codex-resources/codex-command-runner.exe','codex-resources/codex-windows-sandbox-setup.exe')){$p=Join-Path $codex $name;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p));[IO.File]::WriteAllBytes($p,$fakePe)}
$claude=Join-Path $out 'fake-claude.exe';[IO.File]::WriteAllBytes($claude,$fakePe)
$sentinel=Join-Path $out 'outside-sentinel.txt';[IO.File]::WriteAllText($sentinel,'unchanged');$sentinelHash=(Get-FileHash -LiteralPath $sentinel).Hash
# Only the owned fixture copy is patched: these two dependencies cannot be met by fake unsigned executables.
$prepare=Join-Path $source 'Prepare-OfflineSetup.ps1';$originalHash=(Get-FileHash -LiteralPath $prepare).Hash;$text=[IO.File]::ReadAllText($prepare)
$pattern='(?s)function Assert-OfflineSourcePublisher \{.*?\r?\n\}'
if([regex]::Matches($text,$pattern).Count -ne 1){throw 'Synthetic publisher seam not found.'}
$stub='function Assert-OfflineSourcePublisher { param([string]$Path,[string]$Publisher) return [pscustomobject]@{Name=[IO.Path]::GetFileName($Path);Publisher="synthetic";Thumbprint="synthetic";Status="synthetic";Sha256=(Get-FileHash -LiteralPath $Path).Hash} }'
$text=[regex]::Replace($text,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m)$stub})
$needle='[Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($ClaudeExecutable)).ProductVersion'
if(!$text.Contains($needle)){throw 'Synthetic version seam not found.'}
$text=$text.Replace($needle,"'4.5.6'")
[IO.File]::WriteAllText($prepare,$text,(New-Object Text.UTF8Encoding($true)))
$results=@()
function Run-Preparation([string]$name,[string]$shell,[bool]$expected,[string]$reason){
    $destination=Join-Path $out ('full-'+$name+'-'+[IO.Path]::GetFileNameWithoutExtension($shell))
    $log=Join-Path $out ($name+'-'+[IO.Path]::GetFileNameWithoutExtension($shell)+'.log')
    $oldPreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
    try{$messages=@(& $shell -NoProfile -ExecutionPolicy Bypass -File $prepare -OutputDirectory $destination -CodexPackageDirectory $codex -ClaudeExecutable $claude -ExtensionPackageDirectory $extension -PackageVersion '0.15.1' 2>&1);$code=$LASTEXITCODE}finally{$ErrorActionPreference=$oldPreference}
    [IO.File]::WriteAllLines($log,[string[]]@($messages|ForEach-Object {$_.ToString()}),(New-Object Text.UTF8Encoding($false)))
    if((($code -eq 0) -ne $expected) -or (!$expected -and (($messages -join ' ') -notmatch [regex]::Escape($reason)))){throw "$name unexpected preparation result: $($messages -join ' ')"}
    if($expected){
        if(!(Test-Path -LiteralPath (Join-Path $destination 'VerifyOfflineSetup.bat')) -or !(Test-Path -LiteralPath (Join-Path $destination 'Verify-OfflineFiles.ps1'))){throw 'Full output verification tools missing.'}
        $verification=@(& $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $destination 'Verify-OfflineFiles.ps1') 2>&1);if($LASTEXITCODE -ne 0){throw "Full output verification failed: $verification"}
        $verification=@(& $env:ComSpec /d /c ('""'+(Join-Path $destination 'VerifyOfflineSetup.bat')+'" --noninteractive"') 2>&1);if($LASTEXITCODE -ne 0){throw "Full output batch verification failed: $verification"}
        if(Test-Path -LiteralPath (Join-Path $destination 'executed-marker.txt')){throw 'An installer test stub was invoked.'}
        if((Get-Content -Raw -LiteralPath (Join-Path $destination 'VerifyOfflineSetup.bat')) -notmatch 'OFFLINE_PACKAGE_VERSION=0\.15\.1'){throw 'Incorrect batch version binding.'}
        if(Test-Path -LiteralPath (Join-Path $destination 'offline-templates')){throw 'Template directory leaked into prepared output.'}
    }elseif(Test-Path -LiteralPath $destination){throw 'Invalid fixture created output.'}
    if((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash){throw 'Outside sentinel changed.'}
    $script:results+=[pscustomobject]@{Case=$name;Shell=$shell;ExitCode=$code;ExpectedReason=$reason;Log=$log;LogSHA256=(Get-FileHash -LiteralPath $log).Hash;OutsideSentinelUnchanged=$true}
}
$shells=@('C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe',(Get-Command pwsh.exe).Source)
$shellVersions=@{};foreach($shell in $shells){$shellVersions[$shell]=(& $shell -NoProfile -Command '$PSVersionTable.PSVersion.ToString()')}
foreach($shell in $shells){Run-Preparation 'light' $shell $true ''}
Copy-Item -LiteralPath (Join-Path $templates 'VerifyOfflineSetup.bat') -Destination $source
Copy-Item -LiteralPath (Join-Path $templates 'Verify-OfflineFiles.ps1') -Destination $source
[IO.File]::AppendAllText((Join-Path $templates 'Verify-OfflineFiles.ps1'),'bad')
foreach($shell in $shells){Run-Preparation 'root-priority' $shell $true ''}
Remove-Item -LiteralPath (Join-Path $source 'Verify-OfflineFiles.ps1')
foreach($shell in $shells){Run-Preparation 'root-mixed' $shell $false 'Verification root template pair is incomplete.'}
Remove-Item -LiteralPath (Join-Path $source 'VerifyOfflineSetup.bat')
foreach($shell in $shells){Run-Preparation 'tampered' $shell $false 'Verification template is missing or checksum mismatch.'}
Copy-Item -LiteralPath (Join-Path $repo 'scripts/Verify-OfflineFiles.ps1') -Destination (Join-Path $templates 'Verify-OfflineFiles.ps1') -Force
Remove-Item -LiteralPath (Join-Path $templates 'VerifyOfflineSetup.bat')
foreach($shell in $shells){Run-Preparation 'missing' $shell $false 'Verification template is missing or checksum mismatch.'}
Copy-Item -LiteralPath (Join-Path $repo 'VerifyOfflineSetup.bat') -Destination $templates
$list=Join-Path $templates 'SHA256SUMS.txt';$saved=[IO.File]::ReadAllLines($list);[IO.File]::WriteAllLines($list,[string[]]@($saved[0],$saved[0]))
foreach($shell in $shells){Run-Preparation 'duplicate' $shell $false 'Duplicate verification template checksum entry.'}
[IO.File]::WriteAllLines($list,[string[]]$saved)
Remove-Item -LiteralPath $list
foreach($shell in $shells){Run-Preparation 'missing-list' $shell $false 'Verification template checksum list is missing.'}
[IO.File]::WriteAllLines($list,[string[]]@($saved[0],(('A'*64)+'  ../outside.txt')))
foreach($shell in $shells){Run-Preparation 'unsafe-entry' $shell $false 'Invalid verification template checksum entry.'}
[IO.File]::WriteAllLines($list,[string[]]$saved)
$listTarget=Join-Path $out 'template-checksums-target.txt';Copy-Item -LiteralPath $list -Destination $listTarget
Remove-Item -LiteralPath $list
$linkCreatorRuntime=$PSVersionTable.PSVersion.ToString();$linkCreation='direct'
try{[void](New-Item -ItemType SymbolicLink -Path $list -Target $listTarget)}
catch{
    if($_.FullyQualifiedErrorId -notlike 'NewItemSymbolicLinkElevationRequired*'){throw}
    $helper=Join-Path $out 'Create-TestFileLink.ps1'
    [IO.File]::WriteAllText($helper,'param($LinkPath,$TargetPath) $ErrorActionPreference="Stop"; [void](New-Item -ItemType SymbolicLink -Path $LinkPath -Target $TargetPath); $PSVersionTable.PSVersion.ToString()')
    $pwsh=Get-Command pwsh.exe -ErrorAction Stop
    $oldPreference=$ErrorActionPreference;$ErrorActionPreference='Continue'
    try{$helperOutput=@(& $pwsh.Source -NoProfile -File $helper $list $listTarget 2>&1);$helperExit=$LASTEXITCODE}finally{$ErrorActionPreference=$oldPreference}
    if($helperExit -ne 0){throw ('Test-only symbolic-link helper failed: '+($helperOutput -join ' '))}
    $linkCreatorRuntime=[string]$helperOutput[-1];$linkCreation='pwsh-helper'
}
$linkItem=Get-Item -LiteralPath $list -Force
$linkTargetHash=(Get-FileHash -LiteralPath $listTarget).Hash
if(!($linkItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $linkItem.LinkType -ne 'SymbolicLink' -or [IO.Path]::GetFullPath([string]$linkItem.Target) -ne [IO.Path]::GetFullPath($listTarget) -or (Get-FileHash -LiteralPath $list).Hash -ne $linkTargetHash){throw 'Test-only checksum symlink differs from its owned target.'}
foreach($shell in $shells){Run-Preparation 'checksum-link' $shell $false '연결 경로는 사용하지 않습니다'}
if(!((Get-Item -LiteralPath $list -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Refuse removal of non-link checksum fixture.'}
[IO.File]::Delete($list)
if((Get-FileHash -LiteralPath $listTarget).Hash -ne $linkTargetHash){throw 'Test-only link target changed.'}
Copy-Item -LiteralPath $listTarget -Destination $list
$linkSource=Join-Path $out 'template-target';Move-Item -LiteralPath $templates -Destination $linkSource
[void](New-Item -ItemType Junction -Path $templates -Target $linkSource)
foreach($shell in $shells){Run-Preparation 'junction' $shell $false '연결 경로는 사용하지 않습니다'}
[pscustomobject]@{Checks=$results;ShellVersions=$shellVersions;TestScriptSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;OriginalPrepareSha256=$originalHash;FixturePrepareSha256=(Get-FileHash -LiteralPath $prepare).Hash;CommonSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'scripts/OfflinePackage.Common.ps1')).Hash;VerifierSha256=(Get-FileHash -LiteralPath (Join-Path $repo 'scripts/Verify-OfflineFiles.ps1')).Hash;ChecksumLinkCreation=$linkCreation;ChecksumLinkCreatorRuntime=$linkCreatorRuntime;ChecksumLinkTargetSHA256=$linkTargetHash;LightRootVerificationAbsent=$true;PublisherAndClaudeVersionStubbed=$true;InstallerExecuted=$false;CliExecuted=$false;NetworkUsed=$false;OutsideSentinelSha256=$sentinelHash;OutsideSentinelUnchanged=$true}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' synthetic preparation cases; publisher and Claude version checks stubbed; no CLI or installer execution.')
