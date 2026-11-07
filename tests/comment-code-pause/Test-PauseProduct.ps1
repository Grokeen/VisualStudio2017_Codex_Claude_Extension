[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$ProductAssemblyPath,
    [Parameter(Mandatory = $true)][string]$ExpectedProductSha256,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$CscPath = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$product = [IO.Path]::GetFullPath($ProductAssemblyPath)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-Path -LiteralPath $product -PathType Leaf) -or (Test-Path -LiteralPath $output)) { throw 'A product DLL and a new output directory are required.' }
for ($part = $output; $part; $part = [IO.Path]::GetDirectoryName($part)) {
    if ((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Output reparse paths are not accepted.' }
}
$productHash = (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
if ($productHash -ine $ExpectedProductSha256) { throw 'Product DLL SHA256 differs from the expected input.' }
[void][IO.Directory]::CreateDirectory($output)
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$copy = Join-Path $output 'VS2017.AIAssistant.dll'
Copy-Item -LiteralPath $product -Destination $copy
if ((Get-FileHash -LiteralPath $copy).Hash -ne $productHash) { throw 'Product copy differs.' }
$sources = @((Join-Path $PSScriptRoot 'PauseProductContracts.cs'), (Join-Path $repo 'tests/comment-code-preview-poc/FakeEditorFixture.cs'))
$hashes = @($sources | ForEach-Object { [pscustomobject]@{Path=$_; SHA256=(Get-FileHash -LiteralPath $_).Hash} })
$exe = Join-Path $output 'PauseProductContracts.exe'
$compilerOutput = @(& $CscPath /nologo /warnaserror+ /langversion:5 /codepage:65001 /target:exe "/reference:$copy" "/out:$exe" $sources 2>&1)
$compilerExit = $LASTEXITCODE
$compilerOutput | Set-Content -LiteralPath (Join-Path $output 'compile.log') -Encoding UTF8
if ($compilerExit -ne 0) { throw "Test helper compilation failed (exit $compilerExit)." }
$runOutput = @(& $exe 2>&1)
$runExit = $LASTEXITCODE
$runOutput | Set-Content -LiteralPath (Join-Path $output 'run.log') -Encoding UTF8
$stable = (Get-FileHash -LiteralPath $product).Hash -eq $productHash -and (Get-FileHash -LiteralPath $copy).Hash -eq $productHash
foreach ($entry in $hashes) { if ((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.SHA256) { $stable = $false } }
$result = [ordered]@{
    Succeeded = ($compilerExit -eq 0 -and $runExit -eq 0 -and $stable -and ($runOutput -join "`n") -match 'PASS paused product provider:')
    ProductAssemblyPath = $product; ProductSha256 = $productHash; TestInputs = $hashes
    CompileExit = $compilerExit; RunExit = $runExit; InputUnchanged = $stable
    ActualProductDll = $true; FakeEditor = $true; FakeProvider = $true; ActualIDE = $false; ActualAI = $false; ActualMenu = $false
    Output = @($runOutput | ForEach-Object { $_.ToString() })
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
$result | ConvertTo-Json -Depth 5
if (!$result.Succeeded) { exit 1 }
