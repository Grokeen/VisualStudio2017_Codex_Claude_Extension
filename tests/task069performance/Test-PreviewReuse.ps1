#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-PreviewReuse
desc         : 고정 제품 DLL의 반복 Loaded 비용과 문서 재사용 의미를 무창 STA에서 검증한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : TASK069 baseline/수정후 분리·불변 입력·원본 증거 보존.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$AssemblyPath,
    [Parameter(Mandatory=$true)][string]$AssetsPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [ValidateSet('baseline','reuse')][string]$Mode='reuse'
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Windows PowerShell 5.1 -STA required.'}
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Preserve previous performance evidence.'}
$null=New-Item -ItemType Directory -Path $output
$original=(Resolve-Path -LiteralPath $AssemblyPath).Path;$assetsPath=(Resolve-Path -LiteralPath $AssetsPath).Path
$productHash=(Get-FileHash -LiteralPath $original).Hash;$assetsHash=(Get-FileHash -LiteralPath $assetsPath).Hash
$copy=Join-Path $output 'VS2017.AIAssistant.dll';Copy-Item -LiteralPath $original -Destination $copy
$source=Join-Path $PSScriptRoot 'PreviewPerformanceChecks.cs';$sourceCopy=Join-Path $output 'PreviewPerformanceChecks.cs';Copy-Item -LiteralPath $source -Destination $sourceCopy
$assets=Get-Content -LiteralPath $assetsPath -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$framework=$assets.targets.PSObject.Properties|Where-Object {$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'}|Select-Object -First 1
$dependencies=@{}
foreach($entry in $framework.Value.PSObject.Properties){foreach($kind in @('compile','runtime')){if($entry.Value.PSObject.Properties.Name -contains $kind){foreach($part in $entry.Value.$kind.PSObject.Properties.Name){if($part -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($part)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$part)}}}}}
foreach($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $original) -Filter '*.dll' -File){if($sibling.Name -ne 'VS2017.AIAssistant.dll'){$dependencies[$sibling.BaseName]=$sibling.FullName}}
$map=Join-Path $output 'dependencies.json';$dependencies|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $map -Encoding UTF8
$compiler='C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$references='C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
$exe=Join-Path $output 'PreviewPerformanceChecks.exe'
$arguments=@('/nologo','/target:exe','/langversion:7',('/out:'+$exe),('/r:'+$copy))
foreach($name in @('System','System.Core','System.Xml','System.Web.Extensions','WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){$arguments+=('/r:'+(Join-Path $references ($name+'.dll')))}
$arguments+=$sourceCopy
& $compiler @arguments 2>&1|Tee-Object (Join-Path $output 'compile.log')
if($LASTEXITCODE -ne 0){throw 'Private performance harness compile failed.'}
[ordered]@{AssemblyPath=$original;AssemblySha256=$productHash;AssetsPath=$assetsPath;AssetsSha256=$assetsHash;SourceSha256=(Get-FileHash -LiteralPath $sourceCopy).Hash;DriverSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash;Mode=$Mode}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'inputs.json') -Encoding UTF8
$variables=@('VS2017_AI_SETTINGS_PATH','VS2017_AI_WORKSPACE_PATH','VS2017_AI_DISABLE_CLI_DISCOVERY','VS2017_AI_DISABLE_RUNTIME_DISCOVERY');$prior=@{}
foreach($name in $variables){$prior[$name]=[Environment]::GetEnvironmentVariable($name)}
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $output 'settings.json';$env:VS2017_AI_WORKSPACE_PATH=Join-Path $output 'workspace';$env:VS2017_AI_DISABLE_CLI_DISCOVERY='1';$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":false,"ThemeMode":"light"}',(New-Object Text.UTF8Encoding($false)))
    $result=Join-Path $output 'result.json';$argumentLine='"'+$copy+'" "'+$map+'" "'+$result+'" "'+$output+'" "'+$Mode+'" "'+$productHash+'"'
    $child=Start-Process -FilePath $exe -ArgumentList $argumentLine -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    try{$null=$child.Handle;if(!$child.WaitForExit(60000)){$child.Kill();$child.WaitForExit();throw 'Performance fixture exceeded its execution bound.'};$child.WaitForExit();$exitCode=$child.ExitCode}finally{$child.Dispose()}
    if(Test-Path -LiteralPath $result){Get-Content -LiteralPath $result -Raw -Encoding UTF8}
    Get-Content -LiteralPath (Join-Path $output 'stderr.log') -Encoding UTF8
    if((Get-FileHash -LiteralPath $original).Hash -ne $productHash -or (Get-FileHash -LiteralPath $copy).Hash -ne $productHash -or (Get-FileHash -LiteralPath $assetsPath).Hash -ne $assetsHash){throw 'Fixed performance inputs changed.'}
    if($null -eq $exitCode -or $exitCode -ne 0){throw 'Performance meaning contract failed; preserve raw evidence.'}
}finally{foreach($name in $variables){[Environment]::SetEnvironmentVariable($name,$prior[$name])}}
