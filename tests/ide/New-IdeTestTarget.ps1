#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : New-IdeTestTarget
desc         : 실행별 격리 폴더에 해당 IDE SDK/아키텍처의 테스트 패키지 프로젝트를 만든다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 VS2017/2019/2022 실제 호스트 검증 분리.
-------------------------------------------------------------------------
#>
function New-IdeTestTarget {
    param([int]$Major,[string]$Directory)
    $null=New-Item -ItemType Directory -Path $Directory -Force
    [xml]$project=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'IdeIntegration.csproj'))
    $ns=New-Object Xml.XmlNamespaceManager($project.NameTable);$ns.AddNamespace('m',$project.DocumentElement.NamespaceURI)
    foreach($node in $project.SelectNodes('//m:Compile[@Include]',$ns)){$node.SetAttribute('Include',[IO.Path]::GetFullPath((Join-Path $PSScriptRoot $node.Include)))}
    if($Major -eq 17){
        $project.SelectSingleNode('//m:TargetFrameworkVersion',$ns).InnerText='v4.7.2'
        $versions=@{'Microsoft.VisualStudio.Shell.15.0'='17.0.32112.339';'Microsoft.VisualStudio.Threading'='17.0.64';'Microsoft.VSSDK.BuildTools'='17.0.5241'}
        foreach($package in @($project.SelectNodes('//m:PackageReference',$ns))){
            if($versions.ContainsKey($package.Include)){$package.SetAttribute('Version',$versions[$package.Include])}
            if($package.Include -eq 'EnvDTE'){$null=$package.ParentNode.RemoveChild($package)}
        }
        $validation=$project.CreateElement('PackageReference',$project.DocumentElement.NamespaceURI)
        $validation.SetAttribute('Include','Microsoft.VisualStudio.Validation');$validation.SetAttribute('Version','17.0.34');$validation.SetAttribute('ExcludeAssets','runtime')
        $null=$project.SelectSingleNode('//m:PackageReference',$ns).ParentNode.AppendChild($validation)
    }
    [xml]$manifest=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'source.extension.vsixmanifest'))
    $mn=New-Object Xml.XmlNamespaceManager($manifest.NameTable);$mn.AddNamespace('v',$manifest.DocumentElement.NamespaceURI)
    $range='['+$Major+'.0,'+($Major+1)+'.0)'
    $target=$manifest.SelectSingleNode('//v:InstallationTarget',$mn);$target.SetAttribute('Version',$range)
    if($Major -eq 17){$architecture=$manifest.CreateElement('ProductArchitecture',$manifest.DocumentElement.NamespaceURI);$architecture.InnerText='amd64';$null=$target.AppendChild($architecture)}
    $manifest.SelectSingleNode('//v:Prerequisite',$mn).SetAttribute('Version',$range)
    if($Major -eq 17){$manifest.SelectSingleNode('//v:Dependency',$mn).SetAttribute('Version','[4.7.2,)')}
    $settings=New-Object Xml.XmlWriterSettings;$settings.Encoding=New-Object Text.UTF8Encoding($true);$settings.Indent=$true
    $projectPath=Join-Path $Directory 'IdeIntegration.csproj'
    $writer=[Xml.XmlWriter]::Create($projectPath,$settings);try{$project.Save($writer)}finally{$writer.Dispose()}
    $writer=[Xml.XmlWriter]::Create((Join-Path $Directory 'source.extension.vsixmanifest'),$settings);try{$manifest.Save($writer)}finally{$writer.Dispose()}
    return $projectPath
}
