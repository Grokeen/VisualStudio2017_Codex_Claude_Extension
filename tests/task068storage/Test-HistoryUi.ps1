#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-HistoryUi
desc         : 실제 기록 메서드 사본과 제어 reader로 창 표시 없는 WPF 수명·앵커를 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK068 제품 전체 DLL 검사와 추출 메서드 검사를 구분해 기록.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$PythonPath,
      [string]$CompilerPath = 'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe')
$ErrorActionPreference = 'Stop'
if (!$PythonPath) {
    $bundledPython = Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    if (Test-Path -LiteralPath $bundledPython) { $PythonPath = $bundledPython }
    else {
        $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
        if ($pythonCommand) { $PythonPath = $pythonCommand.Source } else { throw 'Pass an installed Python executable with -PythonPath.' }
    }
}
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$output = Join-Path $repo ('artifacts\task068storage\ui-' + [guid]::NewGuid().ToString('N'))
$source = Join-Path $output 'source'
& $PythonPath (Join-Path $PSScriptRoot 'Prepare-HistoryUi.py') $source
if ($LASTEXITCODE -ne 0) { throw 'Actual UI source extraction failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'HistoryUiChecks.cs') -Destination (Join-Path $source 'HistoryUiChecks.cs')
$framework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$exe = Join-Path $output 'HistoryUiChecks.exe'
$compile = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', '/main:HistoryUiChecks', ('/out:' + $exe))
foreach ($name in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) { $compile += '/reference:' + (Join-Path $framework ($name + '.dll')) }
$compile += Get-ChildItem -LiteralPath $source -Filter '*.cs' | ForEach-Object { $_.FullName }
& $CompilerPath @compile *> (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { Get-Content (Join-Path $output 'compile.log'); throw "UI source compile failed: $output" }
$fixtures = Join-Path $repo ('artifacts\task068storage\u\' + [guid]::NewGuid().ToString('N').Substring(0, 8))
& $exe $output $fixtures *> (Join-Path $output 'results.log')
$code = $LASTEXITCODE
Get-Content (Join-Path $output 'results.log')
Write-Output "Actual UI source/no-window evidence: $output"
if ($code -ne 0) { throw "UI source checks failed: $output" }
