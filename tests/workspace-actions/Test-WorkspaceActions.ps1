#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-WorkspaceActions
desc         : 제품 서비스와 동일 소스를 컴파일해 합성 Git·네이티브 fixture만 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 실제 서버·DB·배포 대상에 접속하지 않는 독립 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$CompilerPath, [string]$GitPath, [string]$ProductAssemblyPath)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$GitPath) { $GitPath = (Get-Command git.exe -ErrorAction Stop).Source }
if (!$CompilerPath) {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $CompilerPath = & $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
}
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'C# 컴파일러 경로를 확인하세요.' }
$testDir = Join-Path $repositoryDir ('artifacts\workspace-actions\run-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDir -Force
$testExe = Join-Path $testDir 'WorkspaceActionTests.exe'
$fixtureExe = Join-Path $testDir 'Fixture 한글 CLI.exe'
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$arguments = @('/nologo','/noconfig','/nostdlib+','/langversion:7.0','/target:exe',"/out:$testExe")
foreach ($name in @('mscorlib','System','System.Core','System.Data')) { $arguments += '/reference:' + (Join-Path $frameworkDir ($name + '.dll')) }
if ($ProductAssemblyPath) {
    $ProductAssemblyPath = (Resolve-Path -LiteralPath $ProductAssemblyPath).Path
    Copy-Item -LiteralPath $ProductAssemblyPath -Destination $testDir
    $assets = Get-Content -LiteralPath (Join-Path $repositoryDir 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw | ConvertFrom-Json
    $packageRootDir = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    foreach ($dependency in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
        if ($dependency.Value.compile) {
            foreach ($item in $dependency.Value.compile.PSObject.Properties.Name) {
                if ($item -like '*.dll') { Copy-Item -LiteralPath (Join-Path $packageRootDir ($assets.libraries.($dependency.Name).path + '/' + $item)) -Destination $testDir -Force }
            }
        }
    }
    $arguments += '/define:PRODUCT'
    $arguments += '/reference:' + $ProductAssemblyPath
    $arguments += '/reference:' + (Join-Path $testDir 'Microsoft.VisualStudio.Shell.15.0.dll')
    foreach ($name in @('System.Xaml','WindowsBase','PresentationCore','PresentationFramework')) { $arguments += '/reference:' + (Join-Path $frameworkDir ($name + '.dll')) }
    $arguments += Join-Path $PSScriptRoot 'WorkspaceDialogChecks.cs'
} else {
    $arguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Services\WorkspaceActionService.cs'
    $arguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Providers\ProcessJob.cs'
}
$arguments += Join-Path $PSScriptRoot 'WorkspaceActionTests.cs'
& $CompilerPath @arguments
if ($LASTEXITCODE -ne 0) { throw '작업 서비스 테스트 컴파일 실패' }
Copy-Item -LiteralPath $testExe -Destination $fixtureExe
$previousSettingsPath = $env:VS2017_AI_SETTINGS_PATH
$previousWorkspacePath = $env:VS2017_AI_WORKSPACE_PATH
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $testDir 'ui-settings.json'
    $env:VS2017_AI_WORKSPACE_PATH = Join-Path $testDir 'workspace'
    & $testExe $testDir $GitPath $fixtureExe 2>&1 | Tee-Object -FilePath (Join-Path $testDir 'results.log')
} finally {
    $env:VS2017_AI_SETTINGS_PATH = $previousSettingsPath
    $env:VS2017_AI_WORKSPACE_PATH = $previousWorkspacePath
}
if ($LASTEXITCODE -ne 0) { throw "작업 서비스 회귀 테스트 실패: $testDir" }
Write-Output "작업 서비스 검증 산출물: $testDir"
