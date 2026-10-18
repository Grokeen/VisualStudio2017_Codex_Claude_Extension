#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-GuideBundleIntegrity
desc         : Compare frozen guide assets, runtime files and ZIP entries without extracting a VSIX.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : Audit instruction 5; verify ten original media assets and MIT bytes.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$VsixPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$VsixPath=(Resolve-Path -LiteralPath $VsixPath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$productDirectory=[IO.Path]::GetDirectoryName($AssemblyPath)
if($OutputDirectory -eq $productDirectory -or $OutputDirectory.StartsWith($productDirectory.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Use a result directory outside the immutable product tree.'}
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Use a fresh output directory; prior results are preserved.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem,WindowsBase,PresentationCore
$failures=New-Object 'Collections.Generic.List[string]'
$files=New-Object 'Collections.Generic.List[object]'
$zip=$null

<#
-------------------------------------------------------------------------
name         : Get-BytesHash
desc         : Hash decoded ZIP bytes without writing any package path to disk.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : Streams and cryptographic handles are disposed deterministically.
-------------------------------------------------------------------------
#>
function Get-BytesHash([byte[]]$Bytes){$hasher=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($hasher.ComputeHash($Bytes)).Replace('-','')}finally{$hasher.Dispose()}}

<#
-------------------------------------------------------------------------
name         : Get-EntryBytes
desc         : Require one exact archive entry and enforce a decompressed byte cap.
author       : CodexCode
create date  : 2026-10-19
update date  : 2026-10-19 CodexCode : No archive extraction, duplicates, traversal or unlimited reads.
-------------------------------------------------------------------------
#>
function Get-EntryBytes([string]$RelativePath,[int]$Limit){
 $entryMatches=@($zip.Entries|Where-Object{$_.FullName.Replace('\','/').Equals($RelativePath,[StringComparison]::OrdinalIgnoreCase)})
 if($entryMatches.Count -ne 1 -or $entryMatches[0].Length -gt $Limit){throw ('Missing, duplicate or oversized package entry: '+$RelativePath)}
 $entryStream=$entryMatches[0].Open();$memory=New-Object IO.MemoryStream
 try{$buffer=New-Object byte[] 8192;while(($count=$entryStream.Read($buffer,0,$buffer.Length)) -gt 0){if($memory.Length+$count -gt $Limit){throw 'Decompressed entry exceeded its cap.'};$memory.Write($buffer,0,$count)};return ,$memory.ToArray()}
 finally{$entryStream.Dispose();$memory.Dispose()}
}

try{
 $assemblyIdentity=[Reflection.AssemblyName]::GetAssemblyName($AssemblyPath)
 if($assemblyIdentity.Name -ne 'VS2017.AIAssistant'){throw 'A real product assembly is required.'}
 $beforeDll=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
 $beforeVsix=(Get-FileHash -LiteralPath $VsixPath -Algorithm SHA256).Hash
 $zip=[IO.Compression.ZipFile]::OpenRead($VsixPath)
 if($zip.Entries.Count -gt 4096){throw 'Archive entry count exceeded the verification bound.'}
 $packageDll=Get-EntryBytes 'VS2017.AIAssistant.dll' 32MB
 if((Get-BytesHash $packageDll) -ne $beforeDll){throw 'Package DLL does not match the requested runtime DLL.'}
 $mediaManifest=Get-Content -LiteralPath (Join-Path $repository 'artifacts/task024/guide-media-freeze1.json') -Raw -Encoding UTF8|ConvertFrom-Json
 if(@($mediaManifest.Media).Count -ne 10){throw 'The original media manifest must contain exactly five GIF and five PNG entries.'}
 foreach($media in $mediaManifest.Media){
  try{
   if([IO.Path]::GetFileName($media.Name) -ne $media.Name -or $media.Name -notmatch '^0[1-5]-(connect|request|review|guide|install)\.(gif|png)$'){throw 'Unexpected media manifest path.'}
   $source=Join-Path $repository ('docs/assets/guide-media/'+$media.Name)
   $runtime=Join-Path $productDirectory ('Resources/GuideMedia/'+$media.Name)
   $sourceHash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
   $runtimeHash=(Get-FileHash -LiteralPath $runtime -Algorithm SHA256).Hash
   $bytes=Get-EntryBytes ('Resources/GuideMedia/'+$media.Name) 2MB
   $archiveHash=Get-BytesHash $bytes
   if($sourceHash -ne $media.Sha256 -or $runtimeHash -ne $media.Sha256 -or $archiveHash -ne $media.Sha256){throw ('Source/runtime/ZIP differs from the frozen media: '+$media.Name)}
   $stream=New-Object IO.MemoryStream(,$bytes)
   try{
    $decoder=[Windows.Media.Imaging.BitmapDecoder]::Create($stream,[Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,[Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $expectedFrames=if($media.Name.EndsWith('.gif')){4}else{1}
    if($decoder.Frames.Count -ne $expectedFrames -or $decoder.Frames[0].PixelWidth -ne 600 -or $decoder.Frames[0].PixelHeight -ne 420){throw 'Media frame count or dimensions differ from the guide contract.'}
    $files.Add([pscustomobject]@{Name=$media.Name;Sha256=$archiveHash;Bytes=$bytes.Length;Frames=$decoder.Frames.Count;Width=600;Height=420;SourceRuntimeAndZipEqual=$true})
   }finally{$stream.Dispose()}
  }catch{$failures.Add($_.Exception.Message)}
 }
 $licenseRelative='Resources/ThirdParty/AvalonEdit/LICENSE.txt'
 $licenseHash='7CFE74F11EAEF7A239E7C63D33595F924E7922FB3E3F3651014389620B228A07'
 $licenseBytes=Get-EntryBytes $licenseRelative 64KB
 if((Get-BytesHash $licenseBytes) -ne $licenseHash -or (Get-FileHash -LiteralPath (Join-Path $productDirectory $licenseRelative) -Algorithm SHA256).Hash -ne $licenseHash){throw 'The packaged AvalonEdit MIT license differs from the original.'}
 $avalon=Join-Path $productDirectory 'ICSharpCode.AvalonEdit.dll'
 $avalonBytes=Get-EntryBytes 'ICSharpCode.AvalonEdit.dll' 8MB
 if((Get-BytesHash $avalonBytes) -ne (Get-FileHash -LiteralPath $avalon -Algorithm SHA256).Hash){throw 'Packaged AvalonEdit dependency differs from the runtime dependency.'}
 if((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash -ne $beforeDll -or (Get-FileHash -LiteralPath $VsixPath -Algorithm SHA256).Hash -ne $beforeVsix){throw 'Frozen product input changed during verification.'}
}catch{$failures.Add($_.Exception.Message)}
finally{if($zip){$zip.Dispose()}}
$result=[pscustomobject]@{Succeeded=$failures.Count -eq 0 -and $files.Count -eq 10;MediaFilesVerified=$files.Count;Files=$files;Failures=@($failures);AssemblyPath=$AssemblyPath;VsixPath=$VsixPath;ProductAssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash;VsixSha256=(Get-FileHash -LiteralPath $VsixPath -Algorithm SHA256).Hash;OriginalMediaManifest='artifacts/task024/guide-media-freeze1.json';ExpectedAvalonMitSha256='7CFE74F11EAEF7A239E7C63D33595F924E7922FB3E3F3651014389620B228A07';PackageExtracted=$false;ProductCodeExecuted=$false;WindowShown=$false;NativePointerUsed=$false;AuthenticationInvoked=$false;NetworkRequested=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
$result|Select-Object Succeeded,MediaFilesVerified,Failures,ProductAssemblySha256,VsixSha256|ConvertTo-Json -Depth 4
if(!$result.Succeeded){exit 1}
exit 0
