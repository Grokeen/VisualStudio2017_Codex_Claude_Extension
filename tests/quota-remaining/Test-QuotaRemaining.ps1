#requires -Version 5.1
# CodexOrig / 2026-11-07: Fresh output, exact product source hashes, actual compiler and fixture exit codes.
param([Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$ProductAssemblyPath)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out=[IO.Path]::GetFullPath($OutputDirectory)
if(!( $out.StartsWith(($repo+'\artifacts\quota-remaining\'),[StringComparison]::OrdinalIgnoreCase)) -or (Test-Path -LiteralPath $out)){throw 'Fresh private output required.'}
[void][IO.Directory]::CreateDirectory($out)
$sources=@('src/VS2017.AIAssistant/Providers/ProviderRuntime.cs','src/VS2017.AIAssistant/Providers/ProviderExecutionOptions.cs','tests/quota-remaining/QuotaRemainingTests.cs','src/VS2017.AIAssistant/Views/AssistantControl.Runtime.cs')
$inputs=@($sources|ForEach-Object{$p=Join-Path $repo $_;[pscustomobject]@{RelativePath=$_;SHA256=(Get-FileHash -LiteralPath $p).Hash;Bytes=(Get-Item -LiteralPath $p).Length}})
$copies=Join-Path $out 'sources';[void][IO.Directory]::CreateDirectory($copies)
foreach($sourceItem in $inputs){$origin=Join-Path $repo $sourceItem.RelativePath;$copy=Join-Path $copies ([IO.Path]::GetFileName($origin));Copy-Item -LiteralPath $origin -Destination $copy;if((Get-FileHash -LiteralPath $origin).Hash -ne $sourceItem.SHA256 -or (Get-FileHash -LiteralPath $copy).Hash -ne $sourceItem.SHA256){throw 'Source copy hash mismatch.'};$sourceItem|Add-Member CopyPath $copy}
$inputs|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'inputs.json') -Encoding UTF8
$compiler='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
$exe=Join-Path $out 'QuotaRemainingTests.exe'
$compileSources=@($inputs|Where-Object{$_.RelativePath -notlike '*/Views/*'}|ForEach-Object{$_.CopyPath})
$assemblyArguments=@()
$assemblyHash=$null;$mode='sources'
if($ProductAssemblyPath){
    $product=(Resolve-Path -LiteralPath $ProductAssemblyPath).Path
    Copy-Item -LiteralPath $product -Destination (Join-Path $out 'VS2017.AIAssistant.dll')
    $assemblyArguments=@('/reference:'+(Join-Path $out 'VS2017.AIAssistant.dll'))
    $assemblyHash=(Get-FileHash -LiteralPath $product).Hash
    if((Get-FileHash -LiteralPath (Join-Path $out 'VS2017.AIAssistant.dll')).Hash -ne $assemblyHash){throw 'Product DLL copy differs.'}
    $mode='dll'
    $compileSources=@($inputs|Where-Object{$_.RelativePath -like 'tests/*'}|ForEach-Object{$_.CopyPath})
}
& $compiler /nologo /warnaserror+ /target:exe /langversion:7 /codepage:65001 /reference:System.Web.Extensions.dll ('/out:'+$exe) @assemblyArguments @compileSources *> (Join-Path $out 'compile.log')
$compileExit=$LASTEXITCODE
if($compileExit -ne 0){throw 'Quota fixture compile failed; see preserved log.'}
& $exe (Join-Path $out 'result.json') $mode *> (Join-Path $out 'run.log')
$runExit=$LASTEXITCODE
$changed=@($inputs|Where-Object{(Get-FileHash -LiteralPath (Join-Path $repo $_.RelativePath)).Hash -cne $_.SHA256 -or (Get-FileHash -LiteralPath $_.CopyPath).Hash -cne $_.SHA256})
$dllStable=(!$ProductAssemblyPath -or ((Get-FileHash -LiteralPath $product).Hash -eq $assemblyHash -and (Get-FileHash -LiteralPath (Join-Path $out 'VS2017.AIAssistant.dll')).Hash -eq $assemblyHash))
$binding=[IO.File]::ReadAllText((Join-Path $repo $sources[3])).Contains('runtimeUsage.DescribeForAccount(runtimeInfo == null ? null : runtimeInfo.Usage)')
[ordered]@{CompilerExit=$compileExit;RunExit=$runExit;InputsUnchanged=($changed.Count -eq 0);Mode=$mode;ProductAssemblySHA256=$assemblyHash;ProductDllInputUnchanged=$dllStable;TooltipSourceBindingObserved=$binding;TooltipUiExecuted=$false;ProductSource=(Join-Path $repo $sources[0]);ResultSHA256=(Get-FileHash -LiteralPath (Join-Path $out 'result.json')).Hash;NoGui=$true;NoAccount=$true}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $out 'validation.json') -Encoding UTF8
if($runExit -ne 0 -or $changed.Count -or !$dllStable -or !$binding){throw 'Quota fixture failed.'}
Get-Content -LiteralPath (Join-Path $out 'result.json') -Raw
