[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProductAssemblyPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ExpectedProductSha256,
    [string]$CscPath = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$product=[IO.Path]::GetFullPath($ProductAssemblyPath)
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(!(Test-Path -LiteralPath $product -PathType Leaf) -or (Test-Path -LiteralPath $output)){throw '실제 DLL과 새 OutputDirectory가 필요합니다.'}
for($part=$output;$part;$part=[IO.Path]::GetDirectoryName($part)){if((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw '출력 reparse 경로를 거절합니다.'}}
$productSha=(Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
if($ExpectedProductSha256 -and $productSha -ne $ExpectedProductSha256){throw '제품 DLL SHA256이 다릅니다.'}
[void][IO.Directory]::CreateDirectory($output)
$sources=Join-Path $output 'test-helper-source';[void][IO.Directory]::CreateDirectory($sources)
$copy=Join-Path $output 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $product -Destination $copy
if((Get-FileHash -LiteralPath $copy).Hash -ne $productSha){throw '제품 DLL 사본 해시가 다릅니다.'}
$paths=@((Join-Path $repo 'tests/comment-code-preview-poc/FakeEditorFixture.cs'),(Join-Path $repo 'tests/comment-code-preview-poc/PreviewContracts.cs'),(Join-Path $repo 'src/CommentCodePreview.Poc/MockCommentCodeProvider.cs'),(Join-Path $PSScriptRoot 'ProductProviderContracts.cs'))
$inputs=@($paths | ForEach-Object {$target=Join-Path $sources ([IO.Path]::GetFileName($_));$hash=(Get-FileHash -LiteralPath $_).Hash;Copy-Item -LiteralPath $_ -Destination $target;if((Get-FileHash -LiteralPath $target).Hash -ne $hash){throw '검사 helper 사본 해시가 다릅니다.'};[pscustomobject]@{Path=$_;Copy=$target;SHA256=$hash}})
$inputs|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'test-inputs.json') -Encoding UTF8
function Run-Owned([string]$exe,[string[]]$values,[string]$role) {
    $start=New-Object Diagnostics.ProcessStartInfo;$start.FileName=$exe
    $start.Arguments=($values|ForEach-Object{if($_.Contains('"')){throw '인자의 따옴표를 거절합니다.'};'"'+$_+'"'}) -join ' '
    $start.WorkingDirectory=$output;$start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=New-Object Diagnostics.Process;$process.StartInfo=$start
    try{if(!$process.Start()){throw '소유 검사 프로세스 시작 실패'};$pidValue=$process.Id;$stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync();if(!$process.WaitForExit(30000)){$process.Kill();$process.WaitForExit();throw '소유 검사 30초 제한 초과'};$process.WaitForExit();$exit=$process.ExitCode;[IO.File]::WriteAllText((Join-Path $output ($role+'.stdout.log')),$stdout.GetAwaiter().GetResult());[IO.File]::WriteAllText((Join-Path $output ($role+'.stderr.log')),$stderr.GetAwaiter().GetResult());return [pscustomobject]@{Role=$role;PID=$pidValue;ExitCode=$exit;ExitedNormally=$true;ForcedCleanup=$false}}
    finally{$process.Dispose()}
}
$common=@('/nologo','/langversion:5','/codepage:65001','/utf8output','/warnaserror+','/target:exe',('/reference:'+$copy),'/reference:System.Core.dll','/reference:System.Web.Extensions.dll')
$coreExe=Join-Path $output 'ProductCoreContracts.exe';$providerExe=Join-Path $output 'ProductProviderContracts.exe'
$runs=@()
$runs+=Run-Owned $CscPath ($common+@(('/out:'+$coreExe),(Join-Path $sources 'FakeEditorFixture.cs'),(Join-Path $sources 'PreviewContracts.cs'),(Join-Path $sources 'MockCommentCodeProvider.cs'))) 'compile-core-tests'
$runs+=Run-Owned $CscPath ($common+@(('/out:'+$providerExe),(Join-Path $sources 'ProductProviderContracts.cs'))) 'compile-provider-tests'
if(@($runs|Where-Object{$_.ExitCode -ne 0}).Count -gt 0){$runs|ConvertTo-Json|Set-Content (Join-Path $output 'compile-failure.json');throw '제품 DLL을 참조하는 검사 helper 컴파일 실패'}
$coreResult=Join-Path $output 'core104.raw.json';$providerResult=Join-Path $output 'provider.raw.json'
$runs+=Run-Owned $coreExe @($coreResult,$copy,$productSha) 'core104'
$runs+=Run-Owned $providerExe @($providerResult,$copy) 'provider-boundary'
$core=Get-Content -LiteralPath $coreResult -Raw|ConvertFrom-Json;$boundary=Get-Content -LiteralPath $providerResult -Raw|ConvertFrom-Json
$stable=(Get-FileHash -LiteralPath $product).Hash -eq $productSha -and (Get-FileHash -LiteralPath $copy).Hash -eq $productSha
foreach($input in $inputs){if((Get-FileHash -LiteralPath $input.Path).Hash -ne $input.SHA256 -or (Get-FileHash -LiteralPath $input.Copy).Hash -ne $input.SHA256){$stable=$false}}
$summary=[ordered]@{Succeeded=($stable -and $core.Succeeded -and $core.Passed -eq 104 -and $boundary.Failed -eq 0 -and @($runs|Where-Object{$_.ExitCode -ne 0}).Count -eq 0);ProductAssemblyPath=$product;PrivateProductCopy=$copy;ProductSha256=$productSha;AssemblyIdentity=[Reflection.AssemblyName]::GetAssemblyName($copy).FullName;CorePassed=$core.Passed;CoreFailed=$core.Failed;ProviderPassed=$boundary.Passed;ProviderFailed=$boundary.Failed;InputUnchanged=$stable;Processes=$runs;CoreSourceCompiled=$false;ProviderSourceCompiled=$false;OnlyTestHelpersCompiled=$true;OriginalCoreFixtureCopiedUnchanged=$true;RawCoreFixtureHasHistoricalPocLabels=$true;ActualProductDll=$true;FakeEditor=$true;FakeProvider=$true;ActualIDE=$false;ActualAI=$false;Gui=$false;NativeCLI=$false;ProductBuild=$false;NugetRestore=$false;ShippingApproved=$false}
$summary|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding UTF8
$summary|ConvertTo-Json -Depth 4
if(!$summary.Succeeded){exit 1}
