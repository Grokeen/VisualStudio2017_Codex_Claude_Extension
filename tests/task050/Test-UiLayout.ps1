#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath,[string]$OutputDirectory,[string]$AssetsPath)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA'){throw 'Use Windows PowerShell 5.1 -STA.'}
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path
if(!$OutputDirectory){$OutputDirectory=Join-Path $repository ('artifacts/task050/ui-layout-'+[Guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if((Test-Path -LiteralPath $OutputDirectory) -and @(Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Use a fresh output directory; existing evidence is preserved.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')){Add-Type -AssemblyName $name}
if([string]::IsNullOrWhiteSpace($AssetsPath)){$AssetsPath=Join-Path $repository 'src/VS2017.AIAssistant/obj/project.assets.json'}
$assets=Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8|ConvertFrom-Json
$packageRoot=$assets.packageFolders.PSObject.Properties.Name|Select-Object -First 1
$frameworkTarget=$assets.targets.PSObject.Properties|Where-Object{$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'}|Select-Object -First 1
$references=@{}
foreach($entry in $frameworkTarget.Value.PSObject.Properties){if($entry.Value.PSObject.Properties.Name -contains 'compile'){foreach($item in $entry.Value.compile.PSObject.Properties.Name){if($item -like '*.dll'){$references[[IO.Path]::GetFileNameWithoutExtension($item)]=Join-Path $packageRoot ($assets.libraries.($entry.Name).path+'/'+$item)}}}}
$resolve=[ResolveEventHandler]{param($sender,$eventArgs)$name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name;if($references.ContainsKey($name)){return [Reflection.Assembly]::LoadFrom($references[$name])};return $null}
$oldSettings=$env:VS2017_AI_SETTINGS_PATH;$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH
$assemblyHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
try{
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json'
    $env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"ThemeMode":"dark","VisualThemePreset":"vs","EnableCompletionNotifications":true,"HideWelcome":true,"ShowHistory":false}',[Text.UTF8Encoding]::new($false))
    [Reflection.Assembly]::LoadFrom($AssemblyPath)|Out-Null
    if($references.ContainsKey('Microsoft.VisualStudio.Shell.15.0')){[Reflection.Assembly]::LoadFrom($references['Microsoft.VisualStudio.Shell.15.0'])|Out-Null}
    $compileRefs=@($AssemblyPath,'System.dll','System.Core.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'UiLayoutChecks.cs') -ReferencedAssemblies $compileRefs
    [UiLayoutChecks]::Run()
    $result=[pscustomobject]@{Succeeded=[UiLayoutChecks]::Errors.Count -eq 0;Passed=[UiLayoutChecks]::Checks.Count;Failed=[UiLayoutChecks]::Errors.Count;Checks=@([UiLayoutChecks]::Checks);Errors=@([UiLayoutChecks]::Errors);AssemblyPath=$AssemblyPath;AssemblySha256=$assemblyHash;WindowShown=$false;VisualStudioThemeChanged=$false;PersonalCredentialsRead=$false;NetworkRequested=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
    $result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $result|ConvertTo-Json -Depth 6
    if((Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash -ne $assemblyHash){throw 'The fixed assembly changed during UI checks.'}
    if(!$result.Succeeded){throw "UI layout checks failed: $OutputDirectory"}
}finally{$env:VS2017_AI_SETTINGS_PATH=$oldSettings;$env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace;[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
