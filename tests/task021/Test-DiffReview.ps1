#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-DiffReview
desc         : 실제 수정안 소스 또는 고정 제품 DLL의 줄 계수·unified 비교를 격리 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공유 빌드·IDE·사용자 문서를 변경하지 않는 TASK-021 회귀 추가.
update date  : 2026-10-18 CodexCode : 묶음 실행기의 명시적 결과 디렉터리를 지원하고 기본 임의 경로 유지.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe', [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir ('artifacts\diff\' + [guid]::NewGuid().ToString('N').Substring(0, 10)) }
$outputDir = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $outputDir -Force
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $outputDir 'DiffReviewChecks.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/out:' + $testExe))
foreach ($reference in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions')) { $compileArguments += '/reference:' + (Join-Path $frameworkDir ($reference + '.dll')) }
$inputs = @()
if ($AssemblyPath) {
    $resolvedAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $copiedAssembly = Join-Path $outputDir ([IO.Path]::GetFileName($resolvedAssembly))
    Copy-Item -LiteralPath $resolvedAssembly -Destination $copiedAssembly
    $compileArguments += '/reference:' + $copiedAssembly
    $inputs += Get-FileHash -LiteralPath $copiedAssembly -Algorithm SHA256 | Select-Object Path, Hash
}
else {
    foreach ($source in @('Editing\DocumentSnapshot.cs', 'Editing\EditProposal.cs')) {
        $sourcePath = Join-Path (Join-Path $repositoryDir 'src\VS2017.AIAssistant') $source
        $compileArguments += $sourcePath
        $inputs += Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256 | Select-Object Path, Hash
    }
}
$testSource = Join-Path $PSScriptRoot 'DiffReviewChecks.cs'
$compileArguments += $testSource
$inputs += Get-FileHash -LiteralPath $testSource -Algorithm SHA256 | Select-Object Path, Hash
@{ Mode = $(if ($AssemblyPath) { 'ProductAssembly' } else { 'ProductionSource' }); Inputs = $inputs } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $outputDir 'inputs.json') -Encoding UTF8
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "변경 비교 검사 컴파일 실패: $LASTEXITCODE" }
& $testExe $outputDir 2>&1 | Tee-Object -FilePath (Join-Path $outputDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "변경 비교 검사 실패: $LASTEXITCODE ($outputDir)" }
Write-Output "변경 비교 검사 결과: $outputDir"
