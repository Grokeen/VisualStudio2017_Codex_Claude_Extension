#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Sync-VsixTargets
desc         : 공통 제품 소스를 링크하는 VS2019/2022 전용 프로젝트와 설치 매니페스트를 생성한다.
author       : CodexOrig
create date  : 2026-10-18
update date  : 2026-10-18 CodexOrig : TASK-020 감사 F01, 32비트 SDK15와 64비트 SDK17 분리.
               2026-10-19 CodexOrig : TASK-025 공통 프로젝트 내부 Content도 버전별 링크 경로로 연결.
               2026-10-19 CodexOrig : TASK-036 VS2017 15.7 SDK 구버전 패키지를 별도로 생성.
               2026-10-20 CodexOrig : TASK-054 VSIX의 실제 PDF 이름을 런타임 고정 경로와 맞춘다.
               2026-10-20 CodexOrig : TASK069 최신 44쪽 0.14.0 설명서를 네 패키지 공통 리소스로 동기화한다.
               2026-10-21 CodexCode : Role #15 색상 변경 안내 1쪽을 더한 0.14.1 설명서로 동기화한다.
               2026-10-23 CodexOrig : TASK072 Dracula 편집기 개선 안내를 네 패키지에 동기화한다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repositoryDir=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$baseDir=Join-Path $repositoryDir 'src\VS2017.AIAssistant'
$guideSource=Join-Path $repositoryDir 'output\pdf\VisualStudio_AI_Assistant_설치_사용설명서_v0.14.2-preview.1_미리보기.pdf'
$guideDirectory=Join-Path $baseDir 'Assets\CliGuide'
$null=New-Item -ItemType Directory -Path $guideDirectory -Force
Copy-Item -LiteralPath $guideSource -Destination (Join-Path $guideDirectory 'VisualStudio_AI_Assistant_설치_사용설명서.pdf') -Force
$utf8=New-Object Text.UTF8Encoding($true)
foreach($target in @('2017.Legacy','2019','2022')) {
    $legacy=$target -eq '2017.Legacy'
    $directory=Join-Path $repositoryDir ('src\VS'+$target+'.AIAssistant')
    $null=New-Item -ItemType Directory -Path $directory -Force
    [xml]$project=[IO.File]::ReadAllText((Join-Path $baseDir 'VS2017.AIAssistant.csproj'))
    $namespace=New-Object Xml.XmlNamespaceManager($project.NameTable)
    $namespace.AddNamespace('m','http://schemas.microsoft.com/developer/msbuild/2003')
    $major=if($legacy){15}elseif($target -eq '2019'){16}else{17}
    $project.SelectSingleNode('//m:ProjectGuid',$namespace).InnerText=if($legacy){'{FC79C70B-802F-4207-B5B0-13446CC2DDC4}'}elseif($target -eq '2019'){'{56BC0D5B-F8BC-4CB5-B5B8-60A0A29ED4A9}'}else{'{C2CE87AB-0890-46C4-96E4-8276D8E0AEB1}'}
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
    if($legacy) {
        # WPF/Timer 이벤트의 async void는 기존 VSTHRD100 주석과 최신 분석기로 검증한다.
        # 15.6의 중복 분석기는 해당 SuppressMessage 이름을 인식하지 못한다.
        $noWarn=$project.CreateElement('NoWarn',$project.DocumentElement.NamespaceURI)
        $noWarn.InnerText='$(NoWarn);AvoidAsyncVoid'
        $null=$project.SelectSingleNode('//m:PropertyGroup',$namespace).AppendChild($noWarn)
        # BuildTools는 고객 IDE가 로드하는 SDK가 아니다. 15.9 패커로 NuGet 런타임 DLL 누락을 방지한다.
        $versions=@{'Microsoft.VisualStudio.Shell.15.0'='15.7.27703';'Microsoft.VisualStudio.Threading'='15.6.31';'Microsoft.VSSDK.BuildTools'='15.9.3032'}
        foreach($package in $project.SelectNodes('//m:PackageReference',$namespace)) {
            if($versions.ContainsKey($package.Include)){$package.SetAttribute('Version',$versions[$package.Include])}
        }
        # 실행 SDK는 15.7로 고정하고 빌드 전용 분석기는 기존 제품과 통일한다.
        # AsyncUsageAnalyzers의 과거 이벤트 진단은 현재 VSTHRD100 분석으로 대체한다.
        $packages=$project.SelectSingleNode('//m:PackageReference',$namespace).ParentNode
        foreach($entry in @(@('Microsoft.VisualStudio.SDK.Analyzers','15.8.33',''),@('Microsoft.VisualStudio.Threading.Analyzers','15.8.122',''),@('AsyncUsageAnalyzers','1.0.0-alpha003','all'))) {
            $analyzer=$project.CreateElement('PackageReference',$project.DocumentElement.NamespaceURI)
            $analyzer.SetAttribute('Include',$entry[0]);$analyzer.SetAttribute('Version',$entry[1]);$analyzer.SetAttribute('PrivateAssets','all')
            if($entry[2]){$analyzer.SetAttribute('ExcludeAssets',$entry[2])}
            $null=$packages.AppendChild($analyzer)
        }
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
    foreach($kind in @('Compile','Page','Resource','VSCTCompile','EmbeddedResource','None','Content')) {
        foreach($item in $project.SelectNodes('//m:'+ $kind +'[@Include]',$namespace)) {
            $relative=$item.GetAttribute('Include')
            if($relative -eq 'source.extension.vsixmanifest' -or $relative.StartsWith('$(')) {continue}
            # 저장소 루트의 공용 문서·GIF는 각 SKU에서 같은 상대 경로를 유지한다.
            # 공통 제품 안의 Content(예: 라이선스)는 다른 SKU에서 공통 소스로 연결한다.
            if($kind -eq 'Content' -and ($relative.Replace('/','\').StartsWith('..\..\') -or [IO.Path]::IsPathRooted($relative))) {continue}
            $item.SetAttribute('Include','..\VS2017.AIAssistant\'+$relative)
            if(!$item.SelectSingleNode('m:Link',$namespace)){
                $link=$project.CreateElement('Link',$project.DocumentElement.NamespaceURI)
                $link.InnerText=$relative
                $null=$item.AppendChild($link)
            }
        }
    }
    # 각 AI VSIX에는 현재 IDE 연도의 독립 오프라인 테마 설치 파일 하나만 넣는다.
    $themeYear=if($legacy){'2017'}else{$target}
    foreach($themeContent in $project.SelectNodes('//m:Content[contains(@Include,"Grokeen-VisualStudio-Themes-")]',$namespace)) {
        $themeContent.SetAttribute('Include',($themeContent.GetAttribute('Include').Replace('-VS2017.vsix',('-VS'+$themeYear+'.vsix'))))
        $themeLink=$themeContent.SelectSingleNode('m:Link',$namespace)
        if($themeLink){$themeLink.InnerText=$themeLink.InnerText.Replace('-VS2017.vsix',('-VS'+$themeYear+'.vsix'))}
    }
    $settings=New-Object Xml.XmlWriterSettings
    $settings.Encoding=$utf8; $settings.Indent=$true
    $writer=[Xml.XmlWriter]::Create((Join-Path $directory ('VS'+$target+'.AIAssistant.csproj')),$settings)
    try{$project.Save($writer)}finally{$writer.Dispose()}
    [xml]$manifest=[IO.File]::ReadAllText((Join-Path $baseDir 'source.extension.vsixmanifest'))
    $ns=New-Object Xml.XmlNamespaceManager($manifest.NameTable);$ns.AddNamespace('v','http://schemas.microsoft.com/developer/vsx-schema/2011')
    $identity=$manifest.SelectSingleNode('//v:Identity',$ns)
    $identity.SetAttribute('Id',$(if($legacy){'Grokeen.VS2017.AIAssistant'}else{'Grokeen.VS'+$target+'.AIAssistant'}))
    $manifest.SelectSingleNode('//v:DisplayName',$ns).InnerText=$(if($legacy){'VS2017 AI Assistant (15.7+)'}else{'Visual Studio AI Assistant '+$target})
    $install=$manifest.SelectSingleNode('//v:InstallationTarget',$ns)
    $range=if($legacy){'[15.0.27703.0,16.0)'}else{'['+$major+'.0,'+($major+1)+'.0)'}
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

