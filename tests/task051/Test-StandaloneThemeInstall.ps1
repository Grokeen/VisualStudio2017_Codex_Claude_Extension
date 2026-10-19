#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-StandaloneThemeInstall
desc         : AI 확장 없이 독립 테마만 전용 VS RootSuffix에 설치·구성하고 생성된 hive를 KEY_READ로 확인한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : GUI/자동로드 조건과 설치·구성 결과를 분리하며 사용자 테마·프로젝트는 변경하지 않는다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$VisualStudioPath,[Parameter(Mandatory=$true)][string]$ThemeVsixPath,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$installation=(Get-Item -LiteralPath $VisualStudioPath).FullName
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Preserve existing theme install evidence.'}
$null=New-Item -ItemType Directory -Path $output
$vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$instances=& $vswhere -all -products '*' -format json -utf8 | ConvertFrom-Json
$instance=$instances | Where-Object installationPath -eq $installation | Select-Object -First 1
if(!$instance){throw 'Exact Visual Studio instance not found.'}
$major=([version]$instance.installationVersion).Major
$year=switch($major){15{'2017'}16{'2019'}17{'2022'}default{throw 'Only VS2017/2019/2022 are supported.'}}
$theme=(Get-Item -LiteralPath $ThemeVsixPath).FullName;$themeHash=(Get-FileHash -LiteralPath $theme).Hash
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip=[IO.Compression.ZipFile]::OpenRead($theme)
try{
    if(@($zip.Entries | Where-Object FullName -match '(?i)\.(dll|exe|ps1|bat|cmd)$').Count){throw 'Expected a pure data-only theme package.'}
    $stream=$zip.GetEntry('extension.vsixmanifest').Open();$settings=New-Object Xml.XmlReaderSettings;$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null;$reader=[Xml.XmlReader]::Create($stream,$settings)
    try{$manifest=New-Object Xml.XmlDocument;$manifest.XmlResolver=$null;$manifest.Load($reader)}finally{$reader.Dispose();$stream.Dispose()}
    $ns=New-Object Xml.XmlNamespaceManager($manifest.NameTable);$ns.AddNamespace('v',$manifest.DocumentElement.NamespaceURI)
    if($manifest.SelectSingleNode('//v:Identity',$ns).GetAttribute('Id') -ne ('Grokeen.VisualStudioThemes.'+$year)){throw 'Theme identity does not match the selected IDE.'}
    $categoryCounts=@(foreach($key in @('Kakao','Dracula')){$textReader=New-Object IO.StreamReader($zip.GetEntry('Themes/'+$key+'.pkgdef').Open());try{$raw=$textReader.ReadToEnd();[regex]::Matches($raw,'(?m)^\[\$RootKey\$\\Themes\\').Count}finally{$textReader.Dispose()}})
    if($categoryCounts[0] -le 0 -or $categoryCounts[0] -ne $categoryCounts[1]){throw 'Compiled theme category coverage differs.'}
}finally{$zip.Dispose()}
$rootSuffix='ThemeProbe51'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$installer=Join-Path $installation 'Common7/IDE/VSIXInstaller.exe';$devenv=Join-Path $installation 'Common7/IDE/devenv.exe'
$process=Start-Process -FilePath $installer -ArgumentList ('/quiet /rootSuffix:'+$rootSuffix+' /instanceIds:'+$instance.instanceId+' "'+$theme+'"') -WindowStyle Hidden -PassThru -Wait
try{$installExit=$process.ExitCode;if($installExit -ne 0){throw ('Independent theme install failed: '+$installExit)}}finally{$process.Dispose()}
$update=Start-Process -FilePath $devenv -ArgumentList ('/RootSuffix '+$rootSuffix+' /UpdateConfiguration /Log "'+(Join-Path $output 'configure.xml')+'"') -WindowStyle Hidden -PassThru -Wait
try{$configureExit=$update.ExitCode;if($configureExit -ne 0){throw 'Dedicated theme configuration update failed.'}}finally{$update.Dispose()}
$probeProfileName=$major.ToString()+'.0_'+$instance.instanceId+$rootSuffix
$hive=Join-Path $env:LOCALAPPDATA ('Microsoft/VisualStudio/'+$probeProfileName+'/privateregistry.bin')
Add-Type -Path (Join-Path $PSScriptRoot 'PrivateThemeHiveChecks.cs')
$data=[PrivateThemeHiveChecks]::Read($hive,('Software\Microsoft\VisualStudio\'+$probeProfileName+'_Config'),$categoryCounts[0])
if((Get-FileHash -LiteralPath $theme).Hash -ne $themeHash){throw 'Fixed theme VSIX changed.'}
$result=[pscustomobject]@{Succeeded=$data.Succeeded;Year=$year;InstallationVersion=$instance.installationVersion;RootSuffix=$rootSuffix;ThemeVsix=$theme;ThemeSha256=$themeHash;OfficialInstallerExitCode=$installExit;IdeConfigurationExitCode=$configureExit;Themes=$data.Themes;AiExtensionInstalledByHarness=$false;AiExtensionRegistered=$data.AiExtensionRegistered;RegistryAccess='KEY_READ';GlobalHiveMounted=$false;SelectedThemeChanged=$false;ActualIdeRuntimeTest=$false;VisualSwitchVerified=$false;CheckedAt=[DateTimeOffset]::Now.ToString('o')}
$result|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
$result|ConvertTo-Json -Depth 6
