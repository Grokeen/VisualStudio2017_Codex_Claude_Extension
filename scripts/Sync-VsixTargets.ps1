#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Sync-VsixTargets
desc         : 공통 제품 소스를 링크하는 VS2019/2022 전용 프로젝트와 설치 매니페스트를 생성한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 감사 F01, 32비트 SDK15와 64비트 SDK17 분리.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baseDir=Join-Path $repositoryDir 'src\VS2017.AIAssistant'
$utf8=New-Object Text.UTF8Encoding($true)
foreach($target in @('2019','2022')) {
    $directory=Join-Path $repositoryDir ('src\VS'+$target+'.AIAssistant')
    $null=New-Item -ItemType Directory -Path $directory -Force
    [xml]$project=[IO.File]::ReadAllText((Join-Path $baseDir 'VS2017.AIAssistant.csproj'))
    $namespace=New-Object Xml.XmlNamespaceManager($project.NameTable)
    $namespace.AddNamespace('m','http://schemas.microsoft.com/developer/msbuild/2003')
    $major=if($target -eq '2019'){16}else{17}
    $project.SelectSingleNode('//m:ProjectGuid',$namespace).InnerText=if($target -eq '2019'){'{56BC0D5B-F8BC-4CB5-B5B8-60A0A29ED4A9}'}else{'{C2CE87AB-0890-46C4-96E4-8276D8E0AEB1}'}
    $project.SelectSingleNode('//m:MinimumVisualStudioVersion',$namespace).InnerText=($major.ToString()+'.0')
    if($target -eq '2022') {
        $project.SelectSingleNode('//m:TargetFrameworkVersion',$namespace).InnerText='v4.7.2'
        $versions=@{'Microsoft.VisualStudio.Shell.15.0'='17.0.32112.339';'Microsoft.VisualStudio.Threading'='17.0.64';'Microsoft.VSSDK.BuildTools'='17.0.5241'}
        foreach($package in $project.SelectNodes('//m:PackageReference',$namespace)) {
            if($versions.ContainsKey($package.Include)){$package.SetAttribute('Version',$versions[$package.Include])}
            if($package.Include -eq 'EnvDTE'){$null=$package.ParentNode.RemoveChild($package)}
        }
        $packages=$project.SelectSingleNode('//m:PackageReference',$namespace).ParentNode
        $validation=$project.CreateElement('PackageReference',$project.DocumentElement.NamespaceURI)
        $validation.SetAttribute('Include','Microsoft.VisualStudio.Validation')
        $validation.SetAttribute('Version','17.0.34')
        $validation.SetAttribute('ExcludeAssets','runtime')
        $null=$packages.AppendChild($validation)
    }
    # WPF 임시 프로젝트도 NuGet 복원에서 확인한 SDK 경로를 사용한다.
    $sdkVersion=if($target -eq '2022'){'17.0.5241'}else{'15.9.3032'}
    $sdkPath=$project.CreateElement('PropertyGroup',$project.DocumentElement.NamespaceURI)
    $tools=$project.CreateElement('VSToolsPath',$project.DocumentElement.NamespaceURI)
    $tools.InnerText='$(NuGetPackageRoot)microsoft.vssdk.buildtools\'+$sdkVersion+'\tools'
    $tools.SetAttribute('Condition',"'`$(NuGetPackageRoot)' != ''")
    $null=$sdkPath.AppendChild($tools)
    $firstSdkImport=$project.SelectSingleNode('//m:Import[contains(@Project,"Microsoft.VsSDK.targets")]',$namespace)
    $null=$firstSdkImport.ParentNode.InsertBefore($sdkPath,$firstSdkImport)
    foreach($kind in @('Compile','Page','Resource','VSCTCompile','EmbeddedResource','None')) {
        foreach($item in $project.SelectNodes('//m:'+ $kind +'[@Include]',$namespace)) {
            $relative=$item.GetAttribute('Include')
            if($relative -eq 'source.extension.vsixmanifest' -or $relative.StartsWith('$(')) {continue}
            $item.SetAttribute('Include','..\VS2017.AIAssistant\'+$relative)
            if(!$item.SelectSingleNode('m:Link',$namespace)){
                $link=$project.CreateElement('Link',$project.DocumentElement.NamespaceURI)
                $link.InnerText=$relative
                $null=$item.AppendChild($link)
            }
        }
    }
    $settings=New-Object Xml.XmlWriterSettings
    $settings.Encoding=$utf8; $settings.Indent=$true
    $writer=[Xml.XmlWriter]::Create((Join-Path $directory ('VS'+$target+'.AIAssistant.csproj')),$settings)
    try{$project.Save($writer)}finally{$writer.Dispose()}
    [xml]$manifest=[IO.File]::ReadAllText((Join-Path $baseDir 'source.extension.vsixmanifest'))
    $ns=New-Object Xml.XmlNamespaceManager($manifest.NameTable);$ns.AddNamespace('v','http://schemas.microsoft.com/developer/vsx-schema/2011')
    $identity=$manifest.SelectSingleNode('//v:Identity',$ns)
    $identity.SetAttribute('Id','Grokeen.VS'+$target+'.AIAssistant')
    $manifest.SelectSingleNode('//v:DisplayName',$ns).InnerText='Visual Studio AI Assistant '+$target
    $install=$manifest.SelectSingleNode('//v:InstallationTarget',$ns)
    $range='['+$major+'.0,'+($major+1)+'.0)'
    $install.SetAttribute('Version',$range)
    $architecture=$manifest.CreateElement('ProductArchitecture',$manifest.DocumentElement.NamespaceURI)
    $architecture.InnerText=if($target -eq '2022'){'amd64'}else{'x86'}
    if($target -eq '2022'){$null=$install.AppendChild($architecture)}
    $manifest.SelectSingleNode('//v:Prerequisite',$ns).SetAttribute('Version',$range)
    if($target -eq '2022'){$manifest.SelectSingleNode('//v:Dependency',$ns).SetAttribute('Version','[4.7.2,)')}
    $manifest.SelectSingleNode('//v:Description',$ns).InnerText='Claude·Codex와 대화하고 수정안을 비교·승인하거나 명시한 전체 액세스에서 파일 수정·테스트를 실행합니다.'
    $writer=[Xml.XmlWriter]::Create((Join-Path $directory 'source.extension.vsixmanifest'),$settings)
    try{$manifest.Save($writer)}finally{$writer.Dispose()}
    Write-Output ('Linked target ready: '+$target+' / '+$range+' / '+$architecture.InnerText)
}

