#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-DocumentAttachments
desc         : 문서 서비스·읽기 도우미를 별도 출력에서 컴파일하고 실제 Windows 필터와 문서 경계를 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 제품 빌드 출력·사용자 문서·등록 필터를 변경하지 않는 독립 검증.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$WorkerPath, [ValidateSet('AnyCPU','x86')][string]$Platform='AnyCPU')
$ErrorActionPreference='Stop'
$documentRepository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$documentOutput=Join-Path $documentRepository ('artifacts\dr\'+[guid]::NewGuid().ToString('N').Substring(0,10))
$null=New-Item -ItemType Directory -Path $documentOutput -Force
$documentCompiler='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
$documentFramework=Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$references=@('/nologo','/noconfig','/nostdlib+','/langversion:7.0',('/platform:'+$Platform))
foreach($name in @('mscorlib','System','System.Core','System.Xml','System.IO.Compression','System.IO.Compression.FileSystem')) { $references+='/reference:'+(Join-Path $documentFramework ($name+'.dll')) }
$workerDestination=Join-Path $documentOutput 'VS2017.AIAssistant.DocumentReader.exe'
if($WorkerPath) {
    $resolvedWorker=(Resolve-Path -LiteralPath $WorkerPath).Path
    Copy-Item -LiteralPath $resolvedWorker -Destination $workerDestination
    if(Test-Path -LiteralPath ($resolvedWorker+'.config')) { Copy-Item -LiteralPath ($resolvedWorker+'.config') -Destination ($workerDestination+'.config') }
}
else {
    & $documentCompiler @references /target:exe ('/out:'+$workerDestination) (Join-Path $documentRepository 'src\VS2017.AIAssistant\Chat\DocumentFilterWorker.cs')
    if($LASTEXITCODE -ne 0){throw '문서 도우미 컴파일 실패'}
}
$testExecutable=Join-Path $documentOutput 'DocumentAttachmentTests.exe'
$testArguments=@($references)+@('/target:exe',('/out:'+$testExecutable))
if($AssemblyPath) {
    $documentAssembly=(Resolve-Path -LiteralPath $AssemblyPath).Path; Copy-Item -LiteralPath $documentAssembly -Destination $documentOutput
    $testArguments+='/reference:'+$documentAssembly
} else {
    $testArguments+=Join-Path $documentRepository 'src\VS2017.AIAssistant\Chat\DocumentAttachmentReader.cs'
    $testArguments+=Join-Path $documentRepository 'src\VS2017.AIAssistant\Chat\DocumentWorkerLauncher.cs'
}
$testArguments+=Join-Path $PSScriptRoot 'DocumentAttachmentTests.cs'
& $documentCompiler @testArguments
if($LASTEXITCODE -ne 0){throw '문서 추출 테스트 컴파일 실패'}
$mockWorker=Join-Path $documentOutput 'DocumentWorkerFixture.exe'
& $documentCompiler @references /target:exe ('/out:'+$mockWorker) (Join-Path $PSScriptRoot 'DocumentWorkerFixture.cs')
if($LASTEXITCODE -ne 0){throw '문서 경계 fixture 컴파일 실패'}
& $testExecutable $documentOutput (Join-Path $PSScriptRoot 'fixtures') $mockWorker 2>&1 | Tee-Object -FilePath (Join-Path $documentOutput 'results.log')
if($LASTEXITCODE -ne 0){throw "문서 첨부 검사 실패: $documentOutput"}
$documentCount=[regex]::Match([IO.File]::ReadAllText((Join-Path $documentOutput 'results.log')),'PASS (\d+) document attachment checks').Groups[1].Value
$documentEvidence=[pscustomobject]@{CheckedAt=[DateTimeOffset]::Now.ToString('o');Passed=[int]$documentCount;Total=[int]$documentCount;Platform=$Platform;ProductAssembly=$AssemblyPath;ProductHash=if($AssemblyPath){(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash}else{'source-compiled'};WorkerSource=if($WorkerPath){$resolvedWorker}else{'source-compiled'};WorkerArchitecture=[Reflection.AssemblyName]::GetAssemblyName($workerDestination).ProcessorArchitecture.ToString();WorkerHash=(Get-FileHash -LiteralPath $workerDestination -Algorithm SHA256).Hash;OutputDirectory=$documentOutput}
$documentEvidence|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $documentOutput 'evidence.json') -Encoding UTF8
Write-Output "문서 첨부 검사 결과: $documentOutput"
