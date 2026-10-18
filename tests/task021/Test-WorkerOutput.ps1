#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-WorkerOutput
desc         : 실제 제품 또는 독립 컴파일 소스의 문서 worker 출력 배수를 합성 입력으로 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 전용 복사 DLL 옆 fixture만 생성하며 운영 도우미·실제 문서·계정은 사용하지 않음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$workerOutputRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $workerOutputRepository ('artifacts\task021\worker-output-' + [guid]::NewGuid().ToString('N')) }
$workerOutputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $workerOutputRoot -Force
$workerSandbox = Join-Path $workerOutputRoot ('sandbox-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $workerSandbox
$workerCompiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
$workerFramework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
if (!(Test-Path -LiteralPath $workerCompiler)) { throw '독립 검사에 필요한 VS2017 C# 컴파일러가 없습니다.' }
$workerReferences = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/codepage:65001', '/warnaserror+')
foreach ($workerReference in @('mscorlib', 'System', 'System.Core', 'System.Xml', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Web.Extensions')) {
    $workerReferences += '/reference:' + (Join-Path $workerFramework ($workerReference + '.dll'))
}
$workerReaderSource = Join-Path $workerOutputRepository 'src\VS2017.AIAssistant\Chat\DocumentAttachmentReader.cs'
$workerLauncherSource = Join-Path $workerOutputRepository 'src\VS2017.AIAssistant\Chat\DocumentWorkerLauncher.cs'
$workerInputAssembly = $null
$workerInputHash = $null
if ($AssemblyPath) {
    $workerInputAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $workerInputHash = (Get-FileHash -LiteralPath $workerInputAssembly -Algorithm SHA256).Hash
    $workerSubject = Join-Path $workerSandbox ([IO.Path]::GetFileName($workerInputAssembly))
    Copy-Item -LiteralPath $workerInputAssembly -Destination $workerSubject
    if ((Get-FileHash -LiteralPath $workerSubject -Algorithm SHA256).Hash -ne $workerInputHash) { throw '제품 DLL 복사본 무결성 검사 실패' }
} else {
    $workerSubject = Join-Path $workerSandbox 'VS2017.AIAssistant.WorkerOutput.Subject.dll'
    & $workerCompiler @workerReferences /target:library ('/out:' + $workerSubject) $workerReaderSource $workerLauncherSource
    if ($LASTEXITCODE -ne 0) { throw '실제 문서 읽기·출력 배수 소스의 독립 컴파일 실패' }
}
$workerFixture = Join-Path $workerSandbox 'VS2017.AIAssistant.DocumentReader.exe'
& $workerCompiler @workerReferences /target:exe ('/out:' + $workerFixture) (Join-Path $PSScriptRoot 'WorkerOutputFixture.cs')
if ($LASTEXITCODE -ne 0) { throw '합성 무개행 출력 worker 컴파일 실패' }
$workerTests = Join-Path $workerSandbox 'WorkerOutputChecks.exe'
& $workerCompiler @workerReferences /target:exe ('/out:' + $workerTests) ('/reference:' + $workerSubject) (Join-Path $PSScriptRoot 'WorkerOutputChecks.cs')
if ($LASTEXITCODE -ne 0) { throw '출력 배수 경계 검사 컴파일 실패' }
& $workerTests $workerOutputRoot $workerSandbox 2>&1 | Tee-Object -FilePath (Join-Path $workerOutputRoot 'results.log')
$workerExitCode = $LASTEXITCODE
$workerResultPath = Join-Path $workerOutputRoot 'result.json'
if (Test-Path -LiteralPath $workerResultPath) {
    $workerResult = Get-Content -LiteralPath $workerResultPath -Encoding UTF8 -Raw | ConvertFrom-Json
    $workerResult | Add-Member NoteProperty CheckedAt ([DateTimeOffset]::Now.ToString('o'))
    $workerResult | Add-Member NoteProperty InputAssembly $workerInputAssembly
    $workerResult | Add-Member NoteProperty InputHash $workerInputHash
    $workerResult | Add-Member NoteProperty SourceMode (!$AssemblyPath)
    $workerHashes = [ordered]@{}
    foreach ($workerSource in @($workerReaderSource, $workerLauncherSource, $PSCommandPath, (Join-Path $PSScriptRoot 'WorkerOutputChecks.cs'), (Join-Path $PSScriptRoot 'WorkerOutputFixture.cs'))) {
        $workerHashes[$workerSource] = (Get-FileHash -LiteralPath $workerSource -Algorithm SHA256).Hash
    }
    $workerResult | Add-Member NoteProperty SourceHashes $workerHashes
    if ($workerInputAssembly -and (Get-FileHash -LiteralPath $workerInputAssembly -Algorithm SHA256).Hash -ne $workerInputHash) { throw '입력 제품 DLL이 검사 중 변경되었습니다.' }
    $workerResult | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $workerResultPath -Encoding UTF8
}
if ($workerExitCode -ne 0) { throw "출력 배수 검사 실패: $workerOutputRoot" }
Write-Output "문서 worker 출력 배수 결과: $workerResultPath"
