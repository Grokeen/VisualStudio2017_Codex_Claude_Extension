#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-MarkdownLinks
desc         : 고정 DLL 또는 명시한 공통 소스의 Role #6 실제 WPF 렌더 검사를 격리 실행한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : DLL 해시·source-only·20초 자식 실행 한도와 원본 결과 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding(DefaultParameterSetName='Actual')]
param(
    [Parameter(Mandatory=$true,ParameterSetName='Actual')][string]$AssemblyPath,
    [Parameter(Mandatory=$true,ParameterSetName='Source')][switch]$SourceOnly,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -ne 5){throw 'Use Windows PowerShell 5.1 -NoProfile -STA.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $output) -and @(Get-ChildItem -LiteralPath $output -Force).Count){throw 'Preserve previous evidence. Use a new output directory.'}
$null=New-Item -ItemType Directory -Path $output -Force
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$refs='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$arguments=@('/nologo','/langversion:7','/target:exe',('/out:'+(Join-Path $output 'MarkdownLinkChecks.exe')))
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions','System.Windows.Forms','System.Xml')){$arguments+=('/r:'+(Join-Path $refs ($name+'.dll')))}
$inputs=@()
$avalon='C:/Users/ygree/.nuget/packages/avalonedit/6.3.1.120/lib/net462/ICSharpCode.AvalonEdit.dll'
if(!$SourceOnly){$sibling=Join-Path (Split-Path ([IO.Path]::GetFullPath($AssemblyPath)) -Parent) 'ICSharpCode.AvalonEdit.dll';if(Test-Path -LiteralPath $sibling){$avalon=$sibling}}
Copy-Item -LiteralPath $avalon -Destination (Join-Path $output 'ICSharpCode.AvalonEdit.dll')
$arguments+=('/r:'+(Join-Path $output 'ICSharpCode.AvalonEdit.dll'))
if($SourceOnly){
    foreach($name in @('MarkdownPreview','MarkdownRendering','MarkdownRendering.Html','MarkdownAnimatedImage','MarkdownnCodeHighlighting')){
        $file=Join-Path $repo ('src/VS2017.AIAssistant/Views/'+$name+'.cs');$arguments+=$file
        $inputs+=[pscustomobject]@{Path=$file;Sha256=(Get-FileHash -LiteralPath $file).Hash}
    }
    $arguments+=('/resource:'+(Join-Path $repo 'src/VS2017.AIAssistant/Assets/MarkdownnHighlighting.xshd')+',VS2017.AIAssistant.MarkdownnHighlighting.xshd')
}else{
    $fixed=[IO.Path]::GetFullPath($AssemblyPath)
    $expected=(Get-FileHash -LiteralPath $fixed).Hash
    $copy=Join-Path $output ([IO.Path]::GetFileName($fixed));Copy-Item -LiteralPath $fixed -Destination $copy
    if((Get-FileHash -LiteralPath $copy).Hash -ne $expected){throw 'Fixed DLL copy differs.'}
    $arguments+=('/r:'+$copy)
    $inputs+=[pscustomobject]@{Path=$fixed;Sha256=$expected}
}
$arguments+=(Join-Path $PSScriptRoot 'MarkdownLinkChecks.cs')
& $compiler @arguments 2>&1 | Tee-Object -FilePath (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Markdown link fixture compilation failed.'}
$inputs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'inputs.json') -Encoding UTF8
$run=Join-Path $output 'run.log';$err=Join-Path $output 'stderr.log'
$child=Start-Process -FilePath (Join-Path $output 'MarkdownLinkChecks.exe') -ArgumentList ('"'+$output.TrimEnd('\')+'"') -WindowStyle Hidden -RedirectStandardOutput $run -RedirectStandardError $err -PassThru
try{$null=$child.Handle;if(!$child.WaitForExit(20000)){$child.Kill();$child.WaitForExit();throw 'Markdown link fixture timed out.'};$child.WaitForExit();$code=$child.ExitCode}finally{$child.Dispose()}
Get-Content -LiteralPath $run -Encoding UTF8
Get-Content -LiteralPath $err -Encoding UTF8
$result=Get-Content -LiteralPath (Join-Path $output 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($null -eq $code){throw 'Child exit code was not collected. Preserve the result and fix the driver.'}
if(!$result.Succeeded -or $code -ne 0){throw 'Markdown link checks failed. Original evidence preserved.'}
foreach($entry in $inputs){if((Get-FileHash -LiteralPath $entry.Path).Hash -ne $entry.Sha256){throw 'Input changed during test.'}}
Write-Output ('PASS '+$result.Passed+' / SourceOnly='+$SourceOnly.IsPresent+' / '+(Join-Path $output 'result.json'))
