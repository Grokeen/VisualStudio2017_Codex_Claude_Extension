#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ContextMenus
desc         : dark 메뉴·하위 Popup·텍스트 fallback과 실제 제품의 설정 메뉴 보존을 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : source-only 메모리와 예약된 실제 GUI를 구별하고 DLL 사본·해시·PNG를 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory, [switch]$SourceOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Windows PowerShell 5.1 -STA로 실행하세요.' }
if (!$SourceOnly -and !$AssemblyPath) { throw '실제 메뉴 검사에는 고정 -AssemblyPath가 필요합니다.' }
$menuRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $menuRepository ('artifacts\task022\menus-' + [guid]::NewGuid().ToString('N')) }
$menuOutput = [IO.Path]::GetFullPath($OutputDirectory)
if ((Test-Path -LiteralPath $menuOutput) -and @(Get-ChildItem -LiteralPath $menuOutput -Force).Count -gt 0) { throw '기존 증거를 보존하도록 새 경로 또는 빈 결과 폴더를 사용하세요.' }
$null = New-Item -ItemType Directory -Path $menuOutput -Force
foreach ($menuFramework in @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'System.Web.Extensions')) { Add-Type -AssemblyName $menuFramework }
$menuThemePath = Join-Path $menuRepository 'src\VS2017.AIAssistant\Views\AssistantTheme.xaml'
$menuControl = $null
$menuHash = $null
$menuAssemblyPath = $null
$menuInput = $null
$menuResolver = $null
$menuOldSettings = $env:VS2017_AI_SETTINGS_PATH; $menuOldWorkspace = $env:VS2017_AI_WORKSPACE_PATH; $menuOldDiscovery = $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $menuOutput 'isolated-settings.json'; $env:VS2017_AI_WORKSPACE_PATH = Join-Path $menuOutput 'isolated-workspace'; $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = '1'
    if ($SourceOnly) {
        $menuThemeText = [IO.File]::ReadAllText($menuThemePath)
        $menuThemeText = $menuThemeText.Replace('xmlns:vs="clr-namespace:Microsoft.VisualStudio.Shell;assembly=Microsoft.VisualStudio.Shell.15.0"', '')
        $menuThemeText = $menuThemeText.Replace('vs:VsBrushes.ToolWindowBackgroundKey', 'SystemColors.ControlBrushKey').Replace('vs:VsBrushes.ToolWindowTextKey', 'SystemColors.ControlTextBrushKey')
        $menuResources = [Windows.Markup.XamlReader]::Parse($menuThemeText)
    } else {
        $menuInput = (Resolve-Path -LiteralPath $AssemblyPath).Path
        if ([Reflection.AssemblyName]::GetAssemblyName($menuInput).Name -ne 'VS2017.AIAssistant') { throw '실제 VS2017.AIAssistant 제품 DLL을 지정하세요.' }
        $menuHash = (Get-FileHash -LiteralPath $menuInput -Algorithm SHA256).Hash
        $menuAssemblyPath = Join-Path $menuOutput 'VS2017.AIAssistant.dll'; Copy-Item -LiteralPath $menuInput -Destination $menuAssemblyPath
        if ((Get-FileHash -LiteralPath $menuAssemblyPath -Algorithm SHA256).Hash -ne $menuHash) { throw '메뉴 검사 DLL 복사본 해시 불일치' }
        $menuAssets = Get-Content -Encoding UTF8 -LiteralPath (Join-Path $menuRepository 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw | ConvertFrom-Json
        $menuPackageRoot = $menuAssets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
        $menuReferences = @{}
        foreach ($menuEntry in $menuAssets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) { if ($menuEntry.Value.PSObject.Properties.Name -contains 'compile') { foreach ($menuItem in $menuEntry.Value.compile.PSObject.Properties.Name) { if ($menuItem -like '*.dll') { $menuReferences[[IO.Path]::GetFileNameWithoutExtension($menuItem)] = Join-Path $menuPackageRoot ($menuAssets.libraries.($menuEntry.Name).path + '/' + $menuItem) } } } }
        $menuResolver = [ResolveEventHandler]{param($sender,$eventArgs) $menuName = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name; if ($menuReferences.ContainsKey($menuName)) { return [Reflection.Assembly]::LoadFrom($menuReferences[$menuName]) }; return $null}
        [AppDomain]::CurrentDomain.add_AssemblyResolve($menuResolver)
        $menuAssembly = [Reflection.Assembly]::LoadFrom($menuAssemblyPath)
        if ($menuReferences.ContainsKey('Microsoft.VisualStudio.Shell.15.0')) { [Reflection.Assembly]::LoadFrom($menuReferences['Microsoft.VisualStudio.Shell.15.0']) | Out-Null }
        [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH, '{"HideWelcome":true,"ShowHistory":false,"FontSize":14,"ThemeMode":"dark"}', (New-Object Text.UTF8Encoding($true)))
        $menuControl = [Activator]::CreateInstance($menuAssembly.GetType('VS2017.AIAssistant.Views.AssistantControl', $true))
        $menuControl.GetType().GetField('scmLookupStarted', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($menuControl, $true)
        $menuResources = $menuControl.Resources
    }
    $menuTestReferences = @('System.dll', 'System.Core.dll', [Windows.DependencyObject].Assembly.Location, [Windows.Media.Visual].Assembly.Location, [Windows.Window].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'MenuChecks.cs') -ReferencedAssemblies $menuTestReferences
    [MenuChecks]::Run($menuResources, $menuControl, !$SourceOnly, $menuOutput)
    $menuResult = [pscustomobject]@{
        Succeeded = [MenuChecks]::Errors.Count -eq 0; Passed = [MenuChecks]::Checks.Count; Failed = [MenuChecks]::Errors.Count; SourceOnly = [bool]$SourceOnly
        Checks = @([MenuChecks]::Checks); Errors = @([MenuChecks]::Errors)
        AssemblyPath = $menuInput; LoadedAssemblyPath = $menuAssemblyPath; AssemblySha256 = $menuHash
        SourceThemeSha256 = (Get-FileHash -LiteralPath $menuThemePath -Algorithm SHA256).Hash
        Mode = $(if ($SourceOnly) { 'ThemeSourceMemoryWithShellColorKeysMappedToSystemKeys' } else { 'ActualProductAssemblyAndOpenedPopups' })
        WindowShown = !$SourceOnly; ClipboardMutated = $false; NetworkRequested = $false; PersonalCredentialsRead = $false; NativeExecutionPerformed = $false; CheckedAt = [DateTimeOffset]::Now.ToString('o')
    }
    if ($menuInput -and (Get-FileHash -LiteralPath $menuInput -Algorithm SHA256).Hash -ne $menuHash) { throw '메뉴 검사 중 입력 고정 DLL이 변경되었습니다.' }
    $menuResult | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $menuOutput 'result.json') -Encoding UTF8
    $menuResult | ConvertTo-Json -Depth 7
    if (!$menuResult.Succeeded) { throw "메뉴 검사 실패: $menuOutput" }
} finally {
    if ($menuControl) { $menuControl.Dispose() }
    if ($menuResolver) { [AppDomain]::CurrentDomain.remove_AssemblyResolve($menuResolver) }
    $env:VS2017_AI_SETTINGS_PATH = $menuOldSettings; $env:VS2017_AI_WORKSPACE_PATH = $menuOldWorkspace; $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = $menuOldDiscovery
}
