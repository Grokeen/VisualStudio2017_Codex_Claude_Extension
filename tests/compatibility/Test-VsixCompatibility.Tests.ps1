#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-VsixCompatibility.Tests
desc         : VSIX 선언의 잘못된 호환성 판정과 CLI 실패 코드를 회귀 검증한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 임시 fixture 회귀 테스트 및 PowerShell 5.1 한글 인코딩 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$checkerPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\scripts\Test-VsixCompatibility.ps1'))
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('CodexOrig VSIX 검증 ' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testRoot
$script:testCount = 0
$script:testFailures = New-Object 'System.Collections.Generic.List[string]'

<#
-------------------------------------------------------------------------
name         : New-CompatibilityFixture
desc         : 실제 저장소 파일을 수정하지 않고 독립 입력 파일을 생성한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 매니페스트·프로젝트·패키지 입력 생성.
-------------------------------------------------------------------------
#>
function New-CompatibilityFixture {
    param(
        [string]$Range = '[15.0,16.0)', [string]$Architecture = '',
        [string]$SdkVersion = '15.0.1', [string]$BuildVersion = '15.0.1',
        [string]$Platform = 'AnyCPU', [string]$Framework = 'v4.6.2', [string]$Language = '7.3'
    )
    $manifestPath = Join-Path $testRoot 'source.extension.vsixmanifest'
    $projectPath = Join-Path $testRoot 'Extension.csproj'
    $configPath = Join-Path $testRoot 'packages.config'
    if (Test-Path -LiteralPath $configPath) { Remove-Item -LiteralPath $configPath }
    $architectureXml = if ($Architecture) { '<ProductArchitecture>' + $Architecture + '</ProductArchitecture>' } else { '' }
    @"
<PackageManifest Version="2.0.0" xmlns="http://schemas.microsoft.com/developer/vsx-schema/2011">
  <Metadata><Identity Id="Fixture.Extension" Version="0.1.0" Publisher="CodexOrig" Language="en-US" /></Metadata>
  <Installation><InstallationTarget Id="Microsoft.VisualStudio.Community" Version="$Range">$architectureXml</InstallationTarget></Installation>
  <Assets><Asset Type="Microsoft.VisualStudio.VsPackage" Path="Extension.pkgdef" /></Assets>
</PackageManifest>
"@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    @"
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup><TargetFrameworkVersion>$Framework</TargetFrameworkVersion><LangVersion>$Language</LangVersion><PlatformTarget>$Platform</PlatformTarget></PropertyGroup>
  <ItemGroup><PackageReference Include="Microsoft.VisualStudio.Sdk" Version="$SdkVersion" /><PackageReference Include="Microsoft.VSSDK.BuildTools" Version="$BuildVersion" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $projectPath -Encoding UTF8
    return @{ ManifestPath = $manifestPath; ProjectPath = $projectPath }
}

<#
-------------------------------------------------------------------------
name         : Assert-CompatibilityResult
desc         : 성공 여부와 실패 원인을 확인하여 오탐 통과를 방지한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 예상 실패의 구체적인 메시지 검증.
-------------------------------------------------------------------------
#>
function Assert-CompatibilityResult {
    param($Report, [bool]$Expected, [string]$FailureText = '')
    if ($Report.Succeeded -ne $Expected) { throw "Expected Succeeded=$Expected; failures: $($Report.Failures -join ' | ')" }
    if ($FailureText -and ($Report.Failures -join ' | ') -notlike "*$FailureText*") {
        throw "Expected failure containing '$FailureText'; got $($Report.Failures -join ' | ')"
    }
}

<#
-------------------------------------------------------------------------
name         : Invoke-CompatibilityCase
desc         : 하나의 시나리오를 실행하고 실패해도 다른 시나리오를 검증한다.
author       : CodexOrig
create date  : 2026-10-17
update date  : 2026-10-17 CodexOrig : 독립 케이스 집계와 실패 목록 출력.
-------------------------------------------------------------------------
#>
function Invoke-CompatibilityCase {
    param([string]$Name, [scriptblock]$Body)
    $script:testCount++
    try { & $Body; Write-Host "PASS $Name" }
    catch { $script:testFailures.Add("$Name : $($_.Exception.Message)"); Write-Host "FAIL $Name : $($_.Exception.Message)" }
}

try {
    Invoke-CompatibilityCase 'VS2017 baseline and Unicode paths' {
        $fixture = New-CompatibilityFixture
        $report = & $checkerPath @fixture -PassThru
        Assert-CompatibilityResult $report $true
        if ($report.Matrix[1].Declared -or $report.Matrix[2].Declared) { throw 'Undeclared future versions were reported as supported.' }
    }
    Invoke-CompatibilityCase 'Required missing VS2019 and VS2022 fail' {
        $fixture = New-CompatibilityFixture
        $report = & $checkerPath @fixture -RequiredVersions @('2017', '2019', '2022') -PassThru
        Assert-CompatibilityResult $report $false 'VS2019 has no applicable'
        if (($report.Failures -join ' ') -notlike '*VS2022 has no applicable*') { throw 'Missing VS2022 was not reported.' }
    }
    Invoke-CompatibilityCase 'Legacy SDK15 supports declared VS2017 and VS2019' {
        $fixture = New-CompatibilityFixture -Range '[15.0,17.0)'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2017', '2019') -PassThru) $true
    }
    Invoke-CompatibilityCase 'VS2017 minor-version-only range is detected' {
        $fixture = New-CompatibilityFixture -Range '[15.9,16.0)'
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $true
    }
    Invoke-CompatibilityCase 'Inclusive upper bound includes the next major' {
        $fixture = New-CompatibilityFixture -Range '[15.0,16.0]'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2017', '2019') -PassThru) $true
    }
    Invoke-CompatibilityCase 'Exact VS2019 target and SDK16' {
        $fixture = New-CompatibilityFixture -Range '[16.0]' -SdkVersion '16.0.1' -BuildVersion '16.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2019') -PassThru) $true
    }
    Invoke-CompatibilityCase 'Open lower bound excludes upper major' {
        $fixture = New-CompatibilityFixture -Range '(,16.0)'
        $report = & $checkerPath @fixture -PassThru
        Assert-CompatibilityResult $report $true
        if ($report.Matrix[1].Declared) { throw 'Exclusive upper bound included VS2019.' }
    }
    Invoke-CompatibilityCase 'Four-part exclusive endpoint stays exclusive' {
        $fixture = New-CompatibilityFixture -Range '[15.0.0.0,16.0.0.0)'
        $report = & $checkerPath @fixture -PassThru
        Assert-CompatibilityResult $report $true
        if ($report.Matrix[1].Declared) { throw 'Version normalization changed the exclusive endpoint.' }
    }
    foreach ($badRange in @('[16.0,15.0)', '[15.0,15.0)', '[,16.0)', '(,)', 'not-a-range')) {
        Invoke-CompatibilityCase "Malformed/empty range: $badRange" {
            $fixture = New-CompatibilityFixture -Range $badRange
            Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false
        }
    }
    foreach ($openRange in @('15.0', '[15.0,)')) {
        Invoke-CompatibilityCase "Open upper range cannot silently claim VS2022: $openRange" {
            $fixture = New-CompatibilityFixture -Range $openRange
            Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'separate legacy and VS2022'
        }
    }
    Invoke-CompatibilityCase 'Wrong manifest namespace' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ManifestPath -Raw).Replace('http://schemas.microsoft.com/developer/vsx-schema/2011', 'urn:wrong') | Set-Content -LiteralPath $fixture.ManifestPath -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'official 2011 namespace'
    }
    Invoke-CompatibilityCase 'Missing manifest is a failed result' {
        $fixture = New-CompatibilityFixture
        $fixture.ManifestPath = Join-Path $testRoot 'missing.vsixmanifest'
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false
    }
    Invoke-CompatibilityCase 'Malformed XML fails' {
        $fixture = New-CompatibilityFixture
        '<PackageManifest>' | Set-Content -LiteralPath $fixture.ManifestPath -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false
    }
    Invoke-CompatibilityCase 'DTD and external entities are prohibited' {
        $fixture = New-CompatibilityFixture
        '<!DOCTYPE PackageManifest [<!ENTITY unsafe SYSTEM "file:///not-a-real-file">]><PackageManifest>&unsafe;</PackageManifest>' | Set-Content -LiteralPath $fixture.ManifestPath -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'DTD'
    }
    Invoke-CompatibilityCase 'Missing executable package asset fails' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ManifestPath -Raw).Replace('Microsoft.VisualStudio.VsPackage', 'Microsoft.VisualStudio.ItemTemplate') | Set-Content -LiteralPath $fixture.ManifestPath -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'VsPackage asset'
    }
    Invoke-CompatibilityCase 'VS2017 rejects newer SDK' {
        $fixture = New-CompatibilityFixture -SdkVersion '16.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'VS2017 conflicts with Microsoft.VisualStudio.Sdk'
    }
    Invoke-CompatibilityCase 'VS2017 build baseline rejects newer BuildTools' {
        $fixture = New-CompatibilityFixture -BuildVersion '17.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'VS2017 conflicts with Microsoft.VSSDK.BuildTools'
    }
    Invoke-CompatibilityCase 'Dedicated VS2022 amd64 project passes' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -Architecture 'amd64' -SdkVersion '17.0.1' -BuildVersion '17.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -PassThru) $true
    }
    Invoke-CompatibilityCase 'VS2022 requires ProductArchitecture' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -SdkVersion '17.0.1' -BuildVersion '17.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -PassThru) $false 'invalid architecture'
    }
    Invoke-CompatibilityCase 'VS2022 rejects x86 manifest target' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -Architecture 'x86' -SdkVersion '17.0.1' -BuildVersion '17.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -PassThru) $false 'invalid architecture'
    }
    Invoke-CompatibilityCase 'VS2022 rejects x86 payload' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -Architecture 'amd64' -SdkVersion '17.0.1' -BuildVersion '17.0.1' -Platform 'x86'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -PassThru) $false "PlatformTarget 'x86'"
    }
    Invoke-CompatibilityCase 'Missing PlatformTarget is an explicit manual-review warning' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ProjectPath -Raw).Replace('<PlatformTarget>AnyCPU</PlatformTarget>', '') | Set-Content -LiteralPath $fixture.ProjectPath -Encoding UTF8
        $report = & $checkerPath @fixture -PassThru
        Assert-CompatibilityResult $report $true
        if (($report.Warnings -join ' ') -notlike '*effective compiler target with MSBuild*') { throw 'Implicit compiler architecture was not flagged for review.' }
    }
    Invoke-CompatibilityCase 'VS2022 ARM64 can be requested explicitly' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -Architecture 'arm64' -SdkVersion '17.0.1' -BuildVersion '17.0.1' -Platform 'ARM64'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -Vs2022Architecture arm64 -PassThru) $true
    }
    Invoke-CompatibilityCase 'ARM64-only manifest cannot satisfy amd64 request' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -Architecture 'arm64' -SdkVersion '17.0.1' -BuildVersion '17.0.1'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -PassThru) $false "requested architecture 'amd64'"
    }
    Invoke-CompatibilityCase 'VS2022 rejects legacy SDK' {
        $fixture = New-CompatibilityFixture -Range '[17.0,18.0)' -Architecture 'amd64'
        Assert-CompatibilityResult (& $checkerPath @fixture -RequiredVersions @('2022') -PassThru) $false 'VS2022 conflicts with'
    }
    Invoke-CompatibilityCase 'Modern .NET target cannot masquerade as Framework' {
        $fixture = New-CompatibilityFixture -Framework 'net8.0-windows'
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false '.NET Framework 4.x'
    }
    foreach ($language in @('8.0', 'latest', 'preview')) {
        Invoke-CompatibilityCase "VS2017 rejects unbuildable/unpinned language: $language" {
            $fixture = New-CompatibilityFixture -Language $language
            Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'C# version at most 7.3'
        }
    }
    foreach ($sdkVersion in @('15.*', '$(SdkVersion)', '[15.0,16.0)')) {
        Invoke-CompatibilityCase "Unresolved SDK version fails: $sdkVersion" {
            $fixture = New-CompatibilityFixture -SdkVersion $sdkVersion
            Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'statically resolved pinned version'
        }
    }
    Invoke-CompatibilityCase 'PackageReference Version child element is supported' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ProjectPath -Raw).Replace('<PackageReference Include="Microsoft.VisualStudio.Sdk" Version="15.0.1" />', '<PackageReference Include="Microsoft.VisualStudio.Sdk"><Version>15.0.1</Version></PackageReference>') | Set-Content -LiteralPath $fixture.ProjectPath -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $true
    }
    Invoke-CompatibilityCase 'Legacy packages.config is supported' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ProjectPath -Raw) -replace '<ItemGroup>.*</ItemGroup>', '' | Set-Content -LiteralPath $fixture.ProjectPath -Encoding UTF8
        '<packages><package id="Microsoft.VisualStudio.Shell.15.0" version="15.0.1" /><package id="Microsoft.VSSDK.BuildTools" version="15.0.1" /></packages>' | Set-Content -LiteralPath (Join-Path $testRoot 'packages.config') -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $true
    }
    Invoke-CompatibilityCase 'Direct assembly-only SDK is not falsely verified' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ProjectPath -Raw).Replace('Microsoft.VisualStudio.Sdk', 'Unrelated.Package') | Set-Content -LiteralPath $fixture.ProjectPath -Encoding UTF8
        Assert-CompatibilityResult (& $checkerPath @fixture -PassThru) $false 'No explicit Microsoft.VisualStudio.Sdk or Shell'
    }
    Invoke-CompatibilityCase 'No BuildTools package produces a manual-review warning' {
        $fixture = New-CompatibilityFixture
        (Get-Content -LiteralPath $fixture.ProjectPath -Raw).Replace('Microsoft.VSSDK.BuildTools', 'Unrelated.Package') | Set-Content -LiteralPath $fixture.ProjectPath -Encoding UTF8
        $report = & $checkerPath @fixture -PassThru
        Assert-CompatibilityResult $report $true
        if (($report.Warnings -join ' ') -notlike '*VSSDK build targets manually*') { throw 'Missing manual-review warning.' }
    }
    Invoke-CompatibilityCase 'JSON report round trip and read-only inputs' {
        $fixture = New-CompatibilityFixture
        $manifestBefore = [System.IO.File]::ReadAllBytes($fixture.ManifestPath)
        $projectBefore = [System.IO.File]::ReadAllBytes($fixture.ProjectPath)
        $reportPath = Join-Path $testRoot 'result.json'
        $report = & $checkerPath @fixture -JsonReportPath $reportPath -PassThru
        Assert-CompatibilityResult $report $true
        $saved = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
        if (-not $saved.Succeeded -or $saved.Matrix.Count -ne 3 -or $saved.CheckKind -ne 'StaticSourceCompatibility') { throw 'Saved JSON report differs.' }
        if ([Convert]::ToBase64String($manifestBefore) -ne [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($fixture.ManifestPath)) -or
            [Convert]::ToBase64String($projectBefore) -ne [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($fixture.ProjectPath))) { throw 'Checker modified source inputs.' }
    }
    Invoke-CompatibilityCase 'Report cannot overwrite source manifest' {
        $fixture = New-CompatibilityFixture
        $before = Get-Content -LiteralPath $fixture.ManifestPath -Raw
        Assert-CompatibilityResult (& $checkerPath @fixture -JsonReportPath $fixture.ManifestPath -PassThru) $false 'separate .json'
        if ((Get-Content -LiteralPath $fixture.ManifestPath -Raw) -ne $before) { throw 'Source was overwritten.' }
    }
    Invoke-CompatibilityCase 'CLI returns zero on pass and one on failed checks' {
        $fixture = New-CompatibilityFixture
        $engine = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
        $output = & $engine -NoProfile -NonInteractive -File $checkerPath -ManifestPath $fixture.ManifestPath -ProjectPath $fixture.ProjectPath 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Passing CLI exited $LASTEXITCODE : $output" }
        $fixture = New-CompatibilityFixture -SdkVersion '17.0.1'
        $output = & $engine -NoProfile -NonInteractive -File $checkerPath -ManifestPath $fixture.ManifestPath -ProjectPath $fixture.ProjectPath 2>&1
        if ($LASTEXITCODE -ne 1) { throw "Failing CLI exited $LASTEXITCODE : $output" }
    }
}
finally {
    # 재귀 삭제는 이 테스트가 만든 고유한 임시 디렉터리에만 허용한다.
    $resolvedRoot = [System.IO.Path]::GetFullPath($testRoot)
    $tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ([System.IO.Path]::GetDirectoryName($resolvedRoot) -ne $tempParent -or
        [System.IO.Path]::GetFileName($resolvedRoot) -notmatch '^CodexOrig VSIX 검증 [a-f0-9]{32}$') { throw 'Refusing to clean an unexpected path.' }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}

Write-Host "$($script:testCount - $script:testFailures.Count)/$script:testCount passed."
if ($script:testFailures.Count -gt 0) { $script:testFailures | ForEach-Object { Write-Host $_ }; exit 1 }
exit 0
