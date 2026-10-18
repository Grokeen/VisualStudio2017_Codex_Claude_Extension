#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-MarkdownAuditFollowup
desc         : Freeze one assembly and run independent R3-03 file-failure and inert-preview checks.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : Explicit source-fixture mode never counts as product validation.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$OutputDirectory,[switch]$SourceFixture)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop'){throw 'Use Windows PowerShell 5.1.'}
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$identity=[Reflection.AssemblyName]::GetAssemblyName($AssemblyPath)
if(!$SourceFixture -and $identity.Name -ne 'VS2017.AIAssistant'){throw 'Actual mode requires the product assembly.'}
if($SourceFixture -and $identity.Name -eq 'VS2017.AIAssistant'){throw 'Product assemblies must use actual mode.'}
$inputRoot=[IO.Path]::GetDirectoryName($AssemblyPath)
if($OutputDirectory -eq $inputRoot -or $OutputDirectory.StartsWith($inputRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be outside the input directory.'}
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count -ne 0){throw 'Existing evidence is preserved. Select a fresh output directory.'}
for($checkedPath=$AssemblyPath;$checkedPath;$checkedPath=[IO.Path]::GetDirectoryName($checkedPath)){if(((Get-Item -LiteralPath $checkedPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked input paths are not accepted.'}}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
$copied=@()
foreach($inputPath in @($AssemblyPath,(Join-Path $inputRoot 'ICSharpCode.AvalonEdit.dll'))){
 if(!(Test-Path -LiteralPath $inputPath -PathType Leaf)){throw 'AvalonEdit and the selected assembly are required.'}
 if(((Get-Item -LiteralPath $inputPath).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Linked assembly inputs are not accepted.'}
 $copy=Join-Path $OutputDirectory ([IO.Path]::GetFileName($inputPath));$before=(Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
 Copy-Item -LiteralPath $inputPath -Destination $copy
 if((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $before){throw 'Frozen input copy mismatch.'}
 $copied+=@([pscustomobject]@{Source=$inputPath;Copy=$copy;Sha256=$before})
}
$refs='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$runner=Join-Path $OutputDirectory 'MarkdownAuditFollowupChecks.exe'
$checkSource=Join-Path $PSScriptRoot 'MarkdownAuditFollowupChecks.cs'
$arguments=@('/nologo','/langversion:7','/codepage:65001','/target:exe',('/out:'+$runner),('/r:'+$copied[0].Copy),('/r:'+$copied[1].Copy))
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Xml','System.Windows.Forms','System.Web.Extensions')){$arguments+=('/r:'+(Join-Path $refs ($name+'.dll')))}
$arguments+=$checkSource
& $compiler @arguments 2>&1|Tee-Object -FilePath (Join-Path $OutputDirectory 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Independent audit check compilation failed.'}
$saved=@{}
foreach($key in @('VS2017_AI_SETTINGS_PATH','VS2017_AI_WORKSPACE_PATH','VS2017_AI_DISABLE_RUNTIME_DISCOVERY','VS2017_AI_DISABLE_CLI_DISCOVERY')){$saved[$key]=[Environment]::GetEnvironmentVariable($key,'Process')}
try{
 $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace'
 $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1';$env:VS2017_AI_DISABLE_CLI_DISCOVERY='1'
 $child=Start-Process -FilePath $runner -ArgumentList ('"'+$OutputDirectory.TrimEnd('\')+'" '+$SourceFixture.IsPresent.ToString().ToLowerInvariant()) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $OutputDirectory 'run.log') -RedirectStandardError (Join-Path $OutputDirectory 'run-error.log') -PassThru
 try{
  if(!$child.WaitForExit(20000)){$child.Kill();$null=$child.WaitForExit(2000);throw 'Owned audit-check process exceeded 20 seconds.'}
  $child.WaitForExit();$child.Refresh();$childExit=$child.ExitCode
 }finally{$child.Dispose()}
 $result=Get-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Raw -Encoding UTF8|ConvertFrom-Json
 $changed=@($copied|Where-Object{(Get-FileHash -LiteralPath $_.Source -Algorithm SHA256).Hash -ne $_.Sha256 -or (Get-FileHash -LiteralPath $_.Copy -Algorithm SHA256).Hash -ne $_.Sha256})
 $receipt=[pscustomobject]@{At=[DateTimeOffset]::Now.ToString('o');Inputs=$copied;FixedInputsUnchanged=$changed.Count -eq 0;SourceFixture=$SourceFixture.IsPresent;TestSourceSha256=(Get-FileHash -LiteralPath $checkSource -Algorithm SHA256).Hash;ChildExitCode=$childExit;SharedProductBuild=$false;WindowShown=$false}
 $receipt|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutputDirectory 'input-receipt.json') -Encoding UTF8
 $result|Select-Object Succeeded,Passed,Failed,SourceOnly,AssemblySha256,LoopbackRequestsObserved,WindowShown|ConvertTo-Json
 if(!$result.Succeeded -or $result.Failed -ne 0 -or $changed.Count -ne 0 -or $result.SourceOnly -ne $SourceFixture.IsPresent -or $result.AssemblySha256 -ne $copied[0].Sha256 -or ($null -ne $childExit -and $childExit -ne 0)){throw 'Independent audit checks failed; original evidence is preserved.'}
}finally{foreach($key in $saved.Keys){[Environment]::SetEnvironmentVariable($key,$saved[$key],'Process')}}
exit 0
