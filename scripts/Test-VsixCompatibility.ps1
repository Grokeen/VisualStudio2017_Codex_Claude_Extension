#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-VsixCompatibility
desc         : VSIX 원본 매니페스트와 프로젝트의 정적 호환성을 검사한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 버전·SDK 검사 및 Windows PowerShell 5.1 한글 인코딩 지원.
-------------------------------------------------------------------------
.SYNOPSIS
Checks source declarations for the project's VS2017/2019/2022 compatibility policy.
.DESCRIPTION
Does not evaluate MSBuild imports/conditions, compile a VSIX, or prove IDE runtime
compatibility. All literal property/package declarations are checked conservatively.
Use separate projects for the legacy and VS2022 executable VSIX payloads.
.PARAMETER PassThru
Returns a report object instead of printing a summary and setting a process exit code.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ManifestPath,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [ValidateSet('2017', '2019', '2022')][string[]]$RequiredVersions = @('2017'),
    [ValidateSet('amd64', 'arm64')][string]$Vs2022Architecture = 'amd64',
    [string]$JsonReportPath,
    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

<#
-------------------------------------------------------------------------
name         : Read-CompatibilityXml
desc         : 외부 엔터티와 DTD를 금지하고 검증 대상 XML을 읽는다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 안전한 XML 로딩과 자원 해제 구현.
-------------------------------------------------------------------------
#>
function Read-CompatibilityXml {
    param([string]$Path)
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create([System.IO.Path]::GetFullPath($Path), $settings)
    try {
        $document = New-Object System.Xml.XmlDocument
        $document.XmlResolver = $null
        $document.Load($reader)
        return ,$document
    }
    finally { $reader.Dispose() }
}

<#
-------------------------------------------------------------------------
name         : ConvertTo-CompatibilityVersion
desc         : 자리 수가 다른 버전을 네 자리로 정규화하여 경계 비교한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 숫자 버전만 허용하고 누락 자리는 0 처리.
-------------------------------------------------------------------------
#>
function ConvertTo-CompatibilityVersion {
    param([string]$Value)
    if ($Value -notmatch '^\d+(\.\d+){1,3}$') { throw "Unsupported numeric version: '$Value'." }
    $parsed = [version]$Value
    return New-Object System.Version($parsed.Major, $parsed.Minor, [Math]::Max(0, $parsed.Build), [Math]::Max(0, $parsed.Revision))
}

<#
-------------------------------------------------------------------------
name         : ConvertFrom-CompatibilityRange
desc         : VSIX 버전 구간과 정확한 버전, 최소 버전 선언을 파싱한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 열린 경계 및 잘못된 빈 구간 검증.
-------------------------------------------------------------------------
#>
function ConvertFrom-CompatibilityRange {
    param([string]$Value)
    $valueText = $Value.Trim()
    $minimum = $null
    $maximum = $null
    $includeMinimum = $true
    $includeMaximum = $false
    if ($valueText -match '^\[(\d+(?:\.\d+){1,3})\]$') {
        $minimum = ConvertTo-CompatibilityVersion $Matches[1]
        $maximum = $minimum
        $includeMaximum = $true
    }
    elseif ($valueText -match '^(\[|\()\s*([^,]*)\s*,\s*([^,]*)\s*(\]|\))$') {
        $lower = $Matches[2].Trim()
        $upper = $Matches[3].Trim()
        $includeMinimum = $Matches[1] -eq '['
        $includeMaximum = $Matches[4] -eq ']'
        if ($lower) { $minimum = ConvertTo-CompatibilityVersion $lower }
        if ($upper) { $maximum = ConvertTo-CompatibilityVersion $upper }
        if (-not $lower -and -not $upper) { throw "Empty version range: '$Value'." }
        if ((-not $lower -and $includeMinimum) -or (-not $upper -and $includeMaximum)) {
            throw "An unbounded version endpoint must be open: '$Value'."
        }
    }
    else { $minimum = ConvertTo-CompatibilityVersion $valueText }
    if ($null -ne $minimum -and $null -ne $maximum) {
        if ($minimum -gt $maximum -or ($minimum -eq $maximum -and -not ($includeMinimum -and $includeMaximum))) {
            throw "Reversed or empty version range: '$Value'."
        }
    }
    return [pscustomobject]@{
        Minimum = $minimum; Maximum = $maximum
        IncludeMinimum = $includeMinimum; IncludeMaximum = $includeMaximum
    }
}

<#
-------------------------------------------------------------------------
name         : Test-CompatibilityRangeOverlap
desc         : 버전 구간이 해당 IDE 주 버전과 겹치는지 판정한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 15.9 등 하위 버전 한정 지원도 인식.
-------------------------------------------------------------------------
#>
function Test-CompatibilityRangeOverlap {
    param($Range, [int]$Major)
    $start = ConvertTo-CompatibilityVersion "$Major.0"
    $end = ConvertTo-CompatibilityVersion "$($Major + 1).0"
    if ($null -ne $Range.Minimum -and $Range.Minimum -ge $end) { return $false }
    if ($null -ne $Range.Maximum) {
        if ($Range.Maximum -lt $start) { return $false }
        if ($Range.Maximum -eq $start -and -not $Range.IncludeMaximum) { return $false }
    }
    return $true
}

$failures = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'
$matrix = @()
$packages = @()
$frameworks = @()
$platforms = @()
$manifestFullPath = [System.IO.Path]::GetFullPath($ManifestPath)
$projectFullPath = [System.IO.Path]::GetFullPath($ProjectPath)

try {
    $manifest = Read-CompatibilityXml $manifestFullPath
    $manifestNamespace = 'http://schemas.microsoft.com/developer/vsx-schema/2011'
    if ($manifest.DocumentElement.LocalName -ne 'PackageManifest' -or
        $manifest.DocumentElement.NamespaceURI -ne $manifestNamespace -or
        $manifest.DocumentElement.GetAttribute('Version') -notmatch '^2\.0(?:\.0)?$') {
        throw 'Expected a VSIX 2.0 PackageManifest with the official 2011 namespace.'
    }
    $ns = New-Object System.Xml.XmlNamespaceManager($manifest.NameTable)
    $ns.AddNamespace('v', $manifestNamespace)
    $assets = @($manifest.SelectNodes('/v:PackageManifest/v:Assets/v:Asset', $ns) |
        Where-Object { $_.GetAttribute('Type') -eq 'Microsoft.VisualStudio.VsPackage' })
    if ($assets.Count -eq 0) { $failures.Add('A WPF package extension must declare a Microsoft.VisualStudio.VsPackage asset.') }
    $targets = @($manifest.SelectNodes('/v:PackageManifest/v:Installation/v:InstallationTarget', $ns))
    if ($targets.Count -eq 0) { throw 'No InstallationTarget was declared.' }
    $targetData = @()
    foreach ($target in $targets) {
        $architectureNodes = @($target.SelectNodes('v:ProductArchitecture', $ns))
        if ($architectureNodes.Count -gt 1) { $failures.Add('Each InstallationTarget must have at most one ProductArchitecture.') }
        $architecture = if ($architectureNodes.Count -gt 0) { $architectureNodes[0].InnerText.Trim().ToLowerInvariant() } else { '' }
        $targetData += [pscustomobject]@{
            Id = $target.GetAttribute('Id'); Version = $target.GetAttribute('Version')
            Range = ConvertFrom-CompatibilityRange $target.GetAttribute('Version')
            Architecture = $architecture
        }
    }
    foreach ($entry in @(@('2017', 15), @('2019', 16), @('2022', 17))) {
        $year = [string]$entry[0]
        $major = [int]$entry[1]
        $architecture = if ($year -eq '2022') { $Vs2022Architecture } else { 'x86' }
        $matching = @($targetData | Where-Object {
            $_.Id -in @('Microsoft.VisualStudio.Community', 'Microsoft.VisualStudio.Pro', 'Microsoft.VisualStudio.Enterprise') -and
            (Test-CompatibilityRangeOverlap $_.Range $major)
        })
        $usable = @($matching | Where-Object {
            $_.Architecture -eq $architecture -or ($year -ne '2022' -and $_.Architecture -eq '')
        })
        $required = $RequiredVersions -contains $year
        if ($required -and $matching.Count -eq 0) { $failures.Add("VS$year has no applicable InstallationTarget.") }
        # 지원을 선언한 IDE에는 올바른 아키텍처가 있어야 한다. 미선언 대상은 필수 요청일 때만 실패한다.
        foreach ($match in $matching) {
            $allowed = if ($year -eq '2022') { @('amd64', 'arm64') } else { @('', 'x86') }
            if ($match.Architecture -notin $allowed) { $failures.Add("VS$year has invalid architecture '$($match.Architecture)' for range $($match.Version).") }
        }
        if ($required -and $matching.Count -gt 0 -and $usable.Count -eq 0) { $failures.Add("VS$year has no target for requested architecture '$architecture'.") }
        $matrix += [pscustomobject]@{
            VisualStudio = $year; Required = $required; Declared = ($matching.Count -gt 0)
            RequestedArchitecture = $architecture
            MatchingRanges = @($matching | ForEach-Object { $_.Version })
            ManifestStatus = if ($usable.Count -gt 0) { 'Declared' } elseif ($matching.Count -gt 0) { 'ArchitectureMismatch' } else { 'NotDeclared' }
        }
    }

    $project = Read-CompatibilityXml $projectFullPath
    if ($project.DocumentElement.LocalName -ne 'Project' -or $project.DocumentElement.NamespaceURI -notin @('', 'http://schemas.microsoft.com/developer/msbuild/2003')) {
        throw 'Expected a C# MSBuild Project XML document.'
    }
    $frameworks = @($project.SelectNodes('/*/*[local-name()="PropertyGroup"]/*[local-name()="TargetFrameworkVersion" or local-name()="TargetFramework"]') |
        ForEach-Object { $_.InnerText.Trim() } | Sort-Object -Unique)
    if ($frameworks.Count -eq 0) { $failures.Add('No literal TargetFrameworkVersion/TargetFramework found; imported properties require an MSBuild review.') }
    foreach ($framework in $frameworks) {
        if ($framework -notmatch '^(v4\.\d+(?:\.\d+)?|net4\d{1,2})$') { $failures.Add("Expected a literal .NET Framework 4.x target, found '$framework'.") }
    }
    $platforms = @($project.SelectNodes('/*/*[local-name()="PropertyGroup"]/*[local-name()="PlatformTarget"]') |
        ForEach-Object { $_.InnerText.Trim().Replace(' ', '').ToLowerInvariant() } | Sort-Object -Unique)
    if ($platforms.Count -eq 0) { $warnings.Add('No explicit PlatformTarget found. Verify the effective compiler target with MSBuild; imports and defaults are not evaluated.') }

    $packageNodes = @($project.SelectNodes('/*/*[local-name()="ItemGroup"]/*[local-name()="PackageReference"]'))
    foreach ($node in $packageNodes) {
        $versionText = $node.GetAttribute('Version')
        $versionNode = $node.SelectSingleNode('*[local-name()="Version"]')
        if (-not $versionText -and $null -ne $versionNode) { $versionText = $versionNode.InnerText }
        $packages += [pscustomobject]@{ Id = $node.GetAttribute('Include'); Version = $versionText.Trim() }
    }
    $packagesConfigPath = Join-Path ([System.IO.Path]::GetDirectoryName($projectFullPath)) 'packages.config'
    if (Test-Path -LiteralPath $packagesConfigPath -PathType Leaf) {
        $packagesConfig = Read-CompatibilityXml $packagesConfigPath
        foreach ($node in $packagesConfig.SelectNodes('/packages/package')) {
            $packages += [pscustomobject]@{ Id = $node.GetAttribute('id'); Version = $node.GetAttribute('version') }
        }
    }
    $sdkPackages = @($packages | Where-Object { $_.Id -match '^Microsoft\.VisualStudio\.(Sdk|Shell(?:\.[0-9.]+)?)$' })
    $buildPackages = @($packages | Where-Object { $_.Id -eq 'Microsoft.VSSDK.BuildTools' })
    if ($sdkPackages.Count -eq 0) { $failures.Add('No explicit Microsoft.VisualStudio.Sdk or Shell package version found; direct assembly/import references require manual review.') }
    if ($buildPackages.Count -eq 0) { $warnings.Add('No explicit Microsoft.VSSDK.BuildTools package found. Verify the installed VSSDK build targets manually.') }
    $legacyDeclared = @($matrix | Where-Object { $_.VisualStudio -in @('2017', '2019') -and $_.Declared }).Count -gt 0
    $modernDeclared = @($matrix | Where-Object { $_.VisualStudio -eq '2022' -and $_.Declared }).Count -gt 0
    if ($legacyDeclared -and $modernDeclared) { $failures.Add('An executable VSIX needs separate legacy and VS2022 projects/reference sets; a widened manifest is insufficient.') }
    foreach ($row in $matrix | Where-Object { $_.Declared }) {
        $allowedPlatforms = if ($row.VisualStudio -eq '2022') {
            if ($Vs2022Architecture -eq 'arm64') { @('anycpu', 'arm64') } else { @('anycpu', 'x64') }
        } else { @('anycpu', 'x86') }
        foreach ($platform in $platforms) {
            if ($platform -notin $allowedPlatforms) { $failures.Add("VS$($row.VisualStudio) cannot use declared PlatformTarget '$platform' under this policy.") }
        }
        $maxSdkMajor = switch ($row.VisualStudio) { '2017' { 15 } '2019' { 16 } '2022' { 17 } }
        foreach ($package in @($sdkPackages + $buildPackages)) {
            # 변수·범위·부동 버전은 MSBuild 평가 없이 통과시키지 않는다.
            if ($package.Version -notmatch '^([0-9]+)\.[0-9]+(?:\.[0-9]+){0,2}(?:-[0-9A-Za-z.-]+)?$') {
                $failures.Add("Package $($package.Id) has no statically resolved pinned version: '$($package.Version)'.")
                continue
            }
            $packageMajor = [int]$Matches[1]
            if ($packageMajor -gt $maxSdkMajor -or $packageMajor -lt 15 -or ($row.VisualStudio -eq '2022' -and $packageMajor -ne 17)) {
                $failures.Add("VS$($row.VisualStudio) conflicts with $($package.Id) $($package.Version) under the project's SDK policy.")
            }
        }
    }
    if (@($matrix | Where-Object { $_.VisualStudio -eq '2017' -and $_.Declared }).Count -gt 0) {
        $languages = @($project.SelectNodes('/*/*[local-name()="PropertyGroup"]/*[local-name()="LangVersion"]') | ForEach-Object { $_.InnerText.Trim() })
        if ($languages.Count -eq 0) { $warnings.Add('No explicit LangVersion found; verify the VS2017 compiler version (C# 7.3 maximum).') }
        foreach ($language in $languages) {
            if ($language -notmatch '^\d+(?:\.\d+)?$' -or [double]::Parse($language, [Globalization.CultureInfo]::InvariantCulture) -gt 7.3) {
                $failures.Add("VS2017 requires an explicit C# version at most 7.3, found '$language'.")
            }
        }
    }
    $warnings.Add('Static source checks only: MSBuild imports/conditions, dependencies, VSIX contents, and IDE loading still require build and installation tests.')
}
catch { $failures.Add($_.Exception.Message) }

$report = [pscustomobject]@{
    SchemaVersion = 1
    CheckKind = 'StaticSourceCompatibility'
    Succeeded = ($failures.Count -eq 0)
    ManifestPath = $manifestFullPath; ProjectPath = $projectFullPath
    RequiredVersions = @($RequiredVersions); Matrix = @($matrix)
    Frameworks = @($frameworks); PlatformTargets = @($platforms); Packages = @($packages)
    Failures = @($failures.ToArray() | Select-Object -Unique)
    Warnings = @($warnings.ToArray() | Select-Object -Unique)
}
if ($JsonReportPath) {
    try {
        $reportFullPath = [System.IO.Path]::GetFullPath($JsonReportPath)
        if ([System.IO.Path]::GetExtension($reportFullPath) -ne '.json' -or
            $reportFullPath -in @($manifestFullPath, $projectFullPath, (Join-Path ([System.IO.Path]::GetDirectoryName($projectFullPath)) 'packages.config'))) {
            throw 'The report must be a separate .json file.'
        }
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportFullPath -Encoding UTF8
    }
    catch {
        $report.Succeeded = $false
        $report.Failures = @($report.Failures) + "Could not write JSON report: $($_.Exception.Message)"
    }
}
if ($PassThru) { return $report }
$report.Matrix | Format-Table VisualStudio, Required, Declared, RequestedArchitecture, ManifestStatus -AutoSize | Out-Host
foreach ($message in $report.Warnings) { Write-Host "WARNING: $message" }
foreach ($message in $report.Failures) { Write-Host "FAIL: $message" }
if ($report.Succeeded) { Write-Host 'PASS: Static source declarations satisfy the selected policy. IDE tests remain required.'; exit 0 }
exit 1
