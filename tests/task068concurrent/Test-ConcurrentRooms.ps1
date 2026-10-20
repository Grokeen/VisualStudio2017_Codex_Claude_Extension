#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ConcurrentRooms
desc         : 고정 실제 제품 DLL을 RuntimeEditProvider·개인 Claude EXE로 무창 독립검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK068 source사본/해시/의존성 고정·개인 빌드·원본 실패 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$AssetsPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Windows PowerShell 5.1 -STA required.'}
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $destination){throw 'Preserve original evidence; choose a new directory.'}
$null=New-Item -ItemType Directory -Path $destination
$original=[IO.Path]::GetFullPath($AssemblyPath);$exactAssets=[IO.Path]::GetFullPath($AssetsPath)
$productHash=(Get-FileHash -LiteralPath $original).Hash;$assetsHash=(Get-FileHash -LiteralPath $exactAssets).Hash
$copy=Join-Path $destination ([IO.Path]::GetFileName($original));Copy-Item -LiteralPath $original -Destination $copy
$sourceRecords=@()
foreach($name in @('ConcurrentClaudeFixture.cs','ConcurrentRoomChecks.cs','ConcurrentRoomRunner.cs')){
    $source=Join-Path $PSScriptRoot $name;$target=Join-Path $destination $name;Copy-Item -LiteralPath $source -Destination $target
    $sourceRecords+=[ordered]@{Path=$source;Sha256=(Get-FileHash -LiteralPath $source).Hash}
}
$assets=Get-Content -LiteralPath $exactAssets -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$framework=$assets.targets.PSObject.Properties|Where-Object {$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'}|Select-Object -First 1
$dependencies=@{}
foreach($entry in $framework.Value.PSObject.Properties){foreach($kind in @('compile','runtime')){if($entry.Value.PSObject.Properties.Name -contains $kind){foreach($part in $entry.Value.$kind.PSObject.Properties.Name){if($part -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($part)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$part)}}}}}
foreach($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $original) -Filter '*.dll' -File){if($sibling.Name -ne [IO.Path]::GetFileName($original)){$dependencies[$sibling.BaseName]=$sibling.FullName}}
$map=Join-Path $destination 'dependencies.json';$dependencies|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $map -Encoding UTF8
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$referenceRoot='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$fixture=Join-Path $destination 'ConcurrentClaudeFixture.exe'
& $compiler /nologo /target:exe /langversion:7 /reference:System.Web.Extensions.dll ('/out:'+$fixture) (Join-Path $destination 'ConcurrentClaudeFixture.cs') 2>&1|Tee-Object (Join-Path $destination 'fixture.compile.log')
if($LASTEXITCODE -ne 0){throw 'Private CLI fixture compilation failed.'}
$runner=Join-Path $destination 'ConcurrentRoomRunner.exe'
$arguments=@('/nologo','/target:exe','/langversion:7',('/out:'+$runner),('/r:'+$copy))
foreach($name in @('System','System.Core','System.Xml','System.Web.Extensions','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$arguments+=('/r:'+(Join-Path $referenceRoot ($name+'.dll')))}
$arguments+=(Join-Path $destination 'ConcurrentRoomChecks.cs'),(Join-Path $destination 'ConcurrentRoomRunner.cs')
& $compiler @arguments 2>&1|Tee-Object (Join-Path $destination 'runner.compile.log')
if($LASTEXITCODE -ne 0){throw 'Private WPF runner compilation failed.'}
[ordered]@{AssemblyPath=$original;AssemblySha256=$productHash;AssetsPath=$exactAssets;AssetsSha256=$assetsHash;Sources=$sourceRecords;FixtureSha256=(Get-FileHash -LiteralPath $fixture).Hash}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $destination 'inputs.json') -Encoding UTF8
$variables=@('VS2017_AI_SETTINGS_PATH','VS2017_AI_WORKSPACE_PATH','VS2017_AI_DISABLE_CLI_DISCOVERY','VS2017_AI_DISABLE_RUNTIME_DISCOVERY','TASK068_CONCURRENT_FIXTURE','TASK068_CONCURRENT_GATES');$previous=@{}
foreach($name in $variables){$previous[$name]=[Environment]::GetEnvironmentVariable($name)}
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $destination 'bootstrap-settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $destination 'bootstrap-workspace'
    $env:VS2017_AI_DISABLE_CLI_DISCOVERY='1';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1';$env:TASK068_CONCURRENT_FIXTURE=$fixture
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":false}',[Text.UTF8Encoding]::new($false))
    $result=Join-Path $destination 'result.json'
    $argumentLine='"'+$copy+'" "'+$map+'" "'+$result+'" "'+$destination+'" "'+$productHash+'"'
    $child=Start-Process -FilePath $runner -ArgumentList $argumentLine -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $destination 'stdout.log') -RedirectStandardError (Join-Path $destination 'stderr.log')
    try{$null=$child.Handle;if(!$child.WaitForExit(120000)){$child.Kill();$child.WaitForExit();throw 'Concurrent fixture exceeded its bounded two-minute run.'};$child.WaitForExit();$exitCode=$child.ExitCode}finally{$child.Dispose()}
    if(Test-Path -LiteralPath $result){Get-Content -LiteralPath $result -Raw -Encoding UTF8}
    Get-Content -LiteralPath (Join-Path $destination 'stderr.log') -Encoding UTF8
    if((Get-FileHash -LiteralPath $original).Hash -ne $productHash -or (Get-FileHash -LiteralPath $exactAssets).Hash -ne $assetsHash){throw 'Fixed product/asset inputs changed.'}
    if($null -eq $exitCode -or $exitCode -ne 0){throw 'Actual concurrent room checks failed; preserve raw evidence.'}
}finally{foreach($name in $variables){[Environment]::SetEnvironmentVariable($name,$previous[$name])}}
