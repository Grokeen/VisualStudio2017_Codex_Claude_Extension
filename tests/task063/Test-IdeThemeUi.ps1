#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[Parameter(Mandatory=$true)][string]$AssetsPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
if([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Run in Windows PowerShell 5.1 -STA.'}
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
$AssetsPath=(Resolve-Path -LiteralPath $AssetsPath).Path
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Use a new evidence directory.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $name}
$assets=Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$target=$assets.targets.PSObject.Properties|Where-Object{$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'}|Select-Object -First 1
$references=@{}
foreach($entry in $target.Value.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$references[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}}}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;if($references.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($references[$name])};return $null}
$beforeSettings=$env:VS2017_AI_SETTINGS_PATH;$beforeWorkspace=$env:VS2017_AI_WORKSPACE_PATH
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json'
    $env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true}',[Text.UTF8Encoding]::new($false))
    [Reflection.Assembly]::LoadFrom($AssemblyPath)|Out-Null
    if($references.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){[Reflection.Assembly]::LoadFrom($references['Microsoft.VisualStudio.Shell.15.0'])|Out-Null}
    $refs=@($AssemblyPath,'System.dll','System.Core.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'IdeThemeUiChecks.cs') -ReferencedAssemblies $refs
    try{[IdeThemeUiChecks]::Run()}catch{Write-Error $_.Exception.ToString();throw}
    [pscustomobject]@{Succeeded=$true;AssemblySha256=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash;WindowShown=$false;InstallerLaunched=$false;CurrentIdeThemeChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
}finally{$env:VS2017_AI_SETTINGS_PATH=$beforeSettings;$env:VS2017_AI_WORKSPACE_PATH=$beforeWorkspace;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
