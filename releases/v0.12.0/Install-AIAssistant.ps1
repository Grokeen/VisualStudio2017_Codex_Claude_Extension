#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Install-AIAssistant
desc         : VSIX 설치 전에 IDE 연도·버전·에디션·아키텍처·무결성을 확인하고 해당 IDE의 설치기를 연다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-036 NoApplicableSKUsException 설치 진단·대상 선택 추가.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$PackageDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.12.0',
    [string]$InstanceId,
    [switch]$Install,
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
    $status = 'Ready'; $reason = '설치 조건 확인. 최종 의존성 검사는 VSIXInstaller에서 수행합니다.'; $package=$null
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
        }
    }
    return [pscustomobject]@{ InstanceId=$Instance.instanceId; Product=$Instance.productId; DisplayName=$Instance.displayName; InstallationVersion=$Instance.installationVersion; InstallationPath=$Instance.installationPath; Year=$year; Architecture=$Architecture; Status=$status; Reason=$reason; Package=$package }
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
    $report=[pscustomobject]@{ CheckedAt=[DateTimeOffset]::Now.ToString('o'); CheckKind='InstallationPreflight'; CustomerFailureCauseConfirmed=$false; PackageDirectory=$PackageDirectory; Plans=$plans }
    if (!$ReportPath) { $ReportPath=Join-Path ([IO.Path]::GetTempPath()) ('AI-Assistant-install-'+[guid]::NewGuid().ToString('N')+'.json') }
    if ([IO.Path]::GetExtension($ReportPath) -ne '.json' -or (Test-Path -LiteralPath $ReportPath)) { throw '새 .json 진단 파일 경로를 지정하세요. 기존 파일은 보존합니다.' }
    $report | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    $plans | Select-Object InstanceId,DisplayName,InstallationVersion,Architecture,Status,Reason | Format-Table -Wrap -AutoSize | Out-Host
    Write-Host "진단 파일: $ReportPath"
    if (!$plans.Count) { throw '설치된 Visual Studio를 찾지 못했습니다.' }
    if (!$Install) { return }
    $ready=@($plans | Where-Object Status -EQ 'Ready')
    if ($InstanceId) { $ready=@($ready | Where-Object InstanceId -CEQ $InstanceId) }
    if (!$ready.Count) { throw '설치 가능한 대상이 없습니다. 위 진단 내용을 확인하세요.' }
    if ($ready.Count -gt 1) {
        for($i=0;$i -lt $ready.Count;$i++) { Write-Host ("{0}. {1} ({2})" -f ($i+1),$ready[$i].DisplayName,$ready[$i].InstallationVersion) }
        $choice=Read-Host '설치할 Visual Studio 번호 (취소: Enter)'
        $index=0
        if (![int]::TryParse($choice,[ref]$index) -or $index -lt 1 -or $index -gt $ready.Count) { Write-Host '설치를 취소했습니다.'; return }
        $ready=@($ready[$index-1])
    }
    $selected=$ready[0]
    # 명령행은 검증한 instanceId와 로컬 경로만 사용한다. cmd.exe와 문자열 실행은 사용하지 않는다.
    if ($selected.InstanceId -notmatch '^[a-zA-Z0-9_-]+$') { throw '올바르지 않은 설치 인스턴스 ID입니다.' }
    if ((Get-FileHash -LiteralPath $selected.Package.Path).Hash -ne $selected.Package.Sha256) { throw '검사 후 패키지가 변경됐습니다.' }
    $installer=Join-Path $selected.InstallationPath 'Common7/IDE/VSIXInstaller.exe'
    if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { throw '선택한 IDE의 VSIXInstaller.exe가 없습니다. Visual Studio를 복구하세요.' }
    $log=Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ReportPath))) ('VSIXInstaller-'+[guid]::NewGuid().ToString('N')+'.log')
    $arguments='/instanceIds:'+$selected.InstanceId+' /logFile:"'+$log+'" "'+$selected.Package.Path+'"'
    if ($TestRootSuffix) { $arguments='/quiet /rootSuffix:'+$TestRootSuffix+' '+$arguments }
    Write-Host "설치 대상: $($selected.DisplayName) / $($selected.InstallationVersion)"
    Write-Host "설치 로그: $log"
    if ($TestRootSuffix) { $process=Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru -WindowStyle Hidden }
    else { $process=Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru }
    if ($process.ExitCode -ne 0) { throw "VSIXInstaller 종료 코드: $($process.ExitCode). 진단 JSON과 설치 로그를 확인하세요." }
} catch { Write-Error $_; exit 1 }
