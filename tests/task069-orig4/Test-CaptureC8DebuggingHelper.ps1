#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-CaptureC8DebuggingHelper
 desc        : Exact C8 DLL offscreen WPF acceptance with synthetic break-frame reader and anonymous fixture.
author       : CodexOrig4
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig4 : hash-lock product/assets and isolate all user settings.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ProductDll,
    [Parameter(Mandatory=$true)][string]$AssetsPath,
    [Parameter(Mandatory=$true)][string]$ExpectedDllSha256,
    [Parameter(Mandatory=$true)][string]$ExpectedAssetsSha256,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Run Windows PowerShell 5.1 -NoProfile -STA.'}
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dll=[IO.Path]::GetFullPath($ProductDll);$assets=[IO.Path]::GetFullPath($AssetsPath);$output=[IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if(!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $assets)){throw 'Exact C8 DLL/assets path is missing.'}
$initialDll=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash;$initialAssets=(Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash
if($initialDll -ne $ExpectedDllSha256.ToUpperInvariant()){throw "C8 DLL hash mismatch: $initialDll"};if($initialAssets -ne $ExpectedAssetsSha256.ToUpperInvariant()){throw "C8 assets hash mismatch: $initialAssets"}
if(Test-Path -LiteralPath $output){throw "Evidence output exists; preserve it and choose a fresh directory: $output"};$null=New-Item -ItemType Directory -Path $output
$profile=Join-Path $output 'private-anonymous-profile';$null=New-Item -ItemType Directory -Path $profile
$env:APPDATA=Join-Path $profile 'Roaming';$env:LOCALAPPDATA=Join-Path $profile 'Local';$env:USERPROFILE=$profile
$env:VS2017_AI_SETTINGS_PATH=Join-Path $profile 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $profile 'workspace';$env:VS2017_AI_DISABLE_CLI_DISCOVERY='1';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
$null=New-Item -ItemType Directory -Path $env:VS2017_AI_WORKSPACE_PATH
Set-Content -LiteralPath $env:VS2017_AI_SETTINGS_PATH -Value '{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":false}' -Encoding UTF8
$assetsObject=Get-Content -LiteralPath $assets -Raw -Encoding UTF8|ConvertFrom-Json;$packageRoot=$assetsObject.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$frameworkTarget=$assetsObject.targets.PSObject.Properties|Where-Object{$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'}|Select-Object -First 1;if($null -eq $frameworkTarget){throw 'No .NET Framework target in C8 assets.'}
$dependencies=@{}
foreach($entry in $frameworkTarget.Value.PSObject.Properties){foreach($kind in @('compile','runtime')){if($entry.Value.PSObject.Properties.Name -contains $kind){foreach($part in $entry.Value.$kind.PSObject.Properties.Name){if($part -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($part)]=Join-Path $packageRoot ($assetsObject.libraries.($entry.Name).path+'/'+$part)}}}}}
foreach($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $dll) -Filter '*.dll' -File){if($sibling.FullName -ne $dll){$dependencies[$sibling.BaseName]=$sibling.FullName;Copy-Item -LiteralPath $sibling.FullName -Destination $output}}
# AssemblyResolve can use Visual Studio's public EnvDTE/Shell assemblies without starting or attaching to an IDE.
$vsRoots=@('C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\PublicAssemblies','C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\PrivateAssemblies','C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE')
foreach($root in $vsRoots){if(Test-Path -LiteralPath $root){foreach($name in @('EnvDTE.dll','EnvDTE80.dll','EnvDTE90a.dll','Microsoft.VisualStudio.Shell.15.0.dll','Microsoft.VisualStudio.Shell.Framework.dll','Microsoft.VisualStudio.Shell.Interop.15.0.dll')){$candidate=Join-Path $root $name;if(Test-Path -LiteralPath $candidate){$dependencies[[IO.Path]::GetFileNameWithoutExtension($name)]=$candidate}}}}
$dllCopy=Join-Path $output ([IO.Path]::GetFileName($dll));Copy-Item -LiteralPath $dll -Destination $dllCopy
$dependencyPath=Join-Path $output 'dependencies.json';$dependencies|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $dependencyPath -Encoding UTF8
$source=Join-Path $PSScriptRoot 'CaptureC8DebuggingHelper.cs';$compiler='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe';$referenceDir='C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
if(!(Test-Path -LiteralPath $compiler)){throw "C# compiler not found: $compiler"};$runner=Join-Path $output 'CaptureC8DebuggingHelper.exe'
$references=@('/nologo','/target:exe','/langversion:7',('/out:'+$runner),('/r:'+$dllCopy),('/r:'+(Join-Path $referenceDir 'WindowsBase.dll')),('/r:'+(Join-Path $referenceDir 'PresentationCore.dll')),('/r:'+(Join-Path $referenceDir 'PresentationFramework.dll')),('/r:'+(Join-Path $referenceDir 'System.Xaml.dll')),('/r:System.dll'),('/r:System.Core.dll'),('/r:System.Web.Extensions.dll'),$source)
& $compiler @references 2>&1|Tee-Object (Join-Path $output 'compile.log');if($LASTEXITCODE -ne 0){throw "C8 capture harness compile failed: $LASTEXITCODE"}
$manifest=[ordered]@{CandidateDll=$dll;CandidateDllSha256=$initialDll;AssetsPath=$assets;AssetsSha256=$initialAssets;CaptureSource=$source;CaptureSourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;DisplayIdentity='사용자 · Codex';PrivateProfile=$profile;SyntheticReader=$true;ActualDebuggerAttached=$false;ProductSendOrAi=$false;WindowShown=$false;PopupOpened=$false;OsInputUsed=$false;NetworkUsed=$false;Status='running'}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$stdout=Join-Path $output 'stdout.log';$stderr=Join-Path $output 'stderr.log';$arguments='"'+$dllCopy+'" "'+$dependencyPath+'" "'+$output+'"'
$process=Start-Process -FilePath $runner -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try{$null=$process.Handle;if(!$process.WaitForExit(60000)){$process.Kill();$process.WaitForExit();throw 'C8 actual UI capture exceeded 60 seconds.'};$process.WaitForExit();$exitCode=$process.ExitCode}finally{$process.Dispose()}
if(Test-Path -LiteralPath $stderr){Get-Content -LiteralPath $stderr -Encoding UTF8}
$resultPath=Join-Path $output 'capture-result.json';if(!(Test-Path -LiteralPath $resultPath) -or $exitCode -ne 0){throw "C8 capture failed with exit $exitCode; see $output"}
$result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8|ConvertFrom-Json;if($result.Status -ne 'PASS'){throw 'C8 debugging helper checks failed.'}
$forbidden=@('ygree',$env:USERNAME);foreach($image in $result.Captures){$path=Join-Path $output $image;if(!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -lt 5000){throw "Missing rendered image: $image"}}
$jsonText=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8;foreach($secret in $forbidden){if($secret -and $jsonText.IndexOf($secret,[StringComparison]::OrdinalIgnoreCase) -ge 0){throw 'Capture result contains a private Windows username.'}}
if((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $initialDll -or (Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash -ne $initialAssets){throw 'Frozen C8 DLL/assets changed during capture.'}
$manifest.Status='PASS';$manifest.ResultPath=$resultPath;$manifest.Captures=$result.Captures;$manifest.Checks=$result.Checks;$manifest.PngSha256=@{};foreach($image in $result.Captures){$manifest.PngSha256[$image]=(Get-FileHash -LiteralPath (Join-Path $output $image) -Algorithm SHA256).Hash};$manifest.ProductDllSha256After=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash;$manifest.AssetsSha256After=(Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash
$manifest|ConvertTo-Json -Depth 7|Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Write-Output ("C8 exact DLL debugging helper: {0}; checks {1}; images {2}; output {3}" -f $result.Status,$result.Checks.Count,$result.Captures.Count,$output)
