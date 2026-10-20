#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-SettingsTransaction
desc         : 설정 트랜잭션 소스 또는 고정 DLL을 독립 프로세스에서 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공통 빌드와 개인 설정을 변경하지 않는 실패 검증 추가.
               2026-10-21 CodexOrig 보조 개발 055 : 현재 저장 helper와 필수 규칙 의존성을 독립 소스 컴파일에 포함한다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe',
    [string]$AssemblyPath
)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputDir = Join-Path $repositoryDir ('artifacts\task012\settings-transaction-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $outputDir -Force
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $outputDir 'SettingsTransactionTests.exe'
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
    foreach ($source in @('ChatWorkspace.cs', 'MarkdownWorkspaceStore.cs', 'MarkdownWorkspaceStore.Committed.cs', 'AssistantSettingsTransaction.cs', 'MandatoryInstructionStore.cs', 'DocumentAttachmentReader.cs', 'DocumentWorkerLauncher.cs', 'AttachmentDisplayInfo.cs')) {
        $compileArguments += Join-Path $repositoryDir ('src\VS2017.AIAssistant\Chat\' + $source)
    }
}
$compileArguments += Join-Path $PSScriptRoot 'SettingsTransactionTests.cs'
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "설정 트랜잭션 테스트 컴파일 실패: $LASTEXITCODE" }
& $testExe $outputDir 2>&1 | Tee-Object -FilePath (Join-Path $outputDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "설정 트랜잭션 테스트 실패: $LASTEXITCODE ($outputDir)" }
Write-Output "설정 트랜잭션 검증 결과: $outputDir"
