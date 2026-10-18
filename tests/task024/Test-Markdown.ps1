[CmdletBinding(DefaultParameterSetName='Actual')]
param(
    [Parameter(Mandatory=$true,ParameterSetName='Actual')][string]$AssemblyPath,
    [Parameter(Mandatory=$true,ParameterSetName='Source')][switch]$SourceOnly,
    [string]$OutputDirectory
)
# ---------------------------------------------------------------------
# name         : Test-Markdown
# desc         : 고정 제품 DLL 또는 명시한 source-only 공통 Markdown 회귀를 별도 STA 실행 파일로 검증한다.
# author       : CodexCode
# create date  : 2026-10-19
# update date  : 2026-10-19 CodexCode : 창·네트워크·계정 사용 없이 실제 WPF 객체 및 UTF-8 fixture만 사용.
#                2026-10-19 CodexCode : SC024-01 악성 입력 회귀는 소유 자식 실행 파일을 부모10초 한도로 중단.
# ---------------------------------------------------------------------
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -lt 1) { throw 'Use Windows PowerShell 5.1.' }
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run this test in Windows PowerShell 5.1 -NoProfile -STA.' }
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $repo ('artifacts/task024/markdown-'+[Guid]::NewGuid().ToString('N').Substring(0,10)) }
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { if (@(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) { throw 'OutputDirectory must be fresh or empty.' } }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$inputDirectory=Join-Path $output 'input'
New-Item -ItemType Directory -Path $inputDirectory | Out-Null
$savedEnvironment=@{}
foreach ($key in @('VS2017_AI_WORKSPACE_PATH','VS2017_AI_SETTINGS_PATH','VS2017_AI_DISABLE_CLI_DISCOVERY')) { $savedEnvironment[$key]=[Environment]::GetEnvironmentVariable($key,'Process') }
try {
$env:VS2017_AI_WORKSPACE_PATH=Join-Path $output 'workspace'
$env:VS2017_AI_SETTINGS_PATH=Join-Path $output 'settings.json'
$env:VS2017_AI_DISABLE_CLI_DISCOVERY='1'
$refs='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$csc='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$avalon='C:/Users/ygree/.nuget/packages/avalonedit/6.3.1.120/lib/net462/ICSharpCode.AvalonEdit.dll'
if (!$SourceOnly) {
    $original=[IO.Path]::GetFullPath($AssemblyPath)
    if (!(Test-Path -LiteralPath $original -PathType Leaf)) { throw 'AssemblyPath does not exist.' }
    $expected=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
    $product=Join-Path $inputDirectory ([IO.Path]::GetFileName($original))
    Copy-Item -LiteralPath $original -Destination $product
    if ((Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash -ne $expected) { throw 'Fixed assembly copy hash differs.' }
    $sibling=Join-Path ([IO.Path]::GetDirectoryName($original)) 'ICSharpCode.AvalonEdit.dll'
    if (!(Test-Path -LiteralPath $sibling -PathType Leaf)) { throw 'Actual product must include its AvalonEdit dependency.' }
    $avalon=$sibling
}
Copy-Item -LiteralPath $avalon -Destination (Join-Path $output 'ICSharpCode.AvalonEdit.dll')
if ((Get-FileHash -LiteralPath $avalon -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $output 'ICSharpCode.AvalonEdit.dll') -Algorithm SHA256).Hash) { throw 'AvalonEdit dependency hash differs.' }
$arguments=@('/nologo','/langversion:7','/target:exe',('/out:'+(Join-Path $output 'MarkdownChecks.exe')))
foreach ($reference in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions','System.Windows.Forms','System.Xml')) { $arguments+=('/r:'+(Join-Path $refs ($reference+'.dll'))) }
$arguments+=('/r:'+ (Join-Path $output 'ICSharpCode.AvalonEdit.dll'))
if ($SourceOnly) {
    foreach ($source in @('MarkdownAnimatedImage','MarkdownRendering','MarkdownRendering.Html','MarkdownPreview','MarkdownEditor','MarkdownEditor.Documents','MarkdownnEditing','MarkdownnCodeHighlighting')) { $arguments+=(Join-Path $repo ('src/VS2017.AIAssistant/Views/'+$source+'.cs')) }
    $arguments+=('/resource:'+(Join-Path $repo 'src/VS2017.AIAssistant/Assets/MarkdownnHighlighting.xshd')+',VS2017.AIAssistant.MarkdownnHighlighting.xshd')
} else {
    Copy-Item -LiteralPath $product -Destination (Join-Path $output ([IO.Path]::GetFileName($product)))
    $arguments+=('/r:'+ $product)
}
$arguments+=(Join-Path $PSScriptRoot 'MarkdownControlsChecks.cs')
& $csc @arguments 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Markdown check harness compilation failed.' }
$runLog=Join-Path $output 'run.log'
$errorLog=Join-Path $output 'run-error.log'
$childArguments='"'+$output.TrimEnd('\')+'" '+$SourceOnly.IsPresent.ToString().ToLowerInvariant()
$child=Start-Process -FilePath (Join-Path $output 'MarkdownChecks.exe') -ArgumentList $childArguments -WindowStyle Hidden -RedirectStandardOutput $runLog -RedirectStandardError $errorLog -PassThru
try {
    if (!$child.WaitForExit(10000)) { $child.Kill(); $null=$child.WaitForExit(2000); throw 'Markdown parser checks exceeded the parent 10-second execution limit.' }
    $child.WaitForExit();$child.Refresh()
    $testExit=$child.ExitCode
} finally { $child.Dispose() }
Get-Content -LiteralPath $runLog -Encoding UTF8 | Write-Output
$result=Get-Content -LiteralPath (Join-Path $output 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (!$SourceOnly -and ($result.SourceOnly -or $result.ProductAssemblySha256 -ne $expected)) { throw 'Result does not describe the requested fixed assembly.' }
if (!$result.Succeeded -or [int]$result.Failed -gt 0 -or ($null -ne $testExit -and $testExit -ne 0)) { throw 'Markdown assertions failed; preserve the original result.' }
Write-Output (Join-Path $output 'result.json')
} finally {
    foreach ($key in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($key,$savedEnvironment[$key],'Process') }
}
exit $testExit
