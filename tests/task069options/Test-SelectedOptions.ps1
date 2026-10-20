#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-SelectedOptions
desc         : 고정 실제 DLL의 모델/강도/속도 UI 선택을 개인 native wire로 검증한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK069 원본 불변·실패 원문 보존·계정/원격AI/GUI 없음.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$AssetsPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$SchemaDirectory
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Windows PowerShell 5.1 -STA required.'}
$destination=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $destination){throw 'Preserve original evidence; choose a new directory.'}
$null=New-Item -ItemType Directory -Path $destination
$original=(Resolve-Path -LiteralPath $AssemblyPath).Path
$exactAssets=(Resolve-Path -LiteralPath $AssetsPath).Path
$productHash=(Get-FileHash -LiteralPath $original).Hash;$assetsHash=(Get-FileHash -LiteralPath $exactAssets).Hash
$copy=Join-Path $destination ([IO.Path]::GetFileName($original));Copy-Item -LiteralPath $original -Destination $copy
$sourceRecords=@()
foreach($name in @('OptionsWireFixture.cs','OptionsChecks.cs','OptionsRunner.cs')){
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
$fixture=Join-Path $destination 'OptionsWireFixture.exe'
& $compiler /nologo /target:exe /langversion:7 /reference:System.Web.Extensions.dll ('/out:'+$fixture) (Join-Path $destination 'OptionsWireFixture.cs') 2>&1|Tee-Object (Join-Path $destination 'fixture.compile.log')
if($LASTEXITCODE -ne 0){throw 'Private native fixture compilation failed.'}
$runner=Join-Path $destination 'OptionsRunner.exe'
$arguments=@('/nologo','/target:exe','/langversion:7',('/out:'+$runner),('/r:'+$copy))
foreach($name in @('System','System.Core','System.Xml','System.Web.Extensions','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$arguments+=('/r:'+(Join-Path $referenceRoot ($name+'.dll')))}
$arguments+=(Join-Path $destination 'OptionsChecks.cs'),(Join-Path $destination 'OptionsRunner.cs')
& $compiler @arguments 2>&1|Tee-Object (Join-Path $destination 'runner.compile.log')
if($LASTEXITCODE -ne 0){throw 'Private WPF option checks compilation failed.'}
$schemaRecords=@()
if($SchemaDirectory){foreach($name in @('ThreadStartParams','TurnStartParams')){
    $path=Join-Path $SchemaDirectory ('v2/'+$name+'.json')
    $schema=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    $requiredFields=if($name -eq 'ThreadStartParams'){@('model','cwd','approvalPolicy','sandbox','ephemeral')}else{@('threadId','input','effort','serviceTier','approvalPolicy')}
    foreach($field in $requiredFields){if($schema.properties.PSObject.Properties.Name -notcontains $field){throw ('Installed CLI schema does not advertise field '+$name+'.'+$field)}}
    $schemaRecords+=[ordered]@{Name=$name;Path=[IO.Path]::GetFullPath($path);Sha256=(Get-FileHash -LiteralPath $path).Hash;Fields=$requiredFields}
}}
[ordered]@{AssemblyPath=$original;AssemblySha256=$productHash;AssetsPath=$exactAssets;AssetsSha256=$assetsHash;Sources=$sourceRecords;FixtureSha256=(Get-FileHash -LiteralPath $fixture).Hash;LocalSchemaFieldContracts=$schemaRecords}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $destination 'inputs.json') -Encoding UTF8
$variables=@('VS2017_AI_SETTINGS_PATH','VS2017_AI_WORKSPACE_PATH','VS2017_AI_MODELS_CACHE_PATH','VS2017_AI_DISABLE_CLI_DISCOVERY','VS2017_AI_DISABLE_RUNTIME_DISCOVERY','TASK069_OPTIONS_WIRE');$previous=@{}
foreach($name in $variables){$previous[$name]=[Environment]::GetEnvironmentVariable($name)}
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $destination 'bootstrap-settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $destination 'bootstrap-workspace'
    $env:VS2017_AI_DISABLE_CLI_DISCOVERY='1';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    $env:VS2017_AI_MODELS_CACHE_PATH=Join-Path $destination 'models-cache.json'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":false}',(New-Object Text.UTF8Encoding($false)))
    [ordered]@{models=@([ordered]@{slug='gpt-6-luna';display_name='GPT-6 Luna synthetic capability';visibility='list';priority=1;default_reasoning_level='high';supported_reasoning_levels=@(@{effort='low';description='Low'},@{effort='medium';description='Medium'},@{effort='high';description='High'});additional_speed_tiers=@('fast')})}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $env:VS2017_AI_MODELS_CACHE_PATH -Encoding UTF8
    $result=Join-Path $destination 'result.json'
    $argumentLine='"'+$copy+'" "'+$map+'" "'+$result+'" "'+$destination+'" "'+$fixture+'" "'+$productHash+'"'
    $child=Start-Process -FilePath $runner -ArgumentList $argumentLine -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $destination 'stdout.log') -RedirectStandardError (Join-Path $destination 'stderr.log')
    try{$null=$child.Handle;if(!$child.WaitForExit(180000)){$child.Kill();$child.WaitForExit();throw 'Options checks exceeded bounded execution.'};$child.WaitForExit();$exitCode=$child.ExitCode}finally{$child.Dispose()}
    if(Test-Path -LiteralPath $result){Get-Content -LiteralPath $result -Raw -Encoding UTF8}
    Get-Content -LiteralPath (Join-Path $destination 'stderr.log') -Encoding UTF8
    if((Get-FileHash -LiteralPath $original).Hash -ne $productHash -or (Get-FileHash -LiteralPath $exactAssets).Hash -ne $assetsHash){throw 'Fixed product/asset inputs changed.'}
    if($null -eq $exitCode -or $exitCode -ne 0){throw 'Selected option checks failed; preserve raw evidence.'}
}finally{foreach($name in $variables){[Environment]::SetEnvironmentVariable($name,$previous[$name])}}
