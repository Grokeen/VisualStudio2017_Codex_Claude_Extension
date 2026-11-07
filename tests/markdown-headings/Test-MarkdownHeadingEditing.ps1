param(
    [Parameter(Mandatory=$true)][string]$CscPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "OutputDirectory already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null

$helper = Join-Path $repo 'src\VS2017.AIAssistant\Views\MarkdownnEditing.cs'
$fixture = Join-Path $PSScriptRoot 'MarkdownHeadingFixture.cs'
$exe = Join-Path $output 'MarkdownHeadingFixture.exe'
& $CscPath /nologo /target:exe "/out:$exe" $helper $fixture
if ($LASTEXITCODE -ne 0) { throw "Private helper fixture compile failed: $LASTEXITCODE" }
$fixtureOutput = & $exe
if ($LASTEXITCODE -ne 0 -or $fixtureOutput -notmatch '^PASS=20; FAIL=0$') { throw "Fixture failed: $fixtureOutput" }

$editorPath = Join-Path $repo 'src\VS2017.AIAssistant\Views\MarkdownEditor.cs'
$editor = [IO.File]::ReadAllText($editorPath)
$checks = 20
foreach ($label in @('H1 제목','H2 제목','H3 제목')) {
    if (-not $editor.Contains($label)) { throw "Missing explicit toolbar button: $label" }
    $checks++
}
if ($editor -notmatch 'Editor\.SelectionLength\s*==\s*0') { throw 'Empty-selection/current-line contract is missing.' }
$checks++
if ($editor -notmatch 'private bool ReplaceRange\([^\r\n]*Document\.BeginUpdate\(\)[^\r\n]*Document\.Replace\([^\r\n]*Document\.EndUpdate\(\)') {
    throw 'Single BeginUpdate/Replace/EndUpdate edit contract was not found.'
}
$checks++
if ($editor -notmatch 'MarkdownHeadingEditing\.IsSetextUnderline\(Editor\.Document\.GetText\(first\.Offset, first\.Length\)\)' -or
    $editor -notmatch 'MarkdownHeadingEditing\.IsSetextUnderline\(Editor\.Document\.GetText\(last\.NextLine\.Offset, last\.NextLine\.Length\)\)') {
    throw 'Setext title-range expansion source contract is missing.'
}
$checks++

$result = [ordered]@{
    Scope = 'private helper fixture + source contract; no product SDK build or GUI'
    Passed = $checks
    Expected = $checks
    Failed = 0
    Fixture = $fixtureOutput
    ProductBuild = $false
    Gui = $false
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
"PASS=$checks; FAIL=0"
