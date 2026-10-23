[CmdletBinding()]
param([string]$OutputDirectory, [string]$ReferenceManifest, [string]$AssemblyPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $repo ('artifacts/task072-editor/run-' + [Guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'OutputDirectory already exists; evidence must not be overwritten.' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
[void][IO.Directory]::CreateDirectory($out)
if (-not $ReferenceManifest) { $ReferenceManifest = Join-Path $repo 'artifacts/task072-research/primary-evidence/compile-baseline-3/compile-inputs.json' }
$refs = @((Get-Content -LiteralPath $ReferenceManifest -Raw | ConvertFrom-Json).References)
if (-not @($refs | Where-Object { [IO.Path]::GetFileName($_) -eq 'Microsoft.VisualStudio.Utilities.dll' }).Count) {
    $refs += Join-Path $env:USERPROFILE '.nuget/packages/microsoft.visualstudio.utilities/15.7.27703/lib/net46/Microsoft.VisualStudio.Utilities.dll'
}
$source = Join-Path $repo 'themes/VisualStudio/EditorEnhancements.cs'
$fixture = Join-Path $PSScriptRoot 'EditorEnhancementsContracts.cs'
$hashBefore = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
Copy-Item -LiteralPath $source -Destination (Join-Path $out 'EditorEnhancements.cs')
Copy-Item -LiteralPath $fixture -Destination (Join-Path $out 'EditorEnhancementsContracts.cs')
$referenceRecords = @()
foreach ($ref in $refs) {
    if (-not (Test-Path -LiteralPath $ref -PathType Leaf)) { throw "Reference missing: $ref" }
    $referenceRecords += [ordered]@{ Path=$ref; Sha256=(Get-FileHash -LiteralPath $ref -Algorithm SHA256).Hash }
    if ($ref -notlike '*Reference Assemblies*') { Copy-Item -LiteralPath $ref -Destination $out }
}
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$exe = Join-Path $out 'EditorEnhancementsContracts.exe'
$actualDll = -not [string]::IsNullOrWhiteSpace($AssemblyPath)
$inputHash = $null
if ($actualDll) {
    $AssemblyPath = [IO.Path]::GetFullPath($AssemblyPath)
    $inputHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    $productDll = Join-Path $out ([IO.Path]::GetFileName($AssemblyPath))
    Copy-Item -LiteralPath $AssemblyPath -Destination $productDll
} else {
    $productDll = Join-Path $out 'Grokeen.VisualStudioThemes.dll'
    $productArguments = @('/nologo','/target:library','/warnaserror+',('/out:' + $productDll)) + @($refs | ForEach-Object { '/reference:' + $_ }) + @((Join-Path $out 'EditorEnhancements.cs'))
    & $compiler @productArguments 2>&1 | Set-Content -LiteralPath (Join-Path $out 'product-compile.log') -Encoding UTF8
    if ($LASTEXITCODE -ne 0) { throw 'Product private compilation failed. Raw compiler log is preserved.' }
}
$compilerArguments = @('/nologo','/target:exe','/warnaserror+',('/out:' + $exe)) + @($refs | ForEach-Object { '/reference:' + $_ }) + @((Join-Path $out 'EditorEnhancementsContracts.cs'))
& $compiler @compilerArguments 2>&1 | Set-Content -LiteralPath (Join-Path $out 'compile.log') -Encoding UTF8
$compileExit = $LASTEXITCODE
$runExit = -1
if ($compileExit -eq 0) { & $exe $productDll 2>&1 | Set-Content -LiteralPath (Join-Path $out 'contracts.log') -Encoding UTF8; $runExit=$LASTEXITCODE }
$lines = if (Test-Path -LiteralPath (Join-Path $out 'contracts.log')) { @(Get-Content -LiteralPath (Join-Path $out 'contracts.log')) } else { @() }
$passed = @($lines | Where-Object { $_ -like 'PASS *' }).Count
$failed = @($lines | Where-Object { $_ -like 'FAIL *' }).Count
$same = $hashBefore -eq (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -and $hashBefore -eq (Get-FileHash -LiteralPath (Join-Path $out 'EditorEnhancements.cs') -Algorithm SHA256).Hash
$productHash=(Get-FileHash -LiteralPath $productDll -Algorithm SHA256).Hash
$assemblyUnchanged= -not $actualDll -or ($inputHash -eq $productHash -and $inputHash -eq (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash)
$loadedExactCopy = @($lines | Where-Object { $_ -eq ('PRODUCT ' + $productDll) }).Count -eq 1
$result = [ordered]@{ Succeeded=($compileExit -eq 0 -and $runExit -eq 0 -and $failed -eq 0 -and $passed -eq 125 -and $same -and $assemblyUnchanged -and $loadedExactCopy); CompileExit=$compileExit; RunExit=$runExit; Passed=$passed; Failed=$failed; ExpectedAssertions=125; SourceUnchanged=$same; SourceSha256=$hashBefore; FixtureSha256=(Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash; References=$referenceRecords; ReferenceManifest=[IO.Path]::GetFullPath($ReferenceManifest); ReferenceManifestSha256=(Get-FileHash -LiteralPath $ReferenceManifest -Algorithm SHA256).Hash; ActualProductDll=$actualDll; AssemblyPath=$AssemblyPath; ProductSha256=$productHash; AssemblyUnchanged=$assemblyUnchanged; LoadedExactCopy=$loadedExactCopy; ActualIde=$false; ActualRoslyn=$false; SourceCompiled=(-not $actualDll); WindowsShown=0; NativeInput=0; RemoteAI=0 }
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $out 'result.json') -Encoding UTF8
[pscustomobject]$result | Select-Object Succeeded,CompileExit,RunExit,Passed,Failed,SourceUnchanged | Format-List
Write-Output $out
if (-not $result.Succeeded) { exit 1 }
