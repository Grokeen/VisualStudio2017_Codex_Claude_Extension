#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-Artifacts
desc         : TASK061 실제 작성기와 메뉴 소스를 개인 사본으로 컴파일하고 파일 생성 계약을 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 기존 evidence 보존·Windows PowerShell STA·공유 bin/obj 사용 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory,[ValidateSet('AnyCPU','x86')][string]$Platform='AnyCPU')
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -ne 5){throw 'Use Windows PowerShell 5.1 -NoProfile -STA.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $destination) -and @(Get-ChildItem -LiteralPath $destination -Force).Count){throw 'Use a new empty evidence directory.'}
$null=New-Item -ItemType Directory -Path $destination -Force
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$references='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$arguments=@('/nologo','/langversion:7','/target:exe',('/platform:'+$Platform),('/out:'+(Join-Path $destination 'ArtifactChecks.exe')))
foreach($name in @('System.Core','System.IO.Compression','System.IO.Compression.FileSystem','System.Xml','System.Web.Extensions','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$arguments+=('/r:'+(Join-Path $references ($name+'.dll')))}
$inputs=@()
foreach($relative in @('src/VS2017.AIAssistant/Chat/AssistantArtifactWriter.cs','src/VS2017.AIAssistant/Views/AssistantControl.Artifacts.cs','tests/task061/ArtifactChecks.cs')){
    $original=Join-Path $repo $relative
    $copy=Join-Path $destination ([IO.Path]::GetFileName($original))
    Copy-Item -LiteralPath $original -Destination $copy
    $inputs+=[pscustomobject]@{Path=$original;Sha256=(Get-FileHash -LiteralPath $original).Hash;Copy=$copy}
    $arguments+=$copy
}
$inputs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $destination 'inputs.json') -Encoding UTF8
& $compiler @arguments 2>&1 | Tee-Object -FilePath (Join-Path $destination 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Artifact fixture compilation failed.'}
$null=New-Item -ItemType Directory -Path (Join-Path $destination 'junction-target')
$null=New-Item -ItemType Junction -Path (Join-Path $destination 'junction') -Target (Join-Path $destination 'junction-target')
$child=Start-Process -FilePath (Join-Path $destination 'ArtifactChecks.exe') -ArgumentList ('"'+$destination.TrimEnd('\')+'"') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $destination 'run.log') -RedirectStandardError (Join-Path $destination 'stderr.log')
try{$null=$child.Handle;if(!$child.WaitForExit(30000)){$child.Kill();$child.WaitForExit();throw 'Artifact fixture timed out.'};$child.WaitForExit();$exitCode=$child.ExitCode}finally{$child.Dispose()}
Get-Content -LiteralPath (Join-Path $destination 'run.log') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $destination 'stderr.log') -Encoding UTF8
$result=Get-Content -LiteralPath (Join-Path $destination 'result.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach($item in $inputs){if((Get-FileHash -LiteralPath $item.Path).Hash -ne $item.Sha256){throw 'Input changed during test.'}}
if($null -eq $exitCode -or $exitCode -ne 0 -or !$result.Succeeded){throw 'Artifact checks failed; evidence preserved.'}
Write-Output ('PASS '+$result.Passed+' / '+(Join-Path $destination 'result.json'))
