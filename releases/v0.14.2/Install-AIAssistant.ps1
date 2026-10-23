#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Install-AIAssistant
desc         : VSIX 설치 전에 IDE 연도·버전·에디션·아키텍처·무결성을 확인하고 해당 IDE의 설치기를 연다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-036 NoApplicableSKUsException 설치 진단·대상 선택 추가.
               2026-10-20 CodexCode : TASK-069 오프라인 전용 설치기의 버전별 테마 검증·일괄 설치·IDE 종료 대기 추가.
               2026-10-20 CodexCode : 설치기 중지 요청을 IDE 대기 중 즉시, VSIX 경계에서 안전하게 처리.
               2026-10-20 CodexCode : 설치 성공 패키지 수를 중앙 진행 게이지에 전달.
               2026-10-21 CodexOrig : 공식 TEMP 로그 이름 계약과 검증된 진단 사본·로그 누락 경고를 분리.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.14.2',
    [string]$InstanceId,
    [switch]$Install,
    [switch]$InstallTheme,
    [switch]$RequireTheme,
    [switch]$AllReady,
    [switch]$WaitForIdeExit,
    [switch]$CompactOutput,
    [string]$CancelFlagPath,
    [string]$ReportPath,
    [switch]$LibraryOnly,
    [ValidatePattern('^CodexOrig36[a-f0-9]{8}$')][string]$TestRootSuffix
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
-------------------------------------------------------------------------
name         : Read-AIAssistantPackage
desc         : 설치 파일의 체크섬과 실제 매니페스트를 확인한다. ZIP 내용을 디스크로 추출하지 않는다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : DTD와 외부 엔터티 금지·예상 제품 ID/버전 확인.
-------------------------------------------------------------------------
#>
function Read-AIAssistantPackage {
    param([string]$Directory, [string]$Year, [switch]$Legacy)
    $name=if($Legacy){"VS2017.15.7.AIAssistant-$PackageVersion.vsix"}else{"VS$Year.AIAssistant-$PackageVersion.vsix"}
    $path = Join-Path $Directory $name
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "설치 파일이 없습니다: $path" }
    $checksums = Join-Path $Directory 'SHA256SUMS.txt'
    $name = [IO.Path]::GetFileName($path)
    $lines = @([IO.File]::ReadAllLines($checksums) | Where-Object { $_ -match ('^[A-Fa-f0-9]{64}\s+\*?' + [regex]::Escape($name) + '$') })
    if ($lines.Count -ne 1) { throw "SHA256SUMS.txt의 파일 항목이 없거나 중복입니다: $name" }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($hash -ne $lines[0].Substring(0,64)) { throw "파일 체크섬 불일치: $name. 설치 묶음을 다시 받으세요." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries = @($zip.Entries | Where-Object FullName -CEQ 'extension.vsixmanifest')
        if ($entries.Count -ne 1 -or $entries[0].Length -gt 1048576) { throw 'VSIX 매니페스트가 없거나 올바르지 않습니다.' }
        $stream = $entries[0].Open()
        $settings = New-Object Xml.XmlReaderSettings
        $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
        $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create($stream, $settings)
        try { $xml = New-Object Xml.XmlDocument; $xml.XmlResolver = $null; $xml.Load($reader) }
        finally { $reader.Dispose(); $stream.Dispose() }
    } finally { $zip.Dispose() }
    $ns = New-Object Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('v','http://schemas.microsoft.com/developer/vsx-schema/2011')
    $identity = $xml.SelectSingleNode('/v:PackageManifest/v:Metadata/v:Identity',$ns)
    if (!$identity -or $identity.GetAttribute('Id') -ne "Grokeen.VS$Year.AIAssistant" -or $identity.GetAttribute('Version') -ne $PackageVersion) {
        throw "파일 이름과 VSIX 제품 ID/버전이 다릅니다: $name"
    }
    $targets = @($xml.SelectNodes('/v:PackageManifest/v:Installation/v:InstallationTarget',$ns) | ForEach-Object {
        $architecture = $_.SelectSingleNode('v:ProductArchitecture',$ns)
        [pscustomobject]@{ Id=$_.GetAttribute('Id'); Range=$_.GetAttribute('Version'); Architecture=$(if($architecture){$architecture.InnerText}else{'x86'}) }
    })
    return [pscustomobject]@{ Path=[IO.Path]::GetFullPath($path); Version=$PackageVersion; Flavor=$(if($Legacy){'2017-15.7'}else{$Year}); Sha256=$hash; Targets=$targets }
}

<#
-------------------------------------------------------------------------
name         : Read-AIAssistantThemePackage
desc         : 동봉 테마 VSIX의 SHA256, 제품 ID, 버전과 대상 IDE를 설치 전에 확인한다.
author       : CodexCode
create date  : 2026-10-20
-------------------------------------------------------------------------
#>
function Read-AIAssistantThemePackage {
    param([string]$Directory,[string]$Year,[string]$Architecture)
    $name="Grokeen-VisualStudio-Themes-$PackageVersion-VS$Year.vsix"
    $path=Join-Path $Directory $name
    if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "테마 설치 파일이 없습니다: $path"}
    $checksums=Join-Path $Directory 'SHA256SUMS.txt'
    $lines=@([IO.File]::ReadAllLines($checksums)|Where-Object {$_ -match ('^[A-Fa-f0-9]{64}\s+\*?'+[regex]::Escape($name)+'$')})
    if($lines.Count -ne 1){throw "테마 체크섬 항목이 없거나 중복입니다: $name"}
    $hash=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if($hash -ne $lines[0].Substring(0,64)){throw "테마 파일 체크섬 불일치: $name"}
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($path)
    try {
        $entries=@($zip.Entries|Where-Object FullName -CEQ 'extension.vsixmanifest')
        if($entries.Count -ne 1 -or $entries[0].Length -gt 1048576){throw '테마 VSIX 매니페스트가 없거나 올바르지 않습니다.'}
        $stream=$entries[0].Open();$settings=New-Object Xml.XmlReaderSettings
        $settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
        $reader=[Xml.XmlReader]::Create($stream,$settings)
        try{$xml=New-Object Xml.XmlDocument;$xml.XmlResolver=$null;$xml.Load($reader)}
        finally{$reader.Dispose();$stream.Dispose()}
    }finally{$zip.Dispose()}
    $ns=New-Object Xml.XmlNamespaceManager($xml.NameTable)
    $ns.AddNamespace('v','http://schemas.microsoft.com/developer/vsx-schema/2011')
    $identity=$xml.SelectSingleNode('/v:PackageManifest/v:Metadata/v:Identity',$ns)
    if(!$identity -or $identity.GetAttribute('Id') -ne "Grokeen.VisualStudioThemes.$Year" -or $identity.GetAttribute('Version') -ne $PackageVersion){throw "테마 VSIX 제품 ID/버전이 다릅니다: $name"}
    $targets=@($xml.SelectNodes('/v:PackageManifest/v:Installation/v:InstallationTarget',$ns))
    $expectedRange=switch($Year){'2017'{'[15.0,16.0)'}'2019'{'[16.0,17.0)'}'2022'{'[17.0,18.0)'}}
    if($targets.Count -ne 1 -or $targets[0].GetAttribute('Version') -ne $expectedRange){throw "테마 IDE 대상 범위가 다릅니다: $name"}
    $arch=$targets[0].SelectSingleNode('v:ProductArchitecture',$ns)
    $expectedArch=if($arch){$arch.InnerText}else{'x86'}
    if($expectedArch -ne $Architecture){throw "테마 IDE 아키텍처가 다릅니다: $name"}
    return [pscustomobject]@{Path=[IO.Path]::GetFullPath($path);Sha256=$hash;Year=$Year;Version=$PackageVersion}
}

<#
-------------------------------------------------------------------------
name         : Get-AIAssistantInstallPlan
desc         : 실제 설치 버전을 VSIX 규칙으로 정규화하고 Community의 상위 SKU 호환을 적용한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 낮은 빌드·다른 연도·BuildTools·ARM64·불완전 설치 차단.
-------------------------------------------------------------------------
#>
function Get-AIAssistantInstallPlan {
    param($Instance, [string]$Directory, [bool]$CoreEditorPresent, [string]$Architecture)
    $version = [version]$Instance.installationVersion
    $year = switch ($version.Major) { 15 {'2017'} 16 {'2019'} 17 {'2022'} default {''} }
    $status = 'Ready'; $reason = '설치 조건 확인. 최종 의존성 검사는 VSIXInstaller에서 수행합니다.'; $package=$null;$themePackage=$null
    $ranks = @{ 'Microsoft.VisualStudio.Product.Community'=0; 'Microsoft.VisualStudio.Product.Professional'=1; 'Microsoft.VisualStudio.Product.Enterprise'=2 }
    if (!$ranks.ContainsKey([string]$Instance.productId) -or !$year) { $status='UnsupportedProduct'; $reason='지원 대상은 Visual Studio 2017/2019/2022 Community·Professional·Enterprise입니다.' }
    elseif (!$Instance.isComplete -or !$Instance.isLaunchable -or ($Instance.PSObject.Properties['isRebootRequired'] -and $Instance.isRebootRequired)) { $status='IncompleteInstallation'; $reason='Visual Studio Installer에서 설치/복구를 마치고 필요한 재부팅을 완료하세요.' }
    else {
        $legacy=$year -eq '2017' -and $version.Build -lt 28307
        try { $package = Read-AIAssistantPackage -Directory $Directory -Year $year -Legacy:$legacy }
        catch { $status='InvalidPackage'; $reason=$_.Exception.Message }
        if ($package) {
            # VSIX InstallationTarget은 VS2017 이후 minor를 0으로 표현한다. SDK 지원 하한은 낮추지 않는다.
            $normalized = New-Object Version($version.Major,0,[Math]::Max(0,$version.Build),[Math]::Max(0,$version.Revision))
            $targetRanks = @{ 'Microsoft.VisualStudio.Community'=0; 'Microsoft.VisualStudio.Pro'=1; 'Microsoft.VisualStudio.Enterprise'=2 }
            $matching = @($package.Targets | Where-Object {
                if (!$targetRanks.ContainsKey($_.Id) -or $targetRanks[$_.Id] -gt $ranks[[string]$Instance.productId]) { return $false }
                if ($_.Range -notmatch '^\[([0-9.]+),([0-9.]+)\)$') { return $false }
                return $normalized -ge [version]$matches[1] -and $normalized -lt [version]$matches[2]
            })
            if (!$matching.Count) { $status='UnsupportedVersion'; $reason="설치 버전 $version 에 맞지 않습니다. VS2017 15.7/15.8은 15.7 전용, 15.9 이상은 일반 VS2017용을 사용하세요. 15.7 미만은 지원하지 않습니다." }
            elseif (!@($matching | Where-Object Architecture -EQ $Architecture).Count) { $status='ArchitectureMismatch'; $reason="IDE 아키텍처 $Architecture 를 지원하지 않습니다. VS2022 패키지는 x64(amd64)용입니다." }
            elseif (!$CoreEditorPresent) { $status='MissingCoreEditor'; $reason='Visual Studio 핵심 편집기를 찾을 수 없습니다. Visual Studio Installer에서 수정/복구하세요.' }
            elseif($RequireTheme -or $InstallTheme){
                try{$themePackage=Read-AIAssistantThemePackage -Directory $Directory -Year $year -Architecture $Architecture}
                catch{$status='InvalidThemePackage';$reason=$_.Exception.Message}
            }
        }
    }
    return [pscustomobject]@{ InstanceId=$Instance.instanceId; Product=$Instance.productId; DisplayName=$Instance.displayName; InstallationVersion=$Instance.installationVersion; InstallationPath=$Instance.installationPath; Year=$year; Architecture=$Architecture; Status=$status; Reason=$reason; Package=$package; ThemePackage=$themePackage }
}

<#
-------------------------------------------------------------------------
name         : Get-AIAssistantIdeArchitecture
desc         : 운영체제 비트 수 대신 대상 devenv.exe의 PE 머신 값을 읽는다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : VS2022 ARM64를 x64로 잘못 선택하지 않도록 실제 파일 검사.
-------------------------------------------------------------------------
#>
function Get-AIAssistantIdeArchitecture {
    param([string]$Path)
    $reader = New-Object IO.BinaryReader([IO.File]::OpenRead($Path))
    try {
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw 'Invalid executable header.' }
        $reader.BaseStream.Position=0x3c; $offset=$reader.ReadInt32()
        if ($offset -lt 64 -or $offset -gt $reader.BaseStream.Length-6) { throw 'Invalid PE offset.' }
        $reader.BaseStream.Position=$offset
        if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid PE signature.' }
        switch ($reader.ReadUInt16()) { 0x014c {return 'x86'} 0x8664 {return 'amd64'} 0xaa64 {return 'arm64'} default {return 'unknown'} }
    } finally { $reader.Dispose() }
}

<#
-------------------------------------------------------------------------
name         : Get-AIAssistantVisualStudioInstances
desc         : VS2017에 동봉된 구형 vswhere에서도 JSON을 읽고 호출 전 콘솔 인코딩을 복원한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 2.5에서 추가된 -utf8 옵션에 의존하지 않도록 변경.
-------------------------------------------------------------------------
#>
function Get-AIAssistantVisualStudioInstances {
    param([string]$VswherePath,[switch]$RequireCoreEditor)
    $arguments=@('-all','-products','*','-format','json')
    if($RequireCoreEditor){$arguments+=@('-requires','Microsoft.VisualStudio.Component.CoreEditor')}
    $oldEncoding=[Console]::OutputEncoding
    try {
        [Console]::OutputEncoding=New-Object Text.UTF8Encoding($false)
        $raw=& $VswherePath @arguments
        if ($LASTEXITCODE -ne 0) { throw "vswhere 설치 목록 조회 실패: $LASTEXITCODE" }
        $parsed=$raw | ConvertFrom-Json
        return $parsed
    } finally { [Console]::OutputEncoding=$oldEncoding }
}

<#
-------------------------------------------------------------------------
name         : Stop-AIAssistantIfRequested
desc         : 현재 VSIXInstaller 프로세스를 강제 종료하지 않고 다음 설치 경계에서 중지한다.
author       : CodexCode
create date  : 2026-10-20
-------------------------------------------------------------------------
#>
function Stop-AIAssistantIfRequested {
    param([string]$FlagPath,$Report,[string]$Path)
    if(!$FlagPath -or !(Test-Path -LiteralPath $FlagPath -PathType Leaf)){return}
    $Report.Cancelled=$true
    $Report.CancelledAt=[DateTimeOffset]::Now.ToString('o')
    $Report|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $Path -Encoding UTF8
    Write-Host '설치가 중지됐습니다. 이미 완료된 패키지는 유지됩니다.'
    exit 2
}

<#
-------------------------------------------------------------------------
name         : Assert-AIAssistantLogPath
desc         : 로그 원본과 진단 사본에서 경로 재지정을 거절한다.
author       : CodexOrig
create date  : 2026-10-21
-------------------------------------------------------------------------
#>
function Assert-AIAssistantLogPath {
    param([string]$Path)
    $full=[IO.Path]::GetFullPath($Path);$walk=$full
    while($walk){
        if(Test-Path -LiteralPath $walk){
            if(((Get-Item -LiteralPath $walk -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Log path cannot use a reparse point.'}
        }
        $parent=[IO.Path]::GetDirectoryName($walk);if($parent -eq $walk){break};$walk=$parent
    }
    return $full
}

<#
-------------------------------------------------------------------------
name         : New-AIAssistantLogPlan
desc         : 공식 설치기가 지원하는 TEMP basename과 새 진단 사본 이름을 만든다.
author       : CodexOrig
create date  : 2026-10-21
-------------------------------------------------------------------------
#>
function New-AIAssistantLogPlan {
    param([string]$ReportPath)
    $name='VSIXInstaller-'+[guid]::NewGuid().ToString('N')+'.log'
    $source=Assert-AIAssistantLogPath (Join-Path ([IO.Path]::GetTempPath()) $name)
    $archive=Assert-AIAssistantLogPath (Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ReportPath))) $name)
    if(Test-Path -LiteralPath $source){throw 'Unique installer log already exists.'}
    if(Test-Path -LiteralPath $archive){throw 'Preserve existing installer log archive.'}
    return [pscustomobject]@{Name=$name;SourcePath=$source;ArchivePath=$archive;NotBeforeUtc=[DateTime]::UtcNow}
}

<#
-------------------------------------------------------------------------
name         : Complete-AIAssistantLog
desc         : 이 실행의 유일 TEMP 로그만 제한 검증·복사하며 설치 종료 코드와 로그 오류를 분리한다.
author       : CodexOrig
create date  : 2026-10-21
-------------------------------------------------------------------------
#>
function Complete-AIAssistantLog {
    param($Plan)
    $result=[pscustomobject]@{InstallerLog=$null;LogAvailable=$false;InstallerLogSource=$null;RequestedInstallerLog=$Plan.SourcePath;LogCopied=$false;LogBytes=0;LogSha256=$null;LogWarning=$null}
    $sourceStream=$null;$outputStream=$null
    try{
        if($Plan.Name -cnotmatch '^VSIXInstaller-[a-f0-9]{32}\.log$'){throw 'Invalid unique installer log name.'}
        $source=Assert-AIAssistantLogPath $Plan.SourcePath
        $expected=[IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) $Plan.Name))
        if(![string]::Equals($source,$expected,[StringComparison]::OrdinalIgnoreCase)){throw 'Installer log is outside its exact TEMP path.'}
        if(!(Test-Path -LiteralPath $source -PathType Leaf)){throw 'Installer did not produce its requested log.'}
        $file=Get-Item -LiteralPath $source
        if($file.Length -le 0 -or $file.Length -gt 16777216){throw 'Installer log is empty or exceeds 16MB.'}
        $earliest=$Plan.NotBeforeUtc.AddSeconds(-2);$latest=[DateTime]::UtcNow.AddSeconds(2)
        if($file.CreationTimeUtc -lt $earliest -or $file.LastWriteTimeUtc -lt $earliest -or $file.CreationTimeUtc -gt $latest -or $file.LastWriteTimeUtc -gt $latest){throw 'Installer log is outside this installation time window.'}
        $sourceStream=[IO.File]::Open($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        if($sourceStream.Length -ne $file.Length){throw 'Installer log changed before reading.'}
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$hash=[BitConverter]::ToString($sha.ComputeHash($sourceStream)).Replace('-','')}finally{$sha.Dispose()}
        $sourceStream.Position=0
        $result.InstallerLog=$source;$result.InstallerLogSource=$source;$result.LogAvailable=$true;$result.LogBytes=$sourceStream.Length;$result.LogSha256=$hash
        $archive=Assert-AIAssistantLogPath $Plan.ArchivePath
        if([IO.Path]::GetFileName($archive) -cne $Plan.Name){throw 'Installer log archive name differs.'}
        if(![string]::Equals($source,$archive,[StringComparison]::OrdinalIgnoreCase)){
            $outputStream=[IO.File]::Open($archive,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
            $sourceStream.CopyTo($outputStream);$outputStream.Flush($true);$outputStream.Dispose();$outputStream=$null
            if((Get-Item -LiteralPath $archive).Length -ne $result.LogBytes -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $hash){throw 'Installer log archive verification failed.'}
            $result.InstallerLog=$archive;$result.LogCopied=$true
        }
    }catch{
        $result.LogWarning=if($result.LogAvailable){'설치 로그는 TEMP에서 확인됐으나 진단 폴더 복사본을 만들지 못했습니다.'}else{'설치 종료 코드와 별개로 공식 설치 로그를 확인하지 못했습니다. 진단 JSON을 보존하세요.'}
    }finally{
        if($outputStream){$outputStream.Dispose()};if($sourceStream){$sourceStream.Dispose()}
    }
    return $result
}

<#
-------------------------------------------------------------------------
name         : Invoke-AIAssistantVsixInstaller
desc         : 설치 UI를 유지하고 실제 존재하는 로그만 결과와 안내에 포함한다.
author       : CodexOrig
create date  : 2026-10-21
-------------------------------------------------------------------------
#>
function Invoke-AIAssistantVsixInstaller {
    param([string]$Installer,[string]$InstanceId,[string]$PackagePath,[string]$ReportPath,[string]$TestRootSuffix)
    if($InstanceId -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Invalid installation instance ID.'}
    if($TestRootSuffix -and $TestRootSuffix -notmatch '^CodexOrig36[a-f0-9]{8}$'){throw 'Invalid isolated test suffix.'}
    $plan=New-AIAssistantLogPlan $ReportPath
    $arguments='/instanceIds:'+$InstanceId+' /logFile:'+$plan.Name+' "'+$PackagePath+'"'
    if($TestRootSuffix){$arguments='/quiet /rootSuffix:'+$TestRootSuffix+' '+$arguments}
    $process=$null
    try{
        if($TestRootSuffix){$process=Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden}
        else{$process=Start-Process -FilePath $Installer -ArgumentList $arguments -Wait -PassThru}
        $exitCode=$process.ExitCode
    }finally{if($process){$process.Dispose()}}
    $log=Complete-AIAssistantLog $plan
    if($log.LogAvailable){Write-Host ('설치 로그: '+$log.InstallerLog)}
    if($log.LogWarning){Write-Warning $log.LogWarning}
    return [pscustomobject]@{ExitCode=$exitCode;InstallerLog=$log.InstallerLog;LogAvailable=$log.LogAvailable;InstallerLogSource=$log.InstallerLogSource;RequestedInstallerLog=$log.RequestedInstallerLog;LogCopied=$log.LogCopied;LogBytes=$log.LogBytes;LogSha256=$log.LogSha256;LogWarning=$log.LogWarning}
}

if ($LibraryOnly) { return }
try {
    if (!$PackageDirectory) {
        if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.txt')){$PackageDirectory=$PSScriptRoot}
        else {$PackageDirectory=Join-Path (Split-Path -Parent $PSScriptRoot) ('releases/v'+$PackageVersion)}
    }
    $PackageDirectory=[IO.Path]::GetFullPath($PackageDirectory)
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (!(Test-Path -LiteralPath $vswhere)) { throw 'Visual Studio Installer/vswhere.exe가 없습니다. Visual Studio Installer를 설치/복구하세요.' }
    $instances=@(Get-AIAssistantVisualStudioInstances $vswhere)
    $coreIds=@(Get-AIAssistantVisualStudioInstances $vswhere -RequireCoreEditor | ForEach-Object instanceId)
    $plans=@(foreach($instance in $instances) {
        try { $architecture=Get-AIAssistantIdeArchitecture (Join-Path $instance.installationPath 'Common7/IDE/devenv.exe') } catch { $architecture='unknown' }
        Get-AIAssistantInstallPlan $instance $PackageDirectory ($coreIds -contains $instance.instanceId) $architecture
    })
    $report=[pscustomobject]@{ CheckedAt=[DateTimeOffset]::Now.ToString('o'); CheckKind='InstallationPreflight'; CustomerFailureCauseConfirmed=$false; PackageDirectory=$PackageDirectory; Plans=$plans; InstallationResults=@(); Cancelled=$false; CancelledAt=$null }
    if (!$ReportPath) { $ReportPath=Join-Path ([IO.Path]::GetTempPath()) ('AI-Assistant-install-'+[guid]::NewGuid().ToString('N')+'.json') }
    if ([IO.Path]::GetExtension($ReportPath) -ne '.json' -or (Test-Path -LiteralPath $ReportPath)) { throw '새 .json 진단 파일 경로를 지정하세요. 기존 파일은 보존합니다.' }
    $report | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    if($CompactOutput){
        foreach($plan in $plans){
            if($plan.Status -eq 'Ready'){Write-Host ("{0}  /  {1}  /  {2}  : 준비 완료" -f $plan.DisplayName,$plan.InstallationVersion,$plan.Architecture)}
            else{Write-Host ("{0}  /  {1}  : {2} - {3}" -f $plan.DisplayName,$plan.InstallationVersion,$plan.Status,$plan.Reason)}
        }
    }else{
        $plans | Select-Object InstanceId,DisplayName,InstallationVersion,Architecture,Status,Reason | Format-Table -Wrap -AutoSize | Out-Host
    }
    Write-Host "진단 파일: $ReportPath"
    if (!$plans.Count) { throw '설치된 Visual Studio를 찾지 못했습니다.' }
    if (!$Install) { return }
    Stop-AIAssistantIfRequested $CancelFlagPath $report $ReportPath
    if($AllReady -and $InstanceId){throw '전체 설치와 단일 인스턴스 선택은 동시에 지정할 수 없습니다.'}
    $ready=@($plans | Where-Object Status -EQ 'Ready')
    if ($InstanceId) { $ready=@($ready | Where-Object InstanceId -CEQ $InstanceId) }
    if (!$ready.Count) { throw '설치 가능한 대상이 없습니다. 위 진단 내용을 확인하세요.' }
    if ($ready.Count -gt 1 -and !$AllReady) {
        for($i=0;$i -lt $ready.Count;$i++) { Write-Host ("{0}. {1} ({2})" -f ($i+1),$ready[$i].DisplayName,$ready[$i].InstallationVersion) }
        $choice=Read-Host '설치할 Visual Studio 번호 (취소: Enter)'
        $index=0
        if (![int]::TryParse($choice,[ref]$index) -or $index -lt 1 -or $index -gt $ready.Count) { Write-Host '설치를 취소했습니다.'; return }
        $ready=@($ready[$index-1])
    }
    $totalPackages=$ready.Count * $(if($InstallTheme){2}else{1})
    $completedPackages=0
    foreach($selected in $ready){
        Stop-AIAssistantIfRequested $CancelFlagPath $report $ReportPath
        # 공식 설치기는 실행 중인 IDE에 적용할 수 없다. 사용자 창을 강제로 닫지 않고 종료를 기다린다.
        if($WaitForIdeExit){
            $until=[DateTime]::UtcNow.AddMinutes(30)
            $announced=$false
            while(@(Get-Process -Name devenv -ErrorAction SilentlyContinue).Count){
                Stop-AIAssistantIfRequested $CancelFlagPath $report $ReportPath
                if(!$announced){Write-Host 'Visual Studio가 실행 중입니다. 작업을 저장하고 모든 Visual Studio 창을 닫으면 설치를 자동으로 계속합니다.';$announced=$true}
                if([DateTime]::UtcNow -ge $until){throw 'Visual Studio 종료 대기 30분을 초과했습니다. IDE를 닫고 다시 실행하세요.'}
                Start-Sleep -Seconds 2
            }
        }
        Stop-AIAssistantIfRequested $CancelFlagPath $report $ReportPath
        # 명령행은 검증한 instanceId와 로컬 경로만 사용한다. cmd.exe와 문자열 실행은 사용하지 않는다.
        if ($selected.InstanceId -notmatch '^[a-zA-Z0-9_-]+$') { throw '올바르지 않은 설치 인스턴스 ID입니다.' }
        $installer=Join-Path $selected.InstallationPath 'Common7/IDE/VSIXInstaller.exe'
        if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { throw '선택한 IDE의 VSIXInstaller.exe가 없습니다. Visual Studio를 복구하세요.' }
        $packages=@([pscustomobject]@{Kind='AI extension';File=$selected.Package})
        if($InstallTheme){
            if(!$selected.ThemePackage){throw '테마 패키지를 확인하지 못했습니다.'}
            $packages+= [pscustomobject]@{Kind='Visual Studio themes';File=$selected.ThemePackage}
        }
        foreach($item in $packages){
            Stop-AIAssistantIfRequested $CancelFlagPath $report $ReportPath
            if((Get-FileHash -LiteralPath $item.File.Path -Algorithm SHA256).Hash -ne $item.File.Sha256){throw "검사 후 패키지가 변경됐습니다: $($item.File.Path)"}
            Write-Host "설치 대상: $($selected.DisplayName) / $($selected.InstallationVersion) / $($item.Kind)"
            $installed=Invoke-AIAssistantVsixInstaller -Installer $installer -InstanceId $selected.InstanceId -PackagePath $item.File.Path -ReportPath $ReportPath -TestRootSuffix $TestRootSuffix
            $result=[pscustomobject]@{InstanceId=$selected.InstanceId;Kind=$item.Kind;Package=$item.File.Path;Sha256=$item.File.Sha256;InstallerLog=$installed.InstallerLog;ExitCode=$installed.ExitCode;LogAvailable=$installed.LogAvailable;InstallerLogSource=$installed.InstallerLogSource;RequestedInstallerLog=$installed.RequestedInstallerLog;LogCopied=$installed.LogCopied;LogBytes=$installed.LogBytes;LogSha256=$installed.LogSha256;LogWarning=$installed.LogWarning}
            $report.InstallationResults+= $result
            $report|ConvertTo-Json -Depth 9|Set-Content -LiteralPath $ReportPath -Encoding UTF8
            if($installed.ExitCode -ne 0){throw "VSIXInstaller 종료 코드: $($installed.ExitCode). 진단 JSON과 확인된 설치 로그를 확인하세요."}
            $completedPackages++
            Write-Host ("INSTALL_PROGRESS {0}/{1}" -f $completedPackages,$totalPackages)
        }
    }
    Write-Host '확장 및 Visual Studio 테마 설치가 완료됐습니다. Visual Studio를 다시 열어 주세요.'
} catch { Write-Error $_; exit 1 }
