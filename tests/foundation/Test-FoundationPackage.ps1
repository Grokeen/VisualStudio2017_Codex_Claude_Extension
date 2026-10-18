#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-FoundationPackage
desc         : 실제 VSIX의 등록·메뉴·BAML과 빌드된 WPF 컨트롤을 검증한다.
author       : CodexCode
create date  : 2026-10-17
update date  : 2026-10-18 CodexCode : 소스 편집 UI의 기본 승인 상태와 전체 화면 렌더링 검증.
update date  : 2026-10-18 CodexOrig : 고정 DLL·VSIX 지정과 밝은/어두운 호스트의 제품 검정 팔레트 검증.
-------------------------------------------------------------------------
.SYNOPSIS
    Requires a completed build and Windows PowerShell 5.1 (full Framework, STA).
.DESCRIPTION
    Creates no IDE instance. VS installation, docking and menu invocation remain manual tests.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$OutputDirectory,
    [string]$AssemblyPath,
    [string]$VsixPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or
    [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Windows PowerShell 5.1의 -STA 모드로 실행하세요.'
}
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$projectDir = Join-Path $repositoryDir 'src\VS2017.AIAssistant'
$outputDir = Join-Path $projectDir "bin\$Configuration"
if (!$AssemblyPath) { $AssemblyPath = Join-Path $outputDir 'VS2017.AIAssistant.dll' }
if (!$VsixPath) { $VsixPath = Join-Path $outputDir 'VS2017.AIAssistant.vsix' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir 'artifacts\phase1' }
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$script:passedChecks = 0

<#
-------------------------------------------------------------------------
name         : Assert-FoundationCondition
desc         : 빌드 산출물 검증 실패를 즉시 오류로 보고하고 통과 수를 집계한다.
author       : CodexCode
create date  : 2026-10-17
update date  : 2026-10-17 CodexCode : 검증 실패 시 비정상 종료 지원.
update date  : 2026-10-18 CodexOrig : 고정 DLL·VSIX 지정과 밝은/어두운 호스트의 제품 검정 팔레트 검증.
-------------------------------------------------------------------------
#>
function Assert-FoundationCondition {
    param([bool]$Condition, [string]$Description)
    if (!$Condition) { throw "FAIL: $Description" }
    $script:passedChecks++
    Write-Output "PASS: $Description"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem, PresentationFramework, PresentationCore, WindowsBase
Assert-FoundationCondition (Test-Path -LiteralPath $vsixPath) 'VSIX artifact exists'
$archive = [IO.Compression.ZipFile]::OpenRead($vsixPath)
try {
    foreach ($entryName in @('extension.vsixmanifest', 'VS2017.AIAssistant.dll', 'VS2017.AIAssistant.pkgdef')) {
        Assert-FoundationCondition ($null -ne $archive.GetEntry($entryName)) "VSIX includes $entryName"
    }
    $extraDlls = @($archive.Entries | Where-Object {
        $_.FullName -like '*.dll' -and $_.FullName -ne 'VS2017.AIAssistant.dll'
    })
    Assert-FoundationCondition ($extraDlls.Count -eq 0) 'IDE SDK and dependency DLLs are not bundled'
    $manifestReader = New-Object IO.StreamReader($archive.GetEntry('extension.vsixmanifest').Open())
    try { [xml]$manifest = $manifestReader.ReadToEnd() } finally { $manifestReader.Dispose() }
    Assert-FoundationCondition ($manifest.PackageManifest.Assets.Asset.Path -eq 'VS2017.AIAssistant.pkgdef') 'VsPackage asset resolves to the packaged pkgdef'
    $sourceManifest = New-Object Xml.XmlDocument
    $sourceManifest.Load((Join-Path $projectDir 'source.extension.vsixmanifest'))
    Assert-FoundationCondition ($manifest.PackageManifest.Installation.InstallationTarget.Version -eq
        $sourceManifest.PackageManifest.Installation.InstallationTarget.Version) 'Built installation range matches the source manifest'
    $pkgdefReader = New-Object IO.StreamReader($archive.GetEntry('VS2017.AIAssistant.pkgdef').Open())
    try { $pkgdef = $pkgdefReader.ReadToEnd() } finally { $pkgdefReader.Dispose() }
    Assert-FoundationCondition ($pkgdef.Contains('"Class"="VS2017.AIAssistant.AiAssistantPackage"')) 'Package class is registered'
    Assert-FoundationCondition ($pkgdef.Contains('"CodeBase"="$PackageFolder$\VS2017.AIAssistant.dll"')) 'Package CodeBase resolves inside the extension'
    Assert-FoundationCondition ($pkgdef.Contains('Menus.ctmenu')) 'Menu resource is registered'
    Assert-FoundationCondition ($pkgdef.Contains('"Name"="VS2017.AIAssistant.ToolWindows.AssistantToolWindow"')) 'Tool window class is registered'
}
finally { $archive.Dispose() }

# IDE의 어셈블리를 복사하지 않고 NuGet 캐시의 컴파일 참조를 테스트 프로세스에서만 로드한다.
$assetGraph = Get-Content -LiteralPath (Join-Path $projectDir 'obj\project.assets.json') -Raw | ConvertFrom-Json
$cacheRootDir = $assetGraph.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$referencePaths = @{}
foreach ($dependencyEntry in $assetGraph.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($dependencyEntry.Value.PSObject.Properties.Name -contains 'compile') {
        foreach ($compileItem in $dependencyEntry.Value.compile.PSObject.Properties.Name) {
            if ($compileItem -like '*.dll') {
                $dependencyPath = $assetGraph.libraries.($dependencyEntry.Name).path
                $referencePaths[[IO.Path]::GetFileNameWithoutExtension($compileItem)] =
                    Join-Path $cacheRootDir ($dependencyPath + '/' + $compileItem)
            }
        }
    }
}
$resolveHandler = [ResolveEventHandler]{
    param($sender, $eventArgs)
    $requestedName = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name
    if ($referencePaths.ContainsKey($requestedName)) {
        return [Reflection.Assembly]::LoadFrom($referencePaths[$requestedName])
    }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolveHandler)
$previousSettingsPath = $env:VS2017_AI_SETTINGS_PATH
$previousWorkspacePath = $env:VS2017_AI_WORKSPACE_PATH
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $OutputDirectory ('isolated-settings-' + [guid]::NewGuid().ToString('N') + '.json')
    $env:VS2017_AI_WORKSPACE_PATH = Join-Path $OutputDirectory ('workspace-' + [guid]::NewGuid().ToString('N'))
    $extensionAssembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
    $menuStream = $extensionAssembly.GetManifestResourceStream('VSPackage.resources')
    $menuReader = New-Object Resources.ResourceReader($menuStream)
    try {
        $menuKeys = @($menuReader.GetEnumerator() | ForEach-Object Key)
        Assert-FoundationCondition ($menuKeys -contains 'Menus.ctmenu') 'Compiled menu is embedded in VSPackage.resources'
    }
    finally { $menuReader.Dispose() }
    $bamlStream = $extensionAssembly.GetManifestResourceStream('VS2017.AIAssistant.g.resources')
    $bamlReader = New-Object Resources.ResourceReader($bamlStream)
    try {
        $bamlKeys = @($bamlReader.GetEnumerator() | ForEach-Object Key)
        Assert-FoundationCondition ($bamlKeys -contains 'views/assistantcontrol.baml') 'Compiled WPF view is embedded'
    }
    finally { $bamlReader.Dispose() }

    $controlType = $extensionAssembly.GetType('VS2017.AIAssistant.Views.AssistantControl', $true)
    $control = [Activator]::CreateInstance($controlType)
    Assert-FoundationCondition ($null -ne $control.Content) 'Compiled WPF view initializes'
    Assert-FoundationCondition ($null -ne $control.FindName('ProviderChoice')) 'Source editing provider selector exists'
    Assert-FoundationCondition ($control.FindName('ReviewApproval').IsChecked -eq $false) 'Editing approval starts unchecked'
    Assert-FoundationCondition (!$control.FindName('ApplyButton').IsEnabled) 'Apply is disabled without an approved proposal'
    $themeHost = New-Object Windows.Controls.Border
    $themeHost.Child = $control
    foreach ($theme in @('Light', 'Dark')) {
        $background = if ($theme -eq 'Light') { [Windows.Media.Brushes]::White } else { [Windows.Media.Brushes]::Black }
        $foreground = if ($theme -eq 'Light') { [Windows.Media.Brushes]::Black } else { [Windows.Media.Brushes]::White }
        $themeHost.Resources[[Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowBackgroundKey] = $background
        $themeHost.Resources[[Microsoft.VisualStudio.Shell.VsBrushes]::ToolWindowTextKey] = $foreground
        foreach ($width in @(320, 200)) {
            $height = if ($width -eq 200) { 1100 } else { 900 }
            $control.Measure((New-Object Windows.Size($width, $height)))
            $control.Arrange((New-Object Windows.Rect(0, 0, $width, $height)))
            $control.UpdateLayout()
            # 실제 WPF 메시지 루프처럼 테마·바인딩·렌더 무효화를 처리한 뒤 캡처한다.
            $control.Dispatcher.Invoke([Action]{}, [Windows.Threading.DispatcherPriority]::Render)
            Assert-FoundationCondition ($control.Background.Color.R -lt 60 -and $control.Foreground.Color.R -gt 200) "$theme IDE host preserves consistent black palette at width $width"
            Assert-FoundationCondition ($control.FindName('ReviewApproval').Content.Foreground -eq $control.Foreground) "$theme approval label remains readable at width $width"
            Assert-FoundationCondition ($control.FindName('SettingsPanel').Foreground -eq $control.Foreground -and $control.FindName('ReviewPanel').Foreground -eq $control.Foreground) "$theme settings and review headers remain readable at width $width"
            $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
            $bitmap.Render($control)
            $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
            $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
            $imagePath = Join-Path $OutputDirectory "$theme-$width.png"
            $imageStream = [IO.File]::Create($imagePath)
            try { $encoder.Save($imageStream) } finally { $imageStream.Dispose() }
            Assert-FoundationCondition ((Get-Item -LiteralPath $imagePath).Length -gt 100) "WPF renders $theme at width $width"
        }
    }
}
finally {
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolveHandler)
    $env:VS2017_AI_SETTINGS_PATH = $previousSettingsPath
    $env:VS2017_AI_WORKSPACE_PATH = $previousWorkspacePath
}

Write-Output "Foundation checks: $script:passedChecks PASS. Actual Visual Studio installation and IDE execution remain untested."
