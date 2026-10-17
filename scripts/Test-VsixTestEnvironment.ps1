#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-VsixTestEnvironment
desc         : IDE/SDK 설치 상태와 버전별 .NET 4.6.2 WPF 컴파일을 확인한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : PS5 배열 정규화, WPF 빌드 및 IDE용 솔루션 probe 생성.
-------------------------------------------------------------------------
.SYNOPSIS
Checks all three installed IDEs and optionally compiles a separate WPF probe per IDE.
#>
[CmdletBinding()]
param(
    [ValidateSet('2017', '2019', '2022')][string[]]$Versions = @('2017', '2019', '2022'),
    [string]$OutputDirectory = (Join-Path $env:LOCALAPPDATA 'CodexOrig\VSIX-TestEnvironment\logs'),
    [switch]$BuildSmokeTest,
    [switch]$PassThru
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$components = @('Microsoft.VisualStudio.Workload.ManagedDesktop', 'Microsoft.VisualStudio.Workload.VisualStudioExtension', 'Microsoft.VisualStudio.Component.VSSDK', 'Microsoft.Net.Component.4.6.2.TargetingPack', 'Microsoft.Net.Component.4.6.2.SDK')
$vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$frameworkReference = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2\mscorlib.dll'
$instances = @()
$problems = New-Object 'System.Collections.Generic.List[string]'
if (Test-Path -LiteralPath $vswherePath) {
    $inventory = & $vswherePath -all -products '*' -format json -utf8
    if ($LASTEXITCODE -ne 0) { $problems.Add("vswhere failed: $LASTEXITCODE") }
    else { $parsedInstances = $inventory | ConvertFrom-Json; $instances = @($parsedInstances) }
}
else { $problems.Add('Visual Studio Installer/vswhere is not installed.') }
$rows = @()
foreach ($year in $Versions | Select-Object -Unique) {
    $major = switch ($year) { '2017' { 15 } '2019' { 16 } '2022' { 17 } }
    $matching = @($instances | Where-Object { ([version]$_.installationVersion).Major -eq $major })
    if ($matching.Count -eq 0) { $problems.Add("VS$year is not installed."); $rows += [pscustomobject]@{ Year = $year; Installed = $false }; continue }
    foreach ($instance in $matching) {
        $installPath = $instance.installationPath
        $devenvPath = Join-Path $installPath 'Common7\IDE\devenv.exe'
        $vsixInstallerPath = Join-Path $installPath 'Common7\IDE\VSIXInstaller.exe'
        $msbuildRelative = if ($major -eq 15) { 'MSBuild\15.0\Bin\MSBuild.exe' } else { 'MSBuild\Current\Bin\MSBuild.exe' }
        $msbuildPath = Join-Path $installPath $msbuildRelative
        $configuredPaths = @(& $vswherePath -all -products '*' -version "[$major.0,$($major + 1).0)" -requires $components -property installationPath)
        $componentsPresent = $configuredPaths -contains $installPath
        $complete = $null -ne $instance.PSObject.Properties['isComplete'] -and $instance.isComplete
        $launchable = $null -ne $instance.PSObject.Properties['isLaunchable'] -and $instance.isLaunchable
        $row = [pscustomobject]@{
            Year = $year; Installed = $true; Version = $instance.installationVersion; Path = $installPath
            Complete = $complete; Launchable = $launchable; RequiredComponentsPresent = $componentsPresent
            DevenvPresent = (Test-Path -LiteralPath $devenvPath -PathType Leaf)
            VsixInstallerPresent = (Test-Path -LiteralPath $vsixInstallerPath -PathType Leaf)
            MSBuildPresent = (Test-Path -LiteralPath $msbuildPath -PathType Leaf)
            Framework462Present = (Test-Path -LiteralPath $frameworkReference -PathType Leaf)
            SmokeBuild = 'NotRun'; SmokeLog = ''
        }
        foreach ($check in @('Complete', 'Launchable', 'RequiredComponentsPresent', 'DevenvPresent', 'VsixInstallerPresent', 'MSBuildPresent', 'Framework462Present')) {
            if (-not $row.$check) { $problems.Add("VS$year check failed: $check") }
        }
        if ($BuildSmokeTest -and $row.MSBuildPresent -and $row.Framework462Present) {
            # 저장소 프로젝트와 분리한 디렉터리에서 WPF XAML과 C# 7.0을 실제 컴파일한다.
            $probeDirectory = Join-Path $OutputDirectory "smoke\$year"
            $null = New-Item -ItemType Directory -Path $probeDirectory -Force
            $probeProject = Join-Path $probeDirectory 'WpfProbe.csproj'
            @'
<Project ToolsVersion="15.0" DefaultTargets="Build" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup><ProjectGuid>{EA53B95C-63A4-4C65-AB7D-9E13CACB55A6}</ProjectGuid><ProjectTypeGuids>{60DC8134-EBA5-43B8-BCC9-BB4BC16C2548};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids><OutputType>Library</OutputType><TargetFrameworkVersion>v4.6.2</TargetFrameworkVersion><LangVersion>7.0</LangVersion><AssemblyName>WpfProbe</AssemblyName><RootNamespace>CodexOrig.TestEnvironment</RootNamespace><OutputPath>bin\</OutputPath><PlatformTarget>AnyCPU</PlatformTarget></PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'"><DebugSymbols>true</DebugSymbols><DebugType>full</DebugType><Optimize>false</Optimize></PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)' == 'Release|AnyCPU'"><DebugType>pdbonly</DebugType><Optimize>true</Optimize></PropertyGroup>
  <ItemGroup><Reference Include="System" /><Reference Include="System.Core" /><Reference Include="System.Xaml" /><Reference Include="WindowsBase" /><Reference Include="PresentationCore" /><Reference Include="PresentationFramework" /><Compile Include="ProbeWindow.xaml.cs" /><Page Include="ProbeWindow.xaml" /></ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
'@ | Set-Content -LiteralPath $probeProject -Encoding UTF8
            @'
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.28307.168
MinimumVisualStudioVersion = 15.0.26228.4
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "WpfProbe", "WpfProbe.csproj", "{EA53B95C-63A4-4C65-AB7D-9E13CACB55A6}"
EndProject
Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
    GlobalSection(ProjectConfigurationPlatforms) = postSolution
        {EA53B95C-63A4-4C65-AB7D-9E13CACB55A6}.Release|Any CPU.ActiveCfg = Release|AnyCPU
        {EA53B95C-63A4-4C65-AB7D-9E13CACB55A6}.Release|Any CPU.Build.0 = Release|AnyCPU
    EndGlobalSection
EndGlobal
'@ | Set-Content -LiteralPath (Join-Path $probeDirectory 'WpfProbe.sln') -Encoding UTF8
            '<Window x:Class="CodexOrig.TestEnvironment.ProbeWindow" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><Grid><TextBlock Text="VSIX Test Environment" /></Grid></Window>' | Set-Content -LiteralPath (Join-Path $probeDirectory 'ProbeWindow.xaml') -Encoding UTF8
            @'
namespace CodexOrig.TestEnvironment
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : ProbeWindow
    /// desc         : .NET 4.6.2 WPF/XAML compilation probe.
    /// author       : CodexOrig
    /// create date  : 2026-10-18
    /// update date  : 2026-10-18 CodexOrig : independent environment validation.
    /// </summary>
    /// ---------------------------------------------------------------------
    public partial class ProbeWindow : System.Windows.Window
    {
        /// ---------------------------------------------------------------------
        /// <summary>
        /// name         : ProbeWindow
        /// desc         : Compile the generated XAML InitializeComponent call.
        /// author       : CodexOrig
        /// create date  : 2026-10-18
        /// update date  : 2026-10-18 CodexOrig : WPF constructor compilation probe.
        /// </summary>
        /// ---------------------------------------------------------------------
        public ProbeWindow() { InitializeComponent(); }
    }
}
'@ | Set-Content -LiteralPath (Join-Path $probeDirectory 'ProbeWindow.xaml.cs') -Encoding UTF8
            $row.SmokeLog = Join-Path $probeDirectory 'msbuild.log'
            $buildOutput = & $msbuildPath $probeProject /t:Rebuild /nologo /v:minimal /p:Configuration=Release 2>&1
            $buildCode = $LASTEXITCODE
            $buildOutput | Set-Content -LiteralPath $row.SmokeLog -Encoding UTF8
            if ($buildCode -eq 0 -and (Test-Path -LiteralPath (Join-Path $probeDirectory 'bin\WpfProbe.dll'))) { $row.SmokeBuild = 'Passed' }
            else { $row.SmokeBuild = 'Failed'; $problems.Add("VS$year WPF/.NET Framework 4.6.2 smoke build failed ($buildCode).") }
        }
        $rows += $row
    }
}
$report = [pscustomobject]@{
    CheckedAt = [DateTimeOffset]::Now.ToString('o'); CheckKind = 'InstalledIdeAndWpfBuild'
    Succeeded = ($problems.Count -eq 0); IDEs = $rows
    Problems = @($problems.ToArray()); RequiredComponents = $components
    IdeUiAndVsixRuntimeTested = $false
}
$report | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'environment-report.json') -Encoding UTF8
if ($PassThru) { return $report }
$report | ConvertTo-Json -Depth 7
if (-not $report.Succeeded) { exit 1 }
exit 0
