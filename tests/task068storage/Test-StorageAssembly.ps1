#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-StorageAssembly
desc         : 고정 실제 VSIX DLL만 참조해 기존19+13개 저장 검사를 실행하고 DLL·assets·의존성 해시를 보존한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK068 source compile과 분리한 실제 DLL 감사 증거.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$AssemblyPath,
    [Parameter(Mandatory = $true)][string]$AssetsPath,
    [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedAssemblySha256,
    [ValidatePattern('^[a-fA-F0-9]{64}$')][string]$ExpectedAssetsSha256,
    [string]$CompilerPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe',
    [string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRoot = Join-Path $repo 'artifacts\task068storage'
$run = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $outputRoot ('assembly-' + [guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $run) { throw 'Existing storage DLL evidence must not be overwritten.' }
$null = New-Item -ItemType Directory -Path $run
$inputs = New-Object 'Collections.Generic.List[object]'

function Copy-FixedInput([string]$SourcePath, [string]$Destination, [string]$Kind) {
    $resolved = (Resolve-Path -LiteralPath $SourcePath).Path
    $before = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
    if (Test-Path -LiteralPath $Destination) {
        if ((Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash -ne $before) { throw "Conflicting dependency name: $Destination" }
    }
    else { Copy-Item -LiteralPath $resolved -Destination $Destination }
    $copy = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash
    $after = (Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash
    $inputs.Add([pscustomobject]@{ Kind = $Kind; Source = $resolved; Copy = $Destination; Before = $before; Snapshot = $copy; After = $after; Stable = ($before -eq $copy -and $before -eq $after) })
    if ($before -ne $copy -or $before -ne $after) { throw "Input changed during snapshot: $resolved" }
    return $Destination
}

$null = Copy-FixedInput $PSCommandPath (Join-Path $run 'Test-StorageAssembly.ps1') 'Exact invoked driver'
$product = Copy-FixedInput $AssemblyPath (Join-Path $run 'VS2017.AIAssistant.dll') 'Actual product DLL'
$assetsCopy = Copy-FixedInput $AssetsPath (Join-Path $run 'project.assets.json') 'Exact build assets'
$assemblyHash = (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
$assetsHash = (Get-FileHash -LiteralPath $assetsCopy -Algorithm SHA256).Hash
if ($ExpectedAssemblySha256 -and $assemblyHash -ne $ExpectedAssemblySha256) { throw 'Unexpected product DLL hash.' }
if ($ExpectedAssetsSha256 -and $assetsHash -ne $ExpectedAssetsSha256) { throw 'Unexpected assets hash.' }
$assets = Get-Content -LiteralPath $assetsCopy -Raw | ConvertFrom-Json
$packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
foreach ($entry in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($entry.Value.PSObject.Properties.Name -contains 'compile') {
        foreach ($item in $entry.Value.compile.PSObject.Properties.Name) {
            if ($item -like '*.dll') {
                $origin = Join-Path $packageRoot ($assets.libraries.($entry.Name).path + '/' + $item)
                $null = Copy-FixedInput $origin (Join-Path $run ([IO.Path]::GetFileName($item))) 'Exact-assets dependency'
            }
        }
    }
}
$sourceDir = Join-Path $run 'fixture-source'
$referenceDir = Join-Path $run 'compile-reference'
$null = New-Item -ItemType Directory -Path $sourceDir,$referenceDir
foreach ($name in @('StorageProductChecks.cs', 'StorageBoundaryChecks.cs')) {
    $null = Copy-FixedInput (Join-Path $PSScriptRoot $name) (Join-Path $sourceDir $name) 'Unmodified permanent fixture'
}
$referencePackage = $assets.libraries.PSObject.Properties | Where-Object { $_.Name -like 'Microsoft.NETFramework.ReferenceAssemblies.net462/*' } | Select-Object -First 1
if (!$referencePackage) { throw 'Exact assets do not identify the net462 reference framework.' }
$framework = Join-Path $packageRoot ($referencePackage.Value.path + '/build/.NETFramework/v4.6.2')
$compile = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/reference:' + $product))
foreach ($name in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions')) {
    $reference = Copy-FixedInput (Join-Path $framework ($name + '.dll')) (Join-Path $referenceDir ($name + '.dll')) 'Exact-framework compile reference'
    $compile += '/reference:' + $reference
}
$compile += Join-Path $sourceDir 'StorageProductChecks.cs'
$compile += Join-Path $sourceDir 'StorageBoundaryChecks.cs'
$inputs | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $run 'fixed-inputs.json') -Encoding UTF8
$executions = @()
foreach ($entry in @(@{ Main = 'StorageProductChecks'; Result = 'result.json'; Count = 19 }, @{ Main = 'StorageBoundaryChecks'; Result = 'boundary-result.json'; Count = 13 })) {
    $exe = Join-Path $run ($entry.Main + '.exe')
    & $CompilerPath @compile ('/main:' + $entry.Main) ('/out:' + $exe) *> (Join-Path $run ($entry.Main + '-compile.log'))
    if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $run ($entry.Main + '-compile.log')); throw "Actual-DLL fixture compile failed: $run" }
    $fixtureRoot = Join-Path $outputRoot ('a\' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    & $exe $run $fixtureRoot *> (Join-Path $run ($entry.Main + '-results.log'))
    $code = $LASTEXITCODE
    Get-Content (Join-Path $run ($entry.Main + '-results.log'))
    $result = Get-Content -LiteralPath (Join-Path $run $entry.Result) -Raw | ConvertFrom-Json
    $executions += [pscustomobject]@{ Main = $entry.Main; ExitCode = $code; Passed = $result.Passed; Failed = $result.Failed; ActualProductDll = $result.ActualProductDll; LoadedAssembly = $result.LoadedProductAssembly }
    if (!$result.ActualProductDll -or ![String]::Equals($result.LoadedProductAssembly, $product, [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture did not load the exact private DLL.' }
}
$unchanged = $true
foreach ($fixedInput in $inputs) {
    if ((Get-FileHash -LiteralPath $fixedInput.Source -Algorithm SHA256).Hash -ne $fixedInput.Before -or (Get-FileHash -LiteralPath $fixedInput.Copy -Algorithm SHA256).Hash -ne $fixedInput.Snapshot) { $unchanged = $false }
}
$summary = [pscustomobject]@{ ActualProductDll = $true; ProductSourceCompiled = $false; ExpectedAssertions = 32;
    AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path; PrivateAssembly = $product; AssemblySha256 = $assemblyHash; AssetsSha256 = $assetsHash;
    InputsUnchanged = $unchanged; Passed = ($executions | Measure-Object Passed -Sum).Sum; Failed = ($executions | Measure-Object Failed -Sum).Sum;
    Executions = $executions; GuiUsed = $false; SharedBuildUsed = $false; CheckedAt = [DateTimeOffset]::Now.ToString('o') }
$summary | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $run 'assembly-summary.json') -Encoding UTF8
Write-Output "Actual storage DLL evidence: $run"
if (!$unchanged -or $summary.Passed -ne 32 -or $summary.Failed -ne 0 -or @($executions | Where-Object { $_.ExitCode -ne 0 }).Count -gt 0) { throw 'Actual storage DLL checks failed; preserve original evidence.' }
