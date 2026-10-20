#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-MarkdownWorkspace
desc         : 실제 저장 모델 소스만 독립 컴파일하여 공통 빌드 출력과 개인 기록을 변경하지 않는다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : .NET Framework 4.6.2·C# 7.0 독립 저장 회귀 추가.
               2026-10-20 CodexOrig : TASK068 확정 기록 인덱스 helper를 독립 소스 빌드에 포함.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe',
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputDir = Join-Path $repositoryDir ('artifacts\task012\workspace-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $outputDir -Force
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $outputDir 'MarkdownWorkspaceTests.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/out:' + $testExe))
foreach ($reference in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions', 'System.Xml', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) {
    $compileArguments += '/reference:' + (Join-Path $frameworkDir ($reference + '.dll'))
}
if ($AssemblyPath) {
    $resolvedAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
    Copy-Item -LiteralPath $resolvedAssembly -Destination $outputDir
    $compileArguments += '/reference:' + $resolvedAssembly
}
else {
    $compileArguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Chat\ChatWorkspace.cs'
    $compileArguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Chat\MarkdownWorkspaceStore.cs'
    $compileArguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Chat\MarkdownWorkspaceStore.Committed.cs'
    foreach ($source in @('MandatoryInstructionStore.cs', 'AssistantSettingsTransaction.cs', 'DocumentAttachmentReader.cs', 'DocumentWorkerLauncher.cs')) {
        $compileArguments += Join-Path $repositoryDir ('src\VS2017.AIAssistant\Chat\' + $source)
    }
}
$compileArguments += Join-Path $PSScriptRoot 'MarkdownWorkspaceTests.cs'
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "Markdown 테스트 컴파일 실패: $LASTEXITCODE" }
$linkTargetDir = Join-Path $outputDir 'junction-target'
$linkDir = Join-Path $outputDir 'junction-workspace'
$null = New-Item -ItemType Directory -Path $linkTargetDir
$null = New-Item -ItemType Junction -Path $linkDir -Target $linkTargetDir
& $testExe $outputDir $linkDir $linkTargetDir 2>&1 | Tee-Object -FilePath (Join-Path $outputDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "Markdown 테스트 실패: $LASTEXITCODE ($outputDir)" }
Write-Output "Markdown 검증 결과: $outputDir"
