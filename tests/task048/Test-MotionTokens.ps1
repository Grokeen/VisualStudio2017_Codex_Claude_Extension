#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-MotionTokens
desc         : TASK048 motion token source is independently compiled and OS-on/off paths are tested on an STA WPF dispatcher.
author       : CodexOrig4
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig4 : inputs and result are bound to SHA256 without touching shared bin/obj.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Run in Windows PowerShell 5.1 STA.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $out) -and @(Get-ChildItem -LiteralPath $out -Force).Count){throw 'Choose a fresh output directory.'}
$null=New-Item -ItemType Directory -Path $out -Force
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$framework='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$sources=@('src/VS2017.AIAssistant/Views/AssistantMotionTokens.cs','tests/task048/MotionChecks.cs')
$argsList=@('/nologo','/target:exe','/langversion:7','/codepage:65001','/warnaserror+',('/out:'+(Join-Path $out 'MotionChecks.exe')))
foreach($assembly in @('System','System.Core','System.Xaml','WindowsBase','PresentationCore','PresentationFramework','System.Web.Extensions')){$argsList+=('/reference:'+(Join-Path $framework ($assembly+'.dll')))}
$inputs=@()
foreach($relative in $sources){$source=Join-Path $repo $relative;$copy=Join-Path $out ([IO.Path]::GetFileName($source));Copy-Item -LiteralPath $source -Destination $copy;$sourceHash=(Get-FileHash $source).Hash;if((Get-FileHash $copy).Hash -ne $sourceHash){throw 'Copied source hash differs.'};$inputs+=[pscustomobject]@{Path=$relative;Sha256=$sourceHash};$argsList+=$copy}
$inputs|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'inputs.json') -Encoding UTF8
& $compiler @argsList 2>&1|Tee-Object -FilePath (Join-Path $out 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Motion contract compilation failed.'}
$exe=Join-Path $out 'MotionChecks.exe';$sha=(Get-FileHash $exe).Hash
& $exe (Join-Path $out 'result.json')
if($LASTEXITCODE -ne 0){throw 'Motion token assertions failed.'}
$result=Get-Content (Join-Path $out 'result.json') -Raw -Encoding UTF8|ConvertFrom-Json
if(!$result.Succeeded){throw 'Motion token result JSON reports failure.'}
[pscustomobject]@{Succeeded=$true;Passed=$result.Passed;Failed=$result.Failed;SourceOnly=$true;ExecutableSha256=$sha;Inputs=$inputs;ClientAreaAnimationsEnabledOnTestHost=$result.ClientAreaAnimationsEnabledOnTestHost;WindowShown=$false}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $out 'summary.json') -Encoding UTF8
