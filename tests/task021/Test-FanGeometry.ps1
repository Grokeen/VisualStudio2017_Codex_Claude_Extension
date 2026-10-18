#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-FanGeometry
desc         : 소스 독립 모드 또는 지정한 고정 제품 DLL의 실제 환형 버튼 기하를 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : TASK-021 공유 제품 빌드·개인 설정·외부 계정 없는 독립 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') {
    throw 'Windows PowerShell 5.1 -STA로 실행하세요.'
}
$fanRepo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $fanRepo ('artifacts\task021\fan-' + [guid]::NewGuid().ToString('N').Substring(0,10)) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach ($fanFramework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')) { Add-Type -AssemblyName $fanFramework }
$fanReferences = @('System.dll','System.Core.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
$fanResolve = $null
$fanPriorSettings = $env:VS2017_AI_SETTINGS_PATH
$fanPriorWorkspace = $env:VS2017_AI_WORKSPACE_PATH
$fanPriorDiscovery = $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $OutputDirectory 'settings.json'
    $env:VS2017_AI_WORKSPACE_PATH = Join-Path $OutputDirectory 'workspace'
    $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = '1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH, '{"HideWelcome":true,"FontSize":14}', (New-Object Text.UTF8Encoding($true)))
    $fanMode = 'SourceOnlyFixture'; $fanProductHash = $null
    if ($AssemblyPath) {
        $AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
        $fanProductHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
        $fanAssets = Get-Content -LiteralPath (Join-Path $fanRepo 'src\VS2017.AIAssistant\obj\project.assets.json') -Encoding UTF8 -Raw | ConvertFrom-Json
        $fanPackageRoot = $fanAssets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
        $fanPackageReferences = @{}
        foreach ($fanEntry in $fanAssets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
            if ($fanEntry.Value.PSObject.Properties.Name -contains 'compile') {
                foreach ($fanItem in $fanEntry.Value.compile.PSObject.Properties.Name) {
                    if ($fanItem -like '*.dll') { $fanPackageReferences[[IO.Path]::GetFileNameWithoutExtension($fanItem)] = Join-Path $fanPackageRoot ($fanAssets.libraries.($fanEntry.Name).path + '/' + $fanItem) }
                }
            }
        }
        $fanResolve = [ResolveEventHandler]{ param($sender,$eventArgs)
            $fanName = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name
            if ($fanPackageReferences.ContainsKey($fanName)) { return [Reflection.Assembly]::LoadFrom($fanPackageReferences[$fanName]) }
            return $null
        }
        [AppDomain]::CurrentDomain.add_AssemblyResolve($fanResolve)
        $fanProduct = [Reflection.Assembly]::LoadFrom($AssemblyPath)
        $null = [Reflection.Assembly]::LoadFrom($fanPackageReferences['Microsoft.VisualStudio.Shell.15.0'])
        Add-Type -ReferencedAssemblies $fanReferences -Path (Join-Path $PSScriptRoot 'FanGeometryChecks.cs')
        $fanView = [Activator]::CreateInstance($fanProduct.GetType('VS2017.AIAssistant.Views.AssistantControl', $true))
        foreach ($fanFieldName in @('scmLookupStarted', 'scmLookupCompleted')) {
            $fanField = $fanView.GetType().GetField($fanFieldName, [Reflection.BindingFlags]'Instance,NonPublic')
            if ($fanField) { $fanField.SetValue($fanView, $true) }
        }
        $fanMode = 'FrozenProductAssembly'
    }
    else {
        Add-Type -ReferencedAssemblies $fanReferences -Path @((Join-Path $fanRepo 'src\VS2017.AIAssistant\Views\AssistantControl.Fan.cs'), (Join-Path $PSScriptRoot 'FanControlStub.cs'), (Join-Path $PSScriptRoot 'FanGeometryChecks.cs'))
        $fanView = New-Object VS2017.AIAssistant.Views.AssistantControl
    }
    $fanResult = [CodexCode.Task021.FanGeometryChecks]::Run($fanView, $OutputDirectory)
    $fanReport = [pscustomobject]@{
        Succeeded=$fanResult.Succeeded; Mode=$fanMode; Passed=$fanResult.Checks.Count; Checks=$fanResult.Checks; Error=$fanResult.Error
        GeometrySnapshots=$fanResult.GeometrySnapshots
        ProductAssemblyPath=$AssemblyPath; ProductAssemblySha256=$fanProductHash
        SourceSha256=(Get-FileHash -LiteralPath (Join-Path $fanRepo 'src\VS2017.AIAssistant\Views\AssistantControl.Fan.cs') -Algorithm SHA256).Hash
        PersonalSettingsRead=$false; AiInvoked=$false; SharedProductBuild=$false; CheckedAt=(Get-Date).ToString('O'); OutputDirectory=$OutputDirectory
    }
    $fanReport | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $fanReport | ConvertTo-Json -Depth 5
    if (!$fanResult.Succeeded) { throw "부채꼴 버튼 검증 실패: $OutputDirectory" }
}
finally {
    if ($fanResolve) { [AppDomain]::CurrentDomain.remove_AssemblyResolve($fanResolve) }
    $env:VS2017_AI_SETTINGS_PATH = $fanPriorSettings
    $env:VS2017_AI_WORKSPACE_PATH = $fanPriorWorkspace
    $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = $fanPriorDiscovery
}
