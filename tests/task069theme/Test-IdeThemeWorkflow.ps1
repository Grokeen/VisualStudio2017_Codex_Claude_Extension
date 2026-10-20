#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-IdeThemeWorkflow
desc         : 새 전용 IDE 프로필에서 설치된 제품의 테마 발견·적용·export를 검증한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : PrepareOnly는 IDE를 시작하지 않으며 실제 실행은 Root 예약 후에만 허용한다.
               2026-10-20 CodexOrig : TASK069 전용69 suffix·정의/하네스 고정 입력과 실제 palette 검사 연결.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$VisualStudioPath,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ProductVsixPath,
    [string]$ThemeVsixPath,
    [switch]$PrepareOnly,
    [switch]$ProbeOnly,
    [switch]$SentinelSettings,
    [ValidateRange(30,600)][int]$TimeoutSeconds=180
)
$ErrorActionPreference='Stop'
$installation=(Get-Item -LiteralPath $VisualStudioPath).FullName
$output=[IO.Path]::GetFullPath($OutputDirectory)
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$allowed=Join-Path $repository 'artifacts/task069theme'
if(!$output.StartsWith($allowed+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output must be a new task069theme artifact directory.'}
if(Test-Path -LiteralPath $output){throw 'Preserve existing evidence; choose a new directory.'}
$null=New-Item -ItemType Directory -Path $output
$fixedInputs=@()
foreach($inputFile in @($PSCommandPath,(Join-Path $PSScriptRoot 'ThemeIdePackage.cs'),(Join-Path $PSScriptRoot 'ThemeRegistrationChecks64.cs'),(Join-Path $repository 'tests/ide/New-IdeTestTarget.ps1'),(Join-Path $repository 'tests/ide/IdeIntegration.csproj'),(Join-Path $repository 'tests/ide/source.extension.vsixmanifest'))){
    $copy=Join-Path $output ([IO.Path]::GetFileName($inputFile));Copy-Item -LiteralPath $inputFile -Destination $copy
    $hash=(Get-FileHash -LiteralPath $inputFile).Hash
    if((Get-FileHash -LiteralPath $copy).Hash -ne $hash){throw 'Immutable source input copy mismatch.'}
    $fixedInputs+=[pscustomobject]@{Path=$inputFile;Copy=$copy;SHA256=$hash}
}
$definitionDirectory=Join-Path $output 'definitions';$null=New-Item -ItemType Directory -Path $definitionDirectory
foreach($themeName in @('Kakao','Dracula')){
    $inputFile=Join-Path $repository ('artifacts/task069/themes-final1/definitions/'+$themeName+'.vstheme');$copy=Join-Path $definitionDirectory ($themeName+'.vstheme');Copy-Item -LiteralPath $inputFile -Destination $copy
    $hash=(Get-FileHash -LiteralPath $inputFile).Hash;if((Get-FileHash -LiteralPath $copy).Hash -ne $hash){throw 'Immutable theme definition copy mismatch.'}
    $fixedInputs+=[pscustomobject]@{Path=$inputFile;Copy=$copy;SHA256=$hash}
}
$fixedInputs|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $output 'fixed-inputs-before.json') -Encoding UTF8
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$instances=& $vswhere -all -products '*' -format json -utf8 | ConvertFrom-Json
$instance=$instances | Where-Object installationPath -eq $installation | Select-Object -First 1
if(!$instance){throw 'Exact Visual Studio instance not found.'}
$major=([version]$instance.installationVersion).Major
$year=switch($major){15{'2017'}16{'2019'}17{'2022'}default{throw 'Unsupported IDE.'}}
$msbuild=Join-Path $installation $(if($major -eq 15){'MSBuild/15.0/Bin/MSBuild.exe'}else{'MSBuild/Current/Bin/MSBuild.exe'})
. (Join-Path $repository 'tests/ide/New-IdeTestTarget.ps1')
$projectPath=New-IdeTestTarget -Major $major -Directory (Join-Path $output 'HelperProject')
[xml]$project=[IO.File]::ReadAllText($projectPath)
$ns=New-Object Xml.XmlNamespaceManager($project.NameTable);$ns.AddNamespace('m',$project.DocumentElement.NamespaceURI)
foreach($node in @($project.SelectNodes('//m:Compile',$ns))){$null=$node.ParentNode.RemoveChild($node)}
foreach($node in @($project.SelectNodes('//m:Reference[@Include="VS2017.AIAssistant"]',$ns))){$null=$node.ParentNode.RemoveChild($node)}
$compile=$project.CreateElement('Compile',$project.DocumentElement.NamespaceURI)
$helperSource=Join-Path ([IO.Path]::GetDirectoryName($projectPath)) 'ThemeIdePackage.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ThemeIdePackage.cs') -Destination $helperSource
$compile.SetAttribute('Include',$helperSource)
$null=$project.SelectSingleNode('//m:ItemGroup',$ns).AppendChild($compile)
$paletteSource=Join-Path ([IO.Path]::GetDirectoryName($projectPath)) 'ThemeRegistrationChecks64.cs'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ThemeRegistrationChecks64.cs') -Destination $paletteSource
$paletteCompile=$project.CreateElement('Compile',$project.DocumentElement.NamespaceURI);$paletteCompile.SetAttribute('Include',$paletteSource);$null=$project.SelectSingleNode('//m:ItemGroup',$ns).AppendChild($paletteCompile)
$xmlReference=$project.CreateElement('Reference',$project.DocumentElement.NamespaceURI)
$xmlReference.SetAttribute('Include','System.Xml');$null=$project.SelectSingleNode('//m:ItemGroup',$ns).AppendChild($xmlReference)
$project.SelectSingleNode('//m:AssemblyName',$ns).InnerText='CodexOrig.ThemeIntegration'
$project.SelectSingleNode('//m:RootNamespace',$ns).InnerText='CodexOrig.ThemeIntegration'
$project.SelectSingleNode('//m:ProjectGuid',$ns).InnerText='{EA100064-28D8-4CCA-8C0C-71D08022BA66}'
$settings=New-Object Xml.XmlWriterSettings;$settings.Encoding=New-Object Text.UTF8Encoding($true);$settings.Indent=$true
$writer=[Xml.XmlWriter]::Create($projectPath,$settings);try{$project.Save($writer)}finally{$writer.Dispose()}
$manifestPath=Join-Path ([IO.Path]::GetDirectoryName($projectPath)) 'source.extension.vsixmanifest'
[xml]$manifest=[IO.File]::ReadAllText($manifestPath)
$mn=New-Object Xml.XmlNamespaceManager($manifest.NameTable);$mn.AddNamespace('v',$manifest.DocumentElement.NamespaceURI)
$manifest.SelectSingleNode('//v:Identity',$mn).SetAttribute('Id','CodexOrig.ThemeIntegration.Tests')
$manifest.SelectSingleNode('//v:DisplayName',$mn).InnerText='CodexOrig dedicated theme workflow test'
$manifest.SelectSingleNode('//v:Description',$mn).InnerText='Dedicated ThemeApply64 profile only; never distribute.'
$writer=[Xml.XmlWriter]::Create($manifestPath,$settings);try{$manifest.Save($writer)}finally{$writer.Dispose()}
& $msbuild $projectPath /restore /t:Rebuild /p:Configuration=Release /p:DeployExtension=false /verbosity:minimal /nologo 2>&1 | Tee-Object -FilePath (Join-Path $output 'helper-build.log')
if($LASTEXITCODE -ne 0){throw 'Dedicated helper build failed.'}
$helperVsix=Join-Path ([IO.Path]::GetDirectoryName($projectPath)) 'bin/Release/CodexOrig.ThemeIntegration.vsix'
if($PrepareOnly){[pscustomobject]@{Prepared=$true;Year=$year;HelperVsix=$helperVsix;IdeStarted=$false;ProductInstalled=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'prepared.json') -Encoding UTF8;return}
if(!$ProductVsixPath -or !$ThemeVsixPath){throw 'Both exact fixed product and theme VSIX paths are required.'}
$product=(Get-Item -LiteralPath $ProductVsixPath).FullName;$theme=(Get-Item -LiteralPath $ThemeVsixPath).FullName
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($product)
try{
    $dll=$zip.GetEntry('VS2017.AIAssistant.dll');if(!$dll){throw 'Product DLL entry absent.'}
    $stream=$dll.Open();$sha=[Security.Cryptography.SHA256]::Create()
    try{$dllHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}
    $bundled=$zip.GetEntry(('Themes/'+[IO.Path]::GetFileName($theme)))
    if(!$bundled){throw 'Expected theme package is absent from product VSIX.'}
    $stream=$bundled.Open();$sha=[Security.Cryptography.SHA256]::Create()
    try{$bundledHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}
}finally{$zip.Dispose()}
$themeHash=(Get-FileHash -LiteralPath $theme).Hash
if($bundledHash -ne $themeHash){throw 'Bundled theme package differs from fixed theme package.'}
$rootSuffix='ThemeApply69'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$installer=Join-Path $installation 'Common7/IDE/VSIXInstaller.exe';$devenv=Join-Path $installation 'Common7/IDE/devenv.exe'
$installResults=@()
foreach($package in @($theme,$product,$helperVsix)){
    $process=Start-Process -FilePath $installer -ArgumentList ('/quiet /rootSuffix:'+$rootSuffix+' /instanceIds:'+$instance.instanceId+' "'+$package+'"') -WindowStyle Hidden -PassThru -Wait
    try{$code=$process.ExitCode;$installResults+=[pscustomobject]@{Package=$package;ExitCode=$code};if($code -ne 0){throw ('Official VSIX installer failed: '+$code)}}finally{$process.Dispose()}
}
$installResults|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'installer-results.json') -Encoding UTF8
$process=Start-Process -FilePath $devenv -ArgumentList ('/RootSuffix '+$rootSuffix+' /UpdateConfiguration /Log "'+(Join-Path $output 'configure.xml')+'"') -WindowStyle Hidden -PassThru -Wait
try{if($process.ExitCode -ne 0){throw 'Dedicated IDE configuration update failed.'}}finally{$process.Dispose()}
$configPath=Join-Path $output 'config.json'
$sentinelTemplate=$null
if($SentinelSettings){
    $templateRun=switch($year){'2017'{'candidate8-2017-v2'}'2019'{'candidate8-2019-v1'}'2022'{'candidate8-2022-v1'}}
    $sentinelTemplate=(Get-Item -LiteralPath (Join-Path (Join-Path $repository 'artifacts/task064') ($templateRun+'/kakao.vssettings'))).FullName
    [pscustomobject]@{Path=$sentinelTemplate;SHA256=(Get-FileHash -LiteralPath $sentinelTemplate).Hash;Scope='Only one selected nondefault property is copied into minimal settings XML.'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'sentinel-template.json') -Encoding UTF8
}
[pscustomobject]@{OutputDirectory=$output;ExpectedRegistryRoot=('Software\Microsoft\VisualStudio\'+$major+'.0_'+$instance.instanceId+$rootSuffix);RootSuffix=$rootSuffix;Year=$year;Major=$major.ToString();ExpectedProductSha256=$dllHash;ExpectedThemeSha256=$themeHash;ProbeOnly=$ProbeOnly.IsPresent;SentinelTemplatePath=$sentinelTemplate;DefinitionsDirectory=$definitionDirectory}|ConvertTo-Json|Set-Content -LiteralPath $configPath -Encoding UTF8
@{HideWelcome=$true;FontSize=14;ThemeMode='dark'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'ui-settings.json') -Encoding UTF8
$start=New-Object Diagnostics.ProcessStartInfo
$start.FileName=$devenv;$start.Arguments='/RootSuffix '+$rootSuffix+' /NoSplash /ResetSettings General /Log "'+(Join-Path $output 'activity.xml')+'"'
$start.UseShellExecute=$false;$start.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
$start.EnvironmentVariables['CODEXORIG_THEME069_CONFIG']=$configPath
$start.EnvironmentVariables['VS2017_AI_SETTINGS_PATH']=Join-Path $output 'ui-settings.json'
$start.EnvironmentVariables['VS2017_AI_WORKSPACE_PATH']=Join-Path $output 'workspace'
$start.EnvironmentVariables['VS2017_AI_DISABLE_RUNTIME_DISCOVERY']='1'
$ide=[Diagnostics.Process]::Start($start)
[pscustomobject]@{ProcessId=$ide.Id;RootSuffix=$rootSuffix;ProductVsix=$product;ProductSha256=(Get-FileHash $product).Hash;ThemeSha256=$themeHash;ExpectedDllSha256=$dllHash;StartedAt=[DateTimeOffset]::Now.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'launch.json') -Encoding UTF8
$timer=[Diagnostics.Stopwatch]::StartNew();$resultPath=Join-Path $output 'result.json'
try{
    while(!(Test-Path -LiteralPath $resultPath) -and !$ide.HasExited -and $timer.Elapsed.TotalSeconds -lt $TimeoutSeconds){Start-Sleep -Milliseconds 500}
    if(!(Test-Path -LiteralPath $resultPath)){throw 'Dedicated IDE produced no result before timeout.'}
    $result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8|ConvertFrom-Json
    $result|ConvertTo-Json -Depth 8
    if(!$result.Succeeded){throw 'Actual IDE theme workflow failed; inspect preserved result and logs.'}
}finally{
    $normal=$ide.WaitForExit(10000);$forced=$false
    if(!$normal){
        $owned=Get-CimInstance Win32_Process -Filter ('ProcessId='+$ide.Id)
        if(!$owned -or !$owned.CommandLine.Contains('/RootSuffix '+$rootSuffix+' ')){throw 'Owned IDE command line check failed; no process was killed.'}
        $ide.Kill();if(!$ide.WaitForExit(5000)){throw 'Owned IDE exit not confirmed.'};$forced=$true
    }
    [pscustomobject]@{ProcessId=$ide.Id;RootSuffix=$rootSuffix;ExitedNormally=$normal;ForcedOwnedProcessCleanup=$forced;UserDefaultProfileChanged=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'owned-process-cleanup.json') -Encoding UTF8
    $ide.Dispose()
    $stable=$true;foreach($fixedInput in $fixedInputs){if((Get-FileHash -LiteralPath $fixedInput.Path).Hash -ne $fixedInput.SHA256 -or (Get-FileHash -LiteralPath $fixedInput.Copy).Hash -ne $fixedInput.SHA256){$stable=$false}}
    [pscustomobject]@{InputsUnchanged=$stable;ActualHostPaletteRead=$true;PhysicalPointerUsed=$false;PhysicalVisibilityVerified=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'fixed-inputs-after.json') -Encoding UTF8
    if(!$stable){throw 'Workflow inputs changed during the run.'}
}
