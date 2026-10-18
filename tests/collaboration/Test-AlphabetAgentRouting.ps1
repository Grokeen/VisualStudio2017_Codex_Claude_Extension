#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-AlphabetAgentRouting
desc         : 고정 참조 DLL과 신규 이름 소스 또는 최종 DLL로 A~Z 실제 저장 경계를 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 개인 기록·공유 빌드·AI 실행 없이 이름 라우팅 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputDir = Join-Path $repositoryDir ('artifacts\az\' + [guid]::NewGuid().ToString('N').Substring(0, 10))
$null = New-Item -ItemType Directory -Path $outputDir -Force
$referencePath = Join-Path $outputDir 'VS2017.AIAssistant.dll'
if ($AssemblyPath) { Copy-Item -LiteralPath (Resolve-Path -LiteralPath $AssemblyPath).Path -Destination $referencePath }
else {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $repositoryDir 'releases\v0.5.0\VS2017.AIAssistant-0.5.0.vsix'))
    try { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('VS2017.AIAssistant.dll'), $referencePath) } finally { $zip.Dispose() }
}
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $outputDir 'AlphabetAgentRoutingTests.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/out:' + $testExe), ('/reference:' + $referencePath))
foreach ($reference in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions')) { $compileArguments += '/reference:' + (Join-Path $frameworkDir ($reference + '.dll')) }
if (!$AssemblyPath) { $compileArguments += Join-Path $repositoryDir 'src\VS2017.AIAssistant\Chat\ConversationCollaboration.cs' }
$compileArguments += Join-Path $PSScriptRoot 'AlphabetAgentRoutingTests.cs'
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "A~Z 검사 컴파일 실패: $LASTEXITCODE" }
& $testExe $outputDir 2>&1 | Tee-Object -FilePath (Join-Path $outputDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "A~Z 검사 실패: $LASTEXITCODE ($outputDir)" }
Get-FileHash -LiteralPath $referencePath -Algorithm SHA256
Write-Output "A~Z 검사 결과: $outputDir"
