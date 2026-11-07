#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-KeyboardToolTip
desc         : Explicit Root candidate tuple and inventory are required before any private compile or owned-window execution.
author       : CodexOrig
create date  : 2026-11-03
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CandidateTuplePath,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedTupleSha256,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [switch]$Execute
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop'){throw 'Windows PowerShell 5.1 is required.'}
function Get-Hash([string]$Path){return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash}
function Required($Object,[string]$Name){if($null -eq $Object -or $Object.PSObject.Properties.Name -notcontains $Name -or $null -eq $Object.$Name){throw ('Missing explicit tuple field: '+$Name)};return $Object.$Name}
function No-Links([string]$Path){for($part=[IO.Path]::GetFullPath($Path);$part;$part=[IO.Path]::GetDirectoryName($part)){if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Reparse-point inputs/outputs are rejected.'}}}
function Fixed-Input([string]$Path,[string]$Hash){if($Hash -notmatch '^[A-Fa-f0-9]{64}$' -or !(Test-Path -LiteralPath $Path -PathType Leaf)){throw 'Explicit fixed file is missing.'};No-Links $Path;if((Get-Hash $Path) -ne $Hash){throw 'Explicit input SHA mismatch.'}}
function Quote-Argument([string]$Value){if($Value.Contains('"') -or $Value.Contains("`r") -or $Value.Contains("`n")){throw 'Unsafe process argument characters.'};return '"'+$Value+'"'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
$allowed=[IO.Path]::GetFullPath((Join-Path $repo 'artifacts/task073-tooltip'))+[IO.Path]::DirectorySeparatorChar
if(!$out.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $out)){throw 'Use a fresh nonce path under this SHORT artifacts/task073-tooltip root.'}
No-Links $out
$tuplePath=[IO.Path]::GetFullPath($CandidateTuplePath);Fixed-Input $tuplePath $ExpectedTupleSha256
$tuple=Get-Content -LiteralPath $tuplePath -Raw -Encoding UTF8|ConvertFrom-Json
if((Required $tuple 'Task') -ne 'TASK073-tooltip'){throw 'Wrong explicit tuple task.'}
$candidateName=[string](Required $tuple 'CandidateName')
if($candidateName -notmatch '^candidate[1-9][0-9]*$'){throw 'An explicit candidate name is required; no current/latest fallback.'}
$candidate=[IO.Path]::GetFullPath([string](Required $tuple 'CandidateRoot'))
if((Split-Path $candidate -Leaf) -ne $candidateName){throw 'CandidateRoot and CandidateName differ.'}
$target=[string](Required $tuple 'Target')
$projects=@{'2017.Legacy'='VS2017.Legacy.AIAssistant';'2017'='VS2017.AIAssistant';'2019'='VS2019.AIAssistant';'2022'='VS2022.AIAssistant'}
if(!$projects.ContainsKey($target)){throw 'Unsupported explicit SKU.'}
$assembly=[IO.Path]::GetFullPath([string](Required $tuple 'AssemblyPath'))
$assemblyHash=([string](Required $tuple 'AssemblySha256')).ToUpperInvariant()
if($assembly -ne [IO.Path]::GetFullPath((Join-Path $candidate ($target+'/Release/VS2017.AIAssistant.dll')))){throw 'DLL does not belong to the exact candidate/SKU tuple.'}
Fixed-Input $assembly $assemblyHash
$assets=[IO.Path]::GetFullPath([string](Required $tuple 'AssetsPath'))
$assetsHash=[string](Required $tuple 'AssetsSha256')
if($assets -ne [IO.Path]::GetFullPath((Join-Path $candidate ('source/src/'+$projects[$target]+'/obj/project.assets.json')))){throw 'Assets are not from the exact candidate/SKU snapshot.'}
Fixed-Input $assets $assetsHash
$inventory=[IO.Path]::GetFullPath([string](Required $tuple 'SourceInventoryPath'));$inventoryHash=[string](Required $tuple 'SourceInventorySha256')
if($inventory -ne [IO.Path]::GetFullPath((Join-Path $candidate 'source-hashes.json'))){throw 'Explicit source inventory is outside this candidate.'};Fixed-Input $inventory $inventoryHash
$metadata=[IO.Path]::GetFullPath([string](Required $tuple 'DevelopmentMetadataPath'));$metadataHash=[string](Required $tuple 'DevelopmentMetadataSha256')
if($metadata -ne [IO.Path]::GetFullPath((Join-Path $candidate 'development-artifacts.json'))){throw 'Development metadata is outside this candidate.'};Fixed-Input $metadata $metadataHash
$development=Get-Content -LiteralPath $metadata -Raw -Encoding UTF8|ConvertFrom-Json
if($development.Task -ne 'TASK073' -or $development.Candidate -ne $candidateName -or !$development.SourceSnapshotUnchanged -or @($development.Builds).Count -ne 4){throw 'Candidate metadata is not a complete frozen four-SKU build.'}
foreach($year in $projects.Keys){$build=@($development.Builds|Where-Object Target -eq $year);if($build.Count -ne 1 -or $build[0].ExitCode -ne 0 -or $build[0].Warnings -ne 0 -or $build[0].Errors -ne 0){throw 'Every SKU must have a successful zero-warning fixed build.'}}
$sourceRoot=[IO.Path]::GetFullPath((Join-Path $candidate 'source'))+[IO.Path]::DirectorySeparatorChar
# 2026-11-03 CodexOrig : PS5 JSON 배열은 먼저 대입한 뒤 열거하여 중첩 배열 한 행으로 취급하지 않는다.
$decodedRecords=Get-Content -LiteralPath $inventory -Raw -Encoding UTF8|ConvertFrom-Json
$records=@($decodedRecords)
$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
$inputs=New-Object 'System.Collections.Generic.List[object]'
foreach($row in $records){$relative=([string]$row.RelativePath).Replace('\','/');if([IO.Path]::IsPathRooted($relative) -or @($relative.Split('/')|Where-Object{$_ -eq '..' -or $_ -eq '.'}).Count -gt 0 -or !$seen.Add($relative)){throw 'Unsafe/duplicate source inventory row.'};$path=[IO.Path]::GetFullPath((Join-Path $sourceRoot $relative));if(!$path.StartsWith($sourceRoot,[StringComparison]::OrdinalIgnoreCase)){throw 'Source inventory escaped snapshot.'};Fixed-Input $path ([string]$row.Hash);if((Get-Item -LiteralPath $path).Length -ne [long]$row.Bytes){throw 'Source inventory bytes differ.'};$inputs.Add([pscustomobject]@{Path=$path;Sha256=[string]$row.Hash})}
if($records.Count -eq 0 -or $records.Count -ne [int]$development.SourceInputs){throw 'Source inventory count differs from fixed build metadata.'}
$behaviorRelative='src/VS2017.AIAssistant/Views/AssistantKeyboardToolTip.cs';$behaviorHash=[string](Required $tuple 'BehaviorSourceSha256')
$behaviorRows=@($records|Where-Object{([string]$_.RelativePath).Replace('\','/') -eq $behaviorRelative})
if($behaviorRows.Count -ne 1 -or $behaviorRows[0].Hash -ne $behaviorHash){throw 'Exact behavior source SHA is missing from the candidate inventory.'}
$harness=Join-Path $PSScriptRoot 'KeyboardToolTipProbe.cs';$testSources=@($PSCommandPath,$harness)
$declared=@(Required $tuple 'HarnessSources')
foreach($path in $testSources){$matches=@($declared|Where-Object Name -eq ([IO.Path]::GetFileName($path)));if($matches.Count -ne 1){throw 'Harness source must be explicitly frozen in the tuple.'};Fixed-Input $path ([string]$matches[0].Sha256);$inputs.Add([pscustomobject]@{Path=$path;Sha256=[string]$matches[0].Sha256})}
$runtimeDirectories=@();$resolver=$null
if($tuple.PSObject.Properties.Name -contains 'ResolverSourcePath'){
    $resolver=[IO.Path]::GetFullPath([string](Required $tuple 'ResolverSourcePath'));Fixed-Input $resolver ([string](Required $tuple 'ResolverSourceSha256'))
    $expectedResolver=[IO.Path]::GetFullPath((Join-Path $repo 'tests/task073-root/ManagedCandidateResolver.cs'))
    if($resolver -ne $expectedResolver){throw 'Only the unchanged Root managed resolver is permitted.'}
    $inputs.Add([pscustomobject]@{Path=$resolver;Sha256=[string]$tuple.ResolverSourceSha256})
    $runtimeDirectories=@(Required $tuple 'RuntimeDirectories');foreach($directory in $runtimeDirectories){if(!(Test-Path -LiteralPath $directory -PathType Container)){throw 'Explicit runtime directory is missing.'};No-Links $directory}
}
if($Execute){if((Required $tuple 'PermitOwnedWindowShow') -ne $true -or (Required $tuple 'ExecutionOwner') -ne 'CodexOrig' -or [DateTimeOffset](Required $tuple 'ReservationExpiresAt') -le [DateTimeOffset]::Now){throw 'A current explicit Root-owned window reservation is required.'}}
[void][IO.Directory]::CreateDirectory($out)
$copy=Join-Path $out 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $assembly -Destination $copy;Fixed-Input $copy $assemblyHash
Copy-Item -LiteralPath $tuplePath -Destination (Join-Path $out 'candidate-tuple.json')
$harnessCopy=Join-Path $out 'KeyboardToolTipProbe.cs';Copy-Item -LiteralPath $harness -Destination $harnessCopy
$config=Join-Path $out 'probe-config.json';[ordered]@{CandidateName=$candidateName;BehaviorSourceSha256=$behaviorHash;RuntimeDirectories=$runtimeDirectories}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $config -Encoding UTF8
$plan=[ordered]@{Task='TASK073-tooltip';CandidateName=$candidateName;Target=$target;SourceInventorySha256=$inventoryHash;SourceInputCount=$records.Count;BehaviorSourceSha256=$behaviorHash;AssemblySha256=$assemblyHash;AssetsSha256=$assetsHash;TupleSha256=$ExpectedTupleSha256;Inputs=$inputs;PreparedOnly=(!$Execute);CompilerExecuted=$false;OwnedWindowRequested=[bool]$Execute;ActualProductBehaviorPassed=$false;NoProfileSettingsCliNetwork=$true}
$plan|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $out 'plan.json') -Encoding UTF8
if(!$Execute){Write-Output 'Prepared only. No compile or window execution.';return}
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$framework='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$exe=Join-Path $out 'KeyboardToolTipProbe.exe';$compilerArguments=@('/nologo','/target:exe','/langversion:7','/warnaserror+',('/out:'+$exe))
foreach($name in @('System','System.Core','System.Xml','System.Web.Extensions','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$compilerArguments+=('/r:'+(Join-Path $framework ($name+'.dll')))}
$compilerArguments+=$harnessCopy
if($resolver){$resolverCopy=Join-Path $out 'ManagedCandidateResolver.cs';Copy-Item -LiteralPath $resolver -Destination $resolverCopy;Fixed-Input $resolverCopy ([string]$tuple.ResolverSourceSha256);$compilerArguments+=('/define:TASK073_ROOT_RESOLVER'),$resolverCopy}
& $compiler @compilerArguments 2>&1|Set-Content -LiteralPath (Join-Path $out 'compile.log') -Encoding UTF8
$compileExit=$LASTEXITCODE;if($compileExit -ne 0){throw 'Private fixture compilation failed; evidence preserved.'}
$resultPath=Join-Path $out 'result.json'
$start=New-Object Diagnostics.ProcessStartInfo;$start.FileName=$exe;$start.Arguments=(@($copy,$assemblyHash,$resultPath,$config)|ForEach-Object{Quote-Argument $_}) -join ' '
$start.WorkingDirectory=$out;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
$process=New-Object Diagnostics.Process;$process.StartInfo=$start;$forced=$false;$normal=$false;$processExit=$null;$pidValue=$null
try{
    if(!$process.Start()){throw 'Owned GUI fixture did not start.'};$pidValue=$process.Id
    $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
    if(!$process.WaitForExit(60000)){
        if(!$process.HasExited -and [string]::Equals($process.MainModule.FileName,$exe,[StringComparison]::OrdinalIgnoreCase)){$process.Kill();$forced=$true;$process.WaitForExit()}
        throw 'Owned tooltip fixture timed out; this is not a passing behavior result.'
    }
    $process.WaitForExit();$processExit=$process.ExitCode;$normal=$true
    [IO.File]::WriteAllText((Join-Path $out 'stdout.log'),$stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $out 'stderr.log'),$stderr.GetAwaiter().GetResult())
}finally{
    $alive=$false;try{$alive=!$process.HasExited}catch{}
    [ordered]@{OwnedPid=$pidValue;UseShellExecute=$false;ExitedNormally=$normal;ExitCode=$processExit;ForcedOwnedProcessCleanup=$forced;StillAlive=$alive}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'owned-process.json') -Encoding UTF8
    $process.Dispose()
    $stable=$true;foreach($sourceInput in $inputs){if((Get-Hash $sourceInput.Path) -ne $sourceInput.Sha256){$stable=$false}}
    $stable=$stable -and ((Get-Hash $assembly) -eq $assemblyHash) -and ((Get-Hash $copy) -eq $assemblyHash) -and ((Get-Hash $assets) -eq $assetsHash) -and ((Get-Hash $inventory) -eq $inventoryHash) -and ((Get-Hash $metadata) -eq $metadataHash) -and ((Get-Hash $tuplePath) -eq $ExpectedTupleSha256)
    [ordered]@{InputsUnchanged=$stable;ActualAssemblySha256=(Get-Hash $assembly);CopiedAssemblySha256=(Get-Hash $copy);InventorySha256=(Get-Hash $inventory);CandidateName=$candidateName;CompileExit=$compileExit}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'inputs-after.json') -Encoding UTF8
}
if(!$stable -or !$normal -or $forced -or $alive -or !(Test-Path -LiteralPath $resultPath)){throw 'Missing immutable result or normal owned cleanup.'}
$result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8|ConvertFrom-Json
if($processExit -eq 2 -and $result.Outcome -eq 'NotRun'){Write-Output 'NotRun: owned window/load/activation/focus environment was not established.';exit 2}
if($processExit -ne 0 -or !$result.Succeeded -or !$result.AllRequiredCasesExecuted -or !$result.WindowClosed -or $result.OpenOwnedToolTips -ne 0){throw 'Actual product tooltip contracts failed; raw evidence preserved.'}
Write-Output ('Passed actual tooltip behavior: '+$candidateName+' / '+$result.Passed+' assertions; physical input not tested.')
