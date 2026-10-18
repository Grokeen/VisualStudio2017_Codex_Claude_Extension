#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-DocumentUi
desc         : 고정 제품 DLL의 감사 후 문서 UI를 새 STA 검사 프로세스로 확인한다.
author       : CodexOrig4
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig4 : 공용 빌드·GUI 예약을 사용하지 않고 실패 JSON과 렌더 결과를 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$SourceOnly)
$ErrorActionPreference='Stop'
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Use a fresh output directory.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$hash=(Get-FileHash -LiteralPath $AssemblyPath).Hash
$product=Join-Path $OutputDirectory 'VS2017.AIAssistant.dll'
Copy-Item -LiteralPath $AssemblyPath -Destination $product
Copy-Item -LiteralPath (Join-Path (Split-Path $AssemblyPath) 'ICSharpCode.AvalonEdit.dll') -Destination $OutputDirectory
$harness=Join-Path $OutputDirectory 'DocumentUiChecks.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DocumentUiChecks.cs') -Destination $harness
Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $OutputDirectory 'Test-DocumentUi.ps1')
@($AssemblyPath,$product,(Join-Path $OutputDirectory 'ICSharpCode.AvalonEdit.dll'),$harness,(Join-Path $OutputDirectory 'Test-DocumentUi.ps1'))|ForEach-Object{
    [pscustomobject]@{Path=$_;Sha256=(Get-FileHash -LiteralPath $_).Hash}
}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'inputs.json') -Encoding UTF8
$framework='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$executable=Join-Path $OutputDirectory 'DocumentUiChecks.exe'
$arguments=@('/nologo','/target:exe','/langversion:7','/codepage:65001','/warnaserror+',('/out:'+$executable))
foreach($reference in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions','System.Drawing','System.Windows.Forms','System.Xml')){$arguments+=('/r:'+(Join-Path $framework ($reference+'.dll')))}
$arguments+=('/r:'+(Join-Path $OutputDirectory 'ICSharpCode.AvalonEdit.dll'))
if($SourceOnly){
    # 제품 테마 BAML/휠 정책은 지정 DLL, 문서 UI·렌더러·팔레트는 별도 동결 소스를 사용한다.
    # 이 결과는 실제 제품 DLL 검사와 구분한다. 공용 bin/obj는 사용하지 않는다.
    $repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $sourceDirectory=Join-Path $OutputDirectory 'source'
    $null=New-Item -ItemType Directory -Path $sourceDirectory
    $sourceHashes=@()
    foreach($name in @('MarkdownAnimatedImage','MarkdownRendering','MarkdownRendering.Html','MarkdownPreview','MarkdownEditor','MarkdownEditor.Documents','MarkdownnEditing','MarkdownnCodeHighlighting','AssistantPalette','AssistantThemePolicy')){
        $inputPath=Join-Path $repo ('src/VS2017.AIAssistant/Views/'+$name+'.cs')
        $copy=Join-Path $sourceDirectory ($name+'.cs')
        Copy-Item -LiteralPath $inputPath -Destination $copy
        $inputHash=(Get-FileHash -LiteralPath $inputPath).Hash
        if((Get-FileHash -LiteralPath $copy).Hash -ne $inputHash){throw 'Source copy hash mismatch.'}
        $sourceHashes+=[pscustomobject]@{Path=$inputPath;Sha256=$inputHash}
        $arguments+=$copy
    }
    $xshd=Join-Path $repo 'src/VS2017.AIAssistant/Assets/MarkdownnHighlighting.xshd'
    Copy-Item -LiteralPath $xshd -Destination $sourceDirectory
    $arguments+=('/resource:'+(Join-Path $sourceDirectory 'MarkdownnHighlighting.xshd')+',VS2017.AIAssistant.MarkdownnHighlighting.xshd')
    $sourceHashes+=[pscustomobject]@{Path=$xshd;Sha256=(Get-FileHash -LiteralPath $xshd).Hash}
    $sourceHashes|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'source-inputs.json') -Encoding UTF8
}else{$arguments+=('/r:'+$product)}
$arguments+=$harness
& $compiler @arguments 2>&1|Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Document UI harness compilation failed.'}
$child=Start-Process -FilePath $executable -ArgumentList ('"'+$OutputDirectory.TrimEnd('\')+'" '+$SourceOnly.IsPresent.ToString().ToLowerInvariant()) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $OutputDirectory 'run.log') -RedirectStandardError (Join-Path $OutputDirectory 'run-error.log')
try{
    if(!$child.WaitForExit(30000)){$child.Kill();throw 'Document UI test timed out.'}
    $child.WaitForExit();$child.Refresh();$code=$child.ExitCode
}finally{$child.Dispose()}
Get-Content -LiteralPath (Join-Path $OutputDirectory 'run.log') -Encoding UTF8
$resultPath=Join-Path $OutputDirectory 'result.json'
if(!(Test-Path -LiteralPath $resultPath)){
    Get-Content -LiteralPath (Join-Path $OutputDirectory 'run-error.log') -Encoding UTF8
    throw 'Document UI result is missing; inspect preserved run-error.log.'
}
$result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8|ConvertFrom-Json
if($result.SourceOnly -ne $SourceOnly.IsPresent -or $result.ThemeAssemblySha256 -ne $hash -or (Get-FileHash -LiteralPath $AssemblyPath).Hash -ne $hash){throw 'Product/theme assembly hash mismatch.'}
if(!$SourceOnly -and $result.ProductAssemblySha256 -ne $hash){throw 'Product assembly hash mismatch.'}
if(!$result.Succeeded -or $result.Failed -gt 0 -or ($null -ne $code -and $code -ne 0)){throw 'Document UI assertions failed; original evidence preserved.'}
