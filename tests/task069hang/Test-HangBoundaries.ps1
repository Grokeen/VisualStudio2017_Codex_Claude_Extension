#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-HangBoundaries
desc         : 협업 잠금·불필요 쓰기와 정확한 표시 복구 메서드를 무창 개인 사본에서 검증한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK069 source와 실제 DLL 검사를 분리하고 이전 실패 증거 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$AssetsPath, [string]$ProductSourceRoot, [string]$OutputDirectory,
    [string]$PythonPath = 'C:\Users\ygree\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$run = if($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repo ('artifacts\task069hang\run-' + [guid]::NewGuid().ToString('N')) }
if(Test-Path -LiteralPath $run) { throw 'Previous evidence cannot be overwritten.' }
New-Item -ItemType Directory -Path $run | Out-Null
$fixedInputs = @()
foreach($inputPath in @($PSCommandPath, (Join-Path $PSScriptRoot 'Prepare-Hang.py'))) {
    $hash = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
    $privateCopy = Join-Path $run ([IO.Path]::GetFileName($inputPath))
    Copy-Item -LiteralPath $inputPath -Destination $privateCopy
    if((Get-FileHash -LiteralPath $privateCopy -Algorithm SHA256).Hash -ne $hash) { throw 'Driver copy mismatch.' }
    $fixedInputs += [pscustomobject]@{ Path=$inputPath; Copy=$privateCopy; Sha256=$hash }
}
$source = Join-Path $run 'source'
if(!$ProductSourceRoot) { $ProductSourceRoot = $repo }
& $PythonPath (Join-Path $PSScriptRoot 'Prepare-Hang.py') $source $ProductSourceRoot
if($LASTEXITCODE -ne 0) { throw 'Input snapshot failed.' }
if($AssetsPath) {
    $resolvedAssets = (Resolve-Path -LiteralPath $AssetsPath).Path
    $assetsHash = (Get-FileHash -LiteralPath $resolvedAssets -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $resolvedAssets -Destination (Join-Path $run 'project.assets.json')
    if((Get-FileHash -LiteralPath (Join-Path $run 'project.assets.json') -Algorithm SHA256).Hash -ne $assetsHash -or (Get-FileHash -LiteralPath $resolvedAssets -Algorithm SHA256).Hash -ne $assetsHash) { throw 'Assets input changed.' }
    @{ Path=$resolvedAssets; Sha256=$assetsHash; ProductSourceRoot=$ProductSourceRoot } | ConvertTo-Json | Set-Content (Join-Path $run 'assets-input.json') -Encoding UTF8
}
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$referenceDirectory = Join-Path $run 'compile-reference'
New-Item -ItemType Directory -Path $referenceDirectory | Out-Null
$arguments = @('/nologo','/noconfig','/nostdlib+','/langversion:7.0','/target:exe',('/out:' + (Join-Path $run 'HangChecks.exe')))
foreach($name in @('mscorlib','System','System.Core','System.Web.Extensions','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')) {
    $origin = Join-Path $framework ($name + '.dll')
    $referenceCopy = Join-Path $referenceDirectory ($name + '.dll')
    $referenceHash = (Get-FileHash -LiteralPath $origin -Algorithm SHA256).Hash
    Copy-Item -LiteralPath $origin -Destination $referenceCopy
    $fixedInputs += [pscustomobject]@{ Path=$origin; Copy=$referenceCopy; Sha256=$referenceHash }
    $arguments += '/reference:' + $referenceCopy
}
if($AssemblyPath) {
    $resolved = (Resolve-Path -LiteralPath $AssemblyPath).Path
    $before = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
    $copy = Join-Path $run 'VS2017.AIAssistant.dll'
    Copy-Item -LiteralPath $resolved -Destination $copy
    if((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $before -or (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash -ne $before) { throw 'Product DLL changed.' }
    @{ Path=$resolved; Sha256=$before; ActualProductDll=$true } | ConvertTo-Json | Set-Content (Join-Path $run 'assembly-input.json') -Encoding UTF8
    $arguments += '/reference:' + $copy
} else {
    foreach($name in @('MarkdownWorkspaceStore.cs','MarkdownWorkspaceStore.Committed.cs','ConversationCollaboration.cs','ChatMessage.actual.cs')) { $arguments += Join-Path $source $name }
}
$arguments += Join-Path $source 'AgentUi.actual.cs'
$arguments += Join-Path $source 'HangChecks.cs'
foreach($compileSource in (Get-ChildItem -LiteralPath $source -Filter '*.cs' -File)) {
    $fixedInputs += [pscustomobject]@{ Path=$compileSource.FullName; Copy=$compileSource.FullName; Sha256=(Get-FileHash -LiteralPath $compileSource.FullName -Algorithm SHA256).Hash }
}
& 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe' @arguments *> (Join-Path $run 'compile.log')
if($LASTEXITCODE -ne 0) { Get-Content (Join-Path $run 'compile.log'); throw 'Private fixture compile failed.' }
& (Join-Path $run 'HangChecks.exe') $run *> (Join-Path $run 'results.log')
$code = $LASTEXITCODE
Get-Content (Join-Path $run 'results.log')
$result = Get-Content (Join-Path $run 'result.json') -Raw | ConvertFrom-Json
$unchanged = $true
foreach($inputFile in $fixedInputs) {
    if((Get-FileHash -LiteralPath $inputFile.Path -Algorithm SHA256).Hash -ne $inputFile.Sha256 -or (Get-FileHash -LiteralPath $inputFile.Copy -Algorithm SHA256).Hash -ne $inputFile.Sha256) { $unchanged=$false }
}
foreach($sourceFile in (Get-Content (Join-Path $source 'source-hashes.json') -Raw | ConvertFrom-Json)) {
    $originalCopy = Join-Path $source ([IO.Path]::GetFileName($sourceFile.Path) + '.original')
    if((Get-FileHash -LiteralPath $sourceFile.Path -Algorithm SHA256).Hash -ne $sourceFile.Sha256 -or (Get-FileHash -LiteralPath $originalCopy -Algorithm SHA256).Hash -ne $sourceFile.Sha256) { $unchanged=$false }
}
if($AssemblyPath) {
    if((Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash -ne $before -or (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $before) { $unchanged=$false }
    if(!$result.ActualProductDll -or ![String]::Equals($result.Assembly,$copy,[StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture did not load the fixed actual product DLL.' }
}
if($AssetsPath -and ((Get-FileHash -LiteralPath $resolvedAssets -Algorithm SHA256).Hash -ne $assetsHash -or (Get-FileHash -LiteralPath (Join-Path $run 'project.assets.json') -Algorithm SHA256).Hash -ne $assetsHash)) { $unchanged=$false }
$fixedInputs | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $run 'fixed-driver-framework-inputs.json') -Encoding UTF8
[pscustomobject]@{ Passed=$result.Passed; Failed=$result.Failed; ExpectedAssertions=17; ActualProductDll=$result.ActualProductDll; ExactUiMethodSubstrings=$result.ExactUiMethodSubstrings; FullProductUiTest=$false; InputsUnchanged=$unchanged; GuiUsed=$false; SharedBuildUsed=$false; IncidentCauseProved=$false; CheckedAt=[DateTimeOffset]::Now.ToString('o') } | ConvertTo-Json | Set-Content (Join-Path $run 'hang-summary.json') -Encoding UTF8
Write-Output "Evidence: $run"
if($code -ne 0 -or $result.Passed -ne 17 -or $result.Failed -ne 0 -or !$unchanged) { throw 'Hang boundary checks failed or input changed.' }
