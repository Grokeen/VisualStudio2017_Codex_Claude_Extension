#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-SourceEditing
desc         : 실제 VSIX DLL을 참조하는 편집·CLI·WPF 통합 회귀 테스트를 컴파일하고 실행한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 독립 fixture exe와 한글·공백 경로를 사용한 검증 추가.
-------------------------------------------------------------------------
.SYNOPSIS
    Run after building the extension. No live AI calls, authentication or Visual Studio instance.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$projectDir = Join-Path $repositoryDir 'src\VS2017.AIAssistant'
$assemblyPath = Join-Path $projectDir "bin\$Configuration\VS2017.AIAssistant.dll"
if (!(Test-Path -LiteralPath $assemblyPath)) { throw 'VSIX를 먼저 빌드하세요.' }
if (!$CompilerPath) {
    $vswherePath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswherePath) {
        $CompilerPath = & $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild `
            -find 'MSBuild\**\Bin\Roslyn\csc.exe' | Select-Object -First 1
    }
    if (!$CompilerPath) {
        $portableCompiler = Join-Path $env:TEMP 'CodexCode-VsixFoundation\toolset\Roslyn\csc.exe'
        if (Test-Path -LiteralPath $portableCompiler) { $CompilerPath = $portableCompiler }
    }
}
if (!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath)) { throw '-CompilerPath에 C# 7.0 이상을 지원하는 csc.exe 경로를 지정하세요.' }
$assets = Get-Content -LiteralPath (Join-Path $projectDir 'obj\project.assets.json') -Raw | ConvertFrom-Json
$packageRootDir = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$referencePackage = $assets.libraries.PSObject.Properties | Where-Object { $_.Name -like 'Microsoft.NETFramework.ReferenceAssemblies.net462/*' } | Select-Object -First 1
$frameworkDir = Join-Path $packageRootDir ($referencePackage.Value.path + '/build/.NETFramework/v4.6.2')
$artifactRootDir = Join-Path $repositoryDir 'artifacts\source-editing'
$testDir = Join-Path $artifactRootDir ('run-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDir -Force
Copy-Item -LiteralPath $assemblyPath -Destination $testDir
foreach ($dependency in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($dependency.Value.compile) {
        foreach ($item in $dependency.Value.compile.PSObject.Properties.Name) {
            if ($item -like '*.dll') {
                Copy-Item -LiteralPath (Join-Path $packageRootDir ($assets.libraries.($dependency.Name).path + '/' + $item)) -Destination $testDir -Force
            }
        }
    }
}
$testExecutable = Join-Path $testDir 'EditingTests.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', "/out:$testExecutable")
foreach ($name in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions', 'System.Xaml', 'WindowsBase', 'PresentationCore', 'PresentationFramework')) {
    $compileArguments += '/reference:' + (Join-Path $frameworkDir ($name + '.dll'))
}
$compileArguments += '/reference:' + $assemblyPath
$compileArguments += Join-Path $PSScriptRoot 'EditingTests.cs'
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "테스트 컴파일 실패: $LASTEXITCODE" }
$fixturePath = Join-Path $testDir 'Fixture CLI 한글.exe'
Copy-Item -LiteralPath $testExecutable -Destination $fixturePath
& $testExecutable $testDir $fixturePath 2>&1 | Tee-Object -FilePath (Join-Path $testDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "편집 회귀 테스트 실패: $LASTEXITCODE (기록: $testDir)" }
Write-Output "테스트 산출물: $testDir"
