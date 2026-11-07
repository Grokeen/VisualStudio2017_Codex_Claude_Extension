param(
    [Parameter(Mandatory=$true)][string]$CscPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ProductAssemblyPath,
    [string]$ExpectedProductAssemblySha256,
    [string]$MarkdownEditorSourcePath
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "OutputDirectory already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null

$helperSource = Join-Path $repo 'src\VS2017.AIAssistant\Views\MarkdownnEditing.cs'
$fixtureSource = Join-Path $PSScriptRoot 'MarkdownHeadingAssemblyFixture.cs'
$editorSource = if ($MarkdownEditorSourcePath) { (Resolve-Path -LiteralPath $MarkdownEditorSourcePath).Path } else { Join-Path $repo 'src\VS2017.AIAssistant\Views\MarkdownEditor.cs' }
$helperAssembly = Join-Path $output 'MarkdownHeadingHelperProbe.dll'
$fixtureExe = Join-Path $output 'MarkdownHeadingAssemblyFixture.exe'

if ($ProductAssemblyPath) {
    $assemblyPath = (Resolve-Path -LiteralPath $ProductAssemblyPath).Path
    if ([IO.Path]::GetExtension($assemblyPath) -ne '.dll') { throw 'ProductAssemblyPath must name a DLL.' }
    $mode = 'ProductAssembly'
} else {
    $mode = 'PrivateCscHelperProbe'
    & $CscPath /nologo /target:library "/out:$helperAssembly" $helperSource
    if ($LASTEXITCODE -ne 0) { throw "Private helper-probe compile failed: $LASTEXITCODE" }
    $assemblyPath = $helperAssembly
}

$assemblyHashBefore = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
if ($ExpectedProductAssemblySha256 -and $assemblyHashBefore -ne $ExpectedProductAssemblySha256) {
    throw "Product assembly SHA mismatch. Expected $ExpectedProductAssemblySha256, got $assemblyHashBefore"
}
& $CscPath /nologo /target:exe "/out:$fixtureExe" $fixtureSource
if ($LASTEXITCODE -ne 0) { throw "Reflection fixture compile failed: $LASTEXITCODE" }
$runtimeOutput = & $fixtureExe $assemblyPath
if ($LASTEXITCODE -ne 0 -or $runtimeOutput -notmatch '^PASS=20; FAIL=0$') { throw "Assembly helper fixture failed: $runtimeOutput" }

$editor = [IO.File]::ReadAllText($editorSource)
$staticChecks = 0
foreach ($label in @('H1 제목','H2 제목','H3 제목')) {
    if (-not $editor.Contains($label)) { throw "Missing explicit toolbar button: $label" }
    $staticChecks++
}
if ($editor -notmatch 'Editor\.SelectionLength\s*==\s*0') { throw 'Empty-selection/current-line contract is missing.' }
$staticChecks++
if ($editor -notmatch 'private bool ReplaceRange\([^\r\n]*Document\.BeginUpdate\(\)[^\r\n]*Document\.Replace\([^\r\n]*Document\.EndUpdate\(\)') {
    throw 'Single BeginUpdate/Replace/EndUpdate edit contract was not found.'
}
$staticChecks++
if ($editor -notmatch 'MarkdownHeadingEditing\.IsSetextUnderline\(Editor\.Document\.GetText\(first\.Offset, first\.Length\)\)' -or
    $editor -notmatch 'MarkdownHeadingEditing\.IsSetextUnderline\(Editor\.Document\.GetText\(last\.NextLine\.Offset, last\.NextLine\.Length\)\)') {
    throw 'Setext title-range expansion source contract is missing.'
}
$staticChecks++

$assemblyHashAfter = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
if ($assemblyHashAfter -ne $assemblyHashBefore) { throw 'Assembly input changed while being inspected.' }
$total = 20 + $staticChecks
$result = [ordered]@{
    Scope = 'reflected helper method from exact assembly + separate static UI/source assertions'
    Mode = $mode
    ProductAssemblyValidated = ($mode -eq 'ProductAssembly')
    AssemblyPath = $assemblyPath
    AssemblySha256 = $assemblyHashBefore
    ProductAssemblyInputUnchanged = ($assemblyHashBefore -eq $assemblyHashAfter)
    RuntimeHelperPassed = 20
    RuntimeHelperExpected = 20
    StaticSourcePassed = $staticChecks
    StaticSourceExpected = 6
    Passed = $total
    Expected = $total
    Failed = 0
    ProductSdkBuild = $false
    Gui = $false
    AvalonEditUndoVerified = $false
}
$result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
"PASS=$total; FAIL=0; MODE=$mode; HELPER=20; STATIC=$staticChecks"
