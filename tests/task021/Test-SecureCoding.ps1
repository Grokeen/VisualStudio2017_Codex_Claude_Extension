#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-SecureCoding
desc         : 실제 소스 또는 고정 제품 DLL의 2MiB 해시와 설정 거래·64KiB 경계를 격리 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 큰 실제 파일·IDE·계정·네트워크 없이 유한·성장·무한 스트림 회귀 추가.
update date  : 2026-10-18 CodexCode : 실제 두 프로세스 ABA 직렬화·재진입·abandoned·설정 크기와 오류 경계 추가.
update date  : 2026-10-18 CodexCode : 실제 SDK 어댑터의 전체 액세스 문서 interface 소스를 함께 컴파일.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory, [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir ('artifacts\task021\secure-' + [guid]::NewGuid().ToString('N').Substring(0, 10)) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $OutputDirectory -Force
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $OutputDirectory 'SecureCodingChecks.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/out:' + $testExe))
foreach ($reference in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions', 'System.Design', 'System.IO.Compression', 'System.Xml', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) { $compileArguments += '/reference:' + (Join-Path $frameworkDir ($reference + '.dll')) }
$assets = Get-Content -LiteralPath (Join-Path $repositoryDir 'src\VS2017.AIAssistant\obj\project.assets.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$references = @{}
foreach ($dependency in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($dependency.Value.compile) {
        foreach ($item in $dependency.Value.compile.PSObject.Properties.Name) {
            if ($item -like '*.dll') {
                $referencePath = Join-Path $packageRoot ($assets.libraries.($dependency.Name).path + '/' + $item)
                $references[[IO.Path]::GetFileNameWithoutExtension($item)] = $referencePath
                if (!$AssemblyPath) { $compileArguments += '/reference:' + $referencePath }
            }
        }
    }
}
$references | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'references.json') -Encoding UTF8
$inputs = @()
if ($AssemblyPath) {
    $resolvedAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $copiedAssembly = Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'
    Copy-Item -LiteralPath $resolvedAssembly -Destination $copiedAssembly
    $compileArguments += '/reference:' + $copiedAssembly
    $inputs += Get-FileHash -LiteralPath $copiedAssembly -Algorithm SHA256 | Select-Object Path, Hash
}
else {
    foreach ($source in @('Editing\DocumentSnapshot.cs', 'Editing\EditProposal.cs', 'Editing\IFullAccessDocumentService.cs', 'Services\VisualStudioDocumentService.cs', 'Chat\ChatWorkspace.cs', 'Chat\MarkdownWorkspaceStore.cs', 'Chat\AssistantSettingsTransaction.cs', 'Chat\DocumentAttachmentReader.cs', 'Chat\DocumentWorkerLauncher.cs')) {
        $sourcePath = Join-Path (Join-Path $repositoryDir 'src\VS2017.AIAssistant') $source
        $compileArguments += $sourcePath
        $inputs += Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256 | Select-Object Path, Hash
    }
}
$testSource = Join-Path $PSScriptRoot 'SecureCodingChecks.cs'
$compileArguments += $testSource
$inputs += Get-FileHash -LiteralPath $testSource -Algorithm SHA256 | Select-Object Path, Hash
@{ Mode = $(if ($AssemblyPath) { 'ProductAssembly' } else { 'ProductionSource' }); Inputs = $inputs } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'inputs.json') -Encoding UTF8
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "시큐어 코딩 검사 컴파일 실패: $LASTEXITCODE" }
& $testExe $OutputDirectory 2>&1 | Tee-Object -FilePath (Join-Path $OutputDirectory 'results.log')
if ($LASTEXITCODE -ne 0) { throw "시큐어 코딩 검사 실패: $LASTEXITCODE ($OutputDirectory)" }
Write-Output "시큐어 코딩 검사 결과: $OutputDirectory"
