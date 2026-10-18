#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ConversationCollaboration
desc         : 고정 배포 DLL과 신규 협업 소스 또는 통합 제품 DLL로 독립 저장 검증을 실행한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공유 빌드·개인 설정·실제 CLI 실행 없이 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$AssemblyPath,
    [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputDir = Join-Path $repositoryDir ('artifacts\c\' + [guid]::NewGuid().ToString('N').Substring(0, 10))
$null = New-Item -ItemType Directory -Path $outputDir -Force
$referencePath = Join-Path $outputDir 'VS2017.AIAssistant.dll'
if ($AssemblyPath) { Copy-Item -LiteralPath (Resolve-Path -LiteralPath $AssemblyPath).Path -Destination $referencePath }
else {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $repositoryDir 'releases\v0.5.0\VS2017.AIAssistant-0.5.0.vsix'))
    try { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('VS2017.AIAssistant.dll'), $referencePath) }
    finally { $zip.Dispose() }
}
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $outputDir 'CollaborationTests.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/out:' + $testExe), ('/reference:' + $referencePath))
foreach ($reference in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions')) { $compileArguments += '/reference:' + (Join-Path $frameworkDir ($reference + '.dll')) }
if (!$AssemblyPath) { $compileArguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Chat\ConversationCollaboration.cs' }
$compileArguments += Join-Path $PSScriptRoot 'CollaborationTests.cs'
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "협업 검사 컴파일 실패: $LASTEXITCODE" }
$linkTarget = Join-Path $outputDir 'junction-target'
$linkPath = Join-Path $outputDir 'junction-workspace'
$null = New-Item -ItemType Directory -Path $linkTarget
$null = New-Item -ItemType Junction -Path $linkPath -Target $linkTarget
& $testExe $outputDir $linkPath 2>&1 | Tee-Object -FilePath (Join-Path $outputDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "협업 검사 실패: $LASTEXITCODE ($outputDir)" }
Get-FileHash -LiteralPath $referencePath -Algorithm SHA256
Write-Output "협업 검사 결과: $outputDir"
