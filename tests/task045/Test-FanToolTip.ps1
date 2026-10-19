#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-FanToolTip
desc         : 고정 제품 DLL의 툴팁 검사를 전용 Windows 실행기로 컴파일·실행한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-045 실제 CLI·계정·개인 설정과 공유 빌드를 사용하지 않는 검사.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
 [Parameter(Mandatory=$true)][string]$AssemblyPath,
 [Parameter(Mandatory=$true)][string]$AssetsPath,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [switch]$PrepareInteractive,
 [ValidateRange(200,260)][double]$Width=260,
 [ValidateSet('light','dark')][string]$ThemeMode='dark'
)
$ErrorActionPreference='Stop'
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$AssetsPath=(Resolve-Path -LiteralPath $AssetsPath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){throw 'Preserve existing test evidence; choose a new output directory.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $name}
$references=@('System.dll','System.Core.dll','System.Web.Extensions.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
$assets=Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$dependencies=@{}
$target=$assets.targets.PSObject.Properties|Select-Object -First 1
foreach($entry in $target.Value.PSObject.Properties){
 if($entry.Value.PSObject.Properties.Name -contains 'compile'){
  foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$dependencies[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}
 }
}
$exe=Join-Path $OutputDirectory 'MenuVerificationHost.exe'
$compiler='C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
$arguments=@('/nologo','/target:winexe','/langversion:7',('/out:'+$exe))+@($references|ForEach-Object{'/reference:'+$_})+@((Join-Path $PSScriptRoot 'FanToolTipChecks.cs'))
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'Tooltip test host compilation failed.'}
$configuration=Join-Path $OutputDirectory 'configuration.json'
[ordered]@{AssemblyPath=$AssemblyPath;ProductSha256=(Get-FileHash -LiteralPath $AssemblyPath).Hash;OutputDirectory=$OutputDirectory;Interactive=[bool]$PrepareInteractive;Dependencies=$dependencies;Width=$Width;ThemeMode=$ThemeMode}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $configuration -Encoding UTF8
if($PrepareInteractive){Write-Output ('Prepared interactive host: '+$exe);Write-Output ('Configuration: '+$configuration);return}
$process=Start-Process -FilePath $exe -ArgumentList ('"'+$configuration+'"') -WindowStyle Hidden -PassThru
try{if(!$process.WaitForExit(60000)){$process.Kill();throw 'Tooltip test exceeded 60 seconds.'};if($process.ExitCode -ne 0){throw ('Tooltip test failed: '+(Join-Path $OutputDirectory 'result.json'))}}finally{$process.Dispose()}
Get-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Raw -Encoding UTF8
