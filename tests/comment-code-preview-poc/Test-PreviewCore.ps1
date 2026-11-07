#requires -Version 5.1
<#
name         : Test-PreviewCore
desc         : Private C#5 core DLL and fake-editor contracts; no product build, SDK restore or UI.
author       : CodexOrig
create date  : 2026-11-07
#>
[CmdletBinding()]
param([string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop'){throw 'Use Windows PowerShell 5.1.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts'))+[IO.Path]::DirectorySeparatorChar
if(!$OutputDirectory){$OutputDirectory=$allowed+'run-'+[Guid]::NewGuid().ToString('N').Substring(0,12)}
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!$out.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $out)){throw 'Fresh local fixture output required; never overwrite evidence.'}
for($part=$out;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Output reparse points rejected.'}}
[void][IO.Directory]::CreateDirectory($out)
$copies=Join-Path $out 'source';[void][IO.Directory]::CreateDirectory($copies)
$coreNames=@('EditorContract.cs','PreviewSession.cs','MockCommentCodeProvider.cs')
$testNames=@('FakeEditorFixture.cs','PreviewContracts.cs')
$inputs=@()
foreach($name in $coreNames+$testNames){$path=if($coreNames -contains $name){Join-Path $repo ('src/CommentCodePreview.Poc/'+$name)}else{Join-Path $PSScriptRoot $name};$copy=Join-Path $copies $name;$before=(Get-FileHash -LiteralPath $path).Hash;Copy-Item -LiteralPath $path -Destination $copy;$copied=(Get-FileHash -LiteralPath $copy).Hash;if($before -ne $copied){throw 'Source copy hash differs.'};$inputs+=[pscustomobject]@{Path=$path;Copy=$copy;Sha256=$before}}
$inputs+=[pscustomobject]@{Path=$PSCommandPath;Copy=$null;Sha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash}
$inputs|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $out 'inputs-before.json') -Encoding UTF8
function Quote([string]$Value){if($Value.Contains([string][char]34) -or $Value.Contains([string][char]10) -or $Value.Contains([string][char]13)){throw 'Unsafe process argument.'};return ([string][char]34)+$Value+([string][char]34)}
function Run-Owned([string]$Exe,[string[]]$Arguments,[string]$Role){
 $start=New-Object Diagnostics.ProcessStartInfo;$start.FileName=$Exe;$start.Arguments=($Arguments|ForEach-Object{Quote $_}) -join ' ';$start.WorkingDirectory=$out;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true;$start.StandardOutputEncoding=[Text.Encoding]::UTF8;$start.StandardErrorEncoding=[Text.Encoding]::UTF8
 $process=New-Object Diagnostics.Process;$process.StartInfo=$start;$normal=$false;$forced=$false;$alive=$false;$idValue=$null;$exitValue=$null;$watch=[Diagnostics.Stopwatch]::StartNew()
 try{if(!$process.Start()){throw 'Child start failed.'};$idValue=$process.Id;$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(!$process.WaitForExit(30000)){if(!$process.HasExited -and [string]::Equals($process.MainModule.FileName,[IO.Path]::GetFullPath($Exe),[StringComparison]::OrdinalIgnoreCase)){$process.Kill();$forced=$true;$process.WaitForExit()};throw 'Owned process timeout.'};$process.WaitForExit();$exitValue=$process.ExitCode;$normal=$true;[IO.File]::WriteAllText((Join-Path $out ($Role+'.stdout.log')),$stdout.GetAwaiter().GetResult(),[Text.UTF8Encoding]::new($false));[IO.File]::WriteAllText((Join-Path $out ($Role+'.stderr.log')),$stderr.GetAwaiter().GetResult(),[Text.UTF8Encoding]::new($false))}
 finally{try{$alive=!$process.HasExited}catch{};$record=[pscustomobject]@{Role=$Role;Pid=$idValue;ExitCode=$exitValue;ExitedNormally=$normal;ForcedCleanup=$forced;StillAlive=$alive;UseShellExecute=$false;ElapsedMs=$watch.ElapsedMilliseconds};$record|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out ($Role+'.process.json')) -Encoding UTF8;$process.Dispose()};return $record
}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$framework=Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$referenceArgs=@('mscorlib','System','System.Core')|ForEach-Object{'/reference:'+(Join-Path $framework ($_+'.dll'))}
$common=@('/noconfig','/nostdlib+','/nologo','/langversion:5','/codepage:65001','/utf8output','/warnaserror+')+$referenceArgs
$assembly=Join-Path $out 'CommentCodePreview.Poc.dll';$runner=Join-Path $out 'PreviewContracts.exe';$resultPath=Join-Path $out 'result.json'
$processes=@();$errorType=$null;$hresult=$null;$succeeded=$false
try{
 $arguments=$common+@('/target:library',('/out:'+$assembly))+@($coreNames|ForEach-Object{Join-Path $copies $_});$record=Run-Owned $compiler $arguments 'core-compile';$processes+=$record;if($record.ExitCode -ne 0){throw 'Private core compilation failed.'}
 $arguments=$common+@('/target:exe',('/out:'+$runner),('/reference:'+$assembly),('/reference:'+(Join-Path $framework 'System.Web.Extensions.dll')))+@($testNames|ForEach-Object{Join-Path $copies $_});$record=Run-Owned $compiler $arguments 'test-compile';$processes+=$record;if($record.ExitCode -ne 0){throw 'Private test compilation failed.'}
 $beforeAssembly=(Get-FileHash -LiteralPath $assembly).Hash;$record=Run-Owned $runner @($resultPath,$assembly,$beforeAssembly) 'contracts';$processes+=$record
 if($record.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath)){throw 'PoC contracts failed; preserve result and logs.'}
 $result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8|ConvertFrom-Json
 $succeeded=$result.Succeeded -and (Get-FileHash -LiteralPath $assembly).Hash -eq $beforeAssembly
}catch{$errorType=$_.Exception.GetType().FullName;$hresult=$_.Exception.HResult}
$stable=$true;$after=@($inputs|ForEach-Object{$original=(Get-FileHash -LiteralPath $_.Path).Hash;$copyHash=if($_.Copy){(Get-FileHash -LiteralPath $_.Copy).Hash}else{$null};$match=$original -eq $_.Sha256 -and (!$_.Copy -or $copyHash -eq $_.Sha256);if(!$match){$stable=$false};[ordered]@{Path=$_.Path;BeforeSha256=$_.Sha256;AfterSha256=$original;CopySha256=$copyHash;Unchanged=$match}})
$processes=@(Get-ChildItem -LiteralPath $out -Filter '*.process.json' -File|ForEach-Object{Get-Content -LiteralPath $_.FullName -Raw -Encoding UTF8|ConvertFrom-Json})
$normal=@($processes|Where-Object{!$_.ExitedNormally -or $_.ForcedCleanup -or $_.StillAlive}).Count -eq 0
$summary=[ordered]@{Succeeded=($succeeded -and $stable -and $normal -and $null -eq $errorType);InputsUnchanged=$stable;Inputs=$after;Processes=$processes;NormalOwnedCleanup=$normal;CoreAssemblySha256=$(if(Test-Path -LiteralPath $assembly){(Get-FileHash -LiteralPath $assembly).Hash}else{$null});ResultSha256=$(if(Test-Path -LiteralPath $resultPath){(Get-FileHash -LiteralPath $resultPath).Hash}else{$null});ErrorType=$errorType;HResult=$hresult;ActualPocAssembly=$true;ActualVsExtension=$false;VsSdkUsed=$false;ProductMSBuild=$false;NuGetRestore=$false;Gui=$false;RealEditor=$false;Accounts=$false;Network=$false;Cli=$false}
$summary|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $out 'validation.json') -Encoding UTF8
[pscustomobject]@{Succeeded=$summary.Succeeded;InputsUnchanged=$stable;NormalOwnedCleanup=$normal;OutputDirectory=$out;ErrorType=$errorType}|ConvertTo-Json
if(!$summary.Succeeded){exit 1}
