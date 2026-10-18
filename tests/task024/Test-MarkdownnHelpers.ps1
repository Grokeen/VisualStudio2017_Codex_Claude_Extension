#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-MarkdownnHelpers
desc         : 이식 helper 두 source와 원본 XSHD를 .NET462 독립 compile하여 창 없이 검사한다.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : 기존 제품·원본·빌드 출력 수정 없이 source-only 증거를 분리한다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Windows PowerShell 5.1 -NoProfile -STA로 실행하세요.'}
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repositoryDir ('artifacts/task024/markdownn-helper-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -gt 0){throw '원래 증거를 보존합니다. 새 결과 디렉터리를 지정하세요.'}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$avalonSource=Join-Path $env:USERPROFILE '.nuget/packages/avalonedit/6.3.1.120/lib/net462/ICSharpCode.AvalonEdit.dll'
$avalon=Join-Path $OutputDirectory 'ICSharpCode.AvalonEdit.dll';Copy-Item -LiteralPath $avalonSource -Destination $avalon
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$referenceDir='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$library=Join-Path $OutputDirectory 'MarkdownnHelperSourceChecks.dll'
$sources=@((Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/MarkdownnEditing.cs'),(Join-Path $repositoryDir 'src/VS2017.AIAssistant/Views/MarkdownnCodeHighlighting.cs'),(Join-Path $PSScriptRoot 'MarkdownnHelperChecks.cs'))
$xshd=Join-Path $repositoryDir 'src/VS2017.AIAssistant/Assets/MarkdownnHighlighting.xshd'
$argsList=@('/nologo','/target:library','/langversion:7','/codepage:65001','/nostdlib+',('/out:'+$library),('/lib:'+$referenceDir),'/reference:mscorlib.dll','/reference:System.dll','/reference:System.Core.dll','/reference:System.Xml.dll','/reference:WindowsBase.dll','/reference:PresentationCore.dll','/reference:PresentationFramework.dll','/reference:System.Xaml.dll',('/reference:'+$avalon),('/resource:'+$xshd+',VS2017.AIAssistant.MarkdownnHighlighting.xshd'))+$sources
& $compiler @argsList
if($LASTEXITCODE -ne 0){throw 'Markdownn helper 독립 compile 실패'}
foreach($framework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $framework}
[Reflection.Assembly]::LoadFrom($avalon)|Out-Null
[Reflection.Assembly]::LoadFrom($library)|Out-Null
[MarkdownnHelperChecks]::Run()
$result=[pscustomobject]@{Succeeded=[MarkdownnHelperChecks]::Errors.Count -eq 0;Passed=[MarkdownnHelperChecks]::Checks.Count;Failed=[MarkdownnHelperChecks]::Errors.Count;Checks=@([MarkdownnHelperChecks]::Checks);Errors=@([MarkdownnHelperChecks]::Errors);SourceOnly=$true;AssemblySha256=(Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash;AvalonEditSha256=(Get-FileHash -LiteralPath $avalon -Algorithm SHA256).Hash;SourceInputs=@(($sources+@($xshd))|ForEach-Object {Get-FileHash -LiteralPath $_ -Algorithm SHA256|Select-Object Path,Hash});WindowShown=$false;NativePointerUsed=$false;SharedBuildPerformed=$false;ExternalOriginalEdited=$false;NetworkRequested=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
$result|ConvertTo-Json -Depth 5
if(!$result.Succeeded){throw 'Markdownn helper 검사 실패; 원본 결과를 보존했습니다.'}
