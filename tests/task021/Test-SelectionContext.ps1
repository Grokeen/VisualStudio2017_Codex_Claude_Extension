#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-SelectionContext
desc         : 선택 스냅샷과 실제 SDK 어댑터 소스 또는 고정 제품 DLL을 독립 컴파일하여 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 공유 bin·obj·개인 문서·IDE를 변경하지 않는 F06 회귀 구성.
update date  : 2026-10-18 CodexCode : 묶음 실행기의 명시적 결과 디렉터리를 지원하고 기본 임의 경로 유지.
update date  : 2026-10-18 CodexCode : 전체 액세스 문서 경계 interface를 실제 SDK 어댑터 독립 컴파일에 포함.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe', [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repositoryDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryDir ('artifacts\sel\' + [guid]::NewGuid().ToString('N').Substring(0, 10)) }
$outputDir = [IO.Path]::GetFullPath($OutputDirectory)
$null = New-Item -ItemType Directory -Path $outputDir -Force
$frameworkDir = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$testExe = Join-Path $outputDir 'SelectionContextChecks.exe'
$compileArguments = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/target:exe', ('/out:' + $testExe))
foreach ($reference in @('mscorlib', 'System', 'System.Core', 'System.Web.Extensions', 'System.Design', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) { $compileArguments += '/reference:' + (Join-Path $frameworkDir ($reference + '.dll')) }
if ($AssemblyPath) {
    $resolvedAssembly = (Resolve-Path -LiteralPath $AssemblyPath).Path
    Copy-Item -LiteralPath $resolvedAssembly -Destination $outputDir
    $compileArguments += '/reference:' + $resolvedAssembly
}
else {
    $projectDir = Join-Path $repositoryDir 'src\VS2017.AIAssistant'
    $assets = Get-Content -LiteralPath (Join-Path $projectDir 'obj\project.assets.json') -Raw | ConvertFrom-Json
    $packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    foreach ($dependency in $assets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
        if ($dependency.Value.compile) {
            foreach ($item in $dependency.Value.compile.PSObject.Properties.Name) {
                if ($item -like '*.dll') { $compileArguments += '/reference:' + (Join-Path $packageRoot ($assets.libraries.($dependency.Name).path + '/' + $item)) }
            }
        }
    }
    foreach ($source in @('Editing\DocumentSnapshot.cs', 'Editing\EditProposal.cs', 'Editing\SourceEditSession.cs', 'Editing\IFullAccessDocumentService.cs', 'Services\VisualStudioDocumentService.cs')) { $compileArguments += Join-Path $projectDir $source }
}
$compileArguments += Join-Path $PSScriptRoot 'SelectionContextChecks.cs'
& $CompilerPath @compileArguments
if ($LASTEXITCODE -ne 0) { throw "선택 영역 검사 컴파일 실패: $LASTEXITCODE" }
& $testExe $outputDir 2>&1 | Tee-Object -FilePath (Join-Path $outputDir 'results.log')
if ($LASTEXITCODE -ne 0) { throw "선택 영역 검사 실패: $LASTEXITCODE ($outputDir)" }
if ($AssemblyPath) { Get-FileHash -LiteralPath (Join-Path $outputDir 'VS2017.AIAssistant.dll') -Algorithm SHA256 }
Write-Output "선택 영역 검사 결과: $outputDir"
