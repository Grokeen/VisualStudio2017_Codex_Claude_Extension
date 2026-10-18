#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ButtonCenters
desc         : 제품 두 소스를 독립 컴파일하고 창을 표시하지 않는 메모리 배치만 검사한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : TASK-022 계정·설정·공유 빌드·실 GUI 실행 없는 회귀 검사.
               2026-10-18 CodexCode : 고정 제품 DLL reflection 검사와 격리된 설정·의존성 로드 추가.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory, [string]$CompilerPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Windows PowerShell 5.1 -STA로 실행하세요.' }
$centerRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory=Join-Path $centerRepo ('artifacts\task022\centers-' + [guid]::NewGuid().ToString('N').Substring(0,10)) }
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
foreach ($centerFramework in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml')) { Add-Type -AssemblyName $centerFramework }
$centerReferences=@('System.dll','System.Core.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
$centerProductSources=@((Join-Path $centerRepo 'src\VS2017.AIAssistant\Views\AssistantControl.Fan.cs'),(Join-Path $centerRepo 'src\VS2017.AIAssistant\Views\AssistantDialogs.cs'))
$centerSources=@((Join-Path $PSScriptRoot 'CenterChecks.cs'))
if (!$AssemblyPath) { $centerSources=$centerProductSources + @((Join-Path $PSScriptRoot 'CenterControlStub.cs')) + $centerSources }
if (!(Test-Path -LiteralPath $CompilerPath -PathType Leaf)) { throw 'VS2017 이상 Roslyn csc.exe 경로를 CompilerPath로 지정하세요.' }
$centerAssembly = Join-Path $OutputDirectory 'CenterMemoryChecks.dll'
$centerArguments = @('/nologo','/target:library','/langversion:7',('/out:' + $centerAssembly)) + @($centerReferences | ForEach-Object { '/reference:' + $_ }) + $centerSources
& $CompilerPath @centerArguments
if ($LASTEXITCODE -ne 0) { throw '버튼 중앙 배치 독립 컴파일이 실패했습니다.' }
Add-Type -Path $centerAssembly
$centerResolve=$null
$centerPriorSettings=$env:VS2017_AI_SETTINGS_PATH; $centerPriorWorkspace=$env:VS2017_AI_WORKSPACE_PATH; $centerPriorDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try {
    $env:VS2017_AI_SETTINGS_PATH=Join-Path $OutputDirectory 'settings.json'; $env:VS2017_AI_WORKSPACE_PATH=Join-Path $OutputDirectory 'workspace'; $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
    [IO.File]::WriteAllText($env:VS2017_AI_SETTINGS_PATH,'{"HideWelcome":true,"FontSize":14}',(New-Object Text.UTF8Encoding($true)))
    $centerMode='SourceOnlyMemoryLayout'; $centerProductHash=$null
    if ($AssemblyPath) {
        $AssemblyPath=(Resolve-Path -LiteralPath $AssemblyPath).Path; $centerProductHash=(Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
        $centerAssets=Get-Content -LiteralPath (Join-Path $centerRepo 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $centerPackageRoot=$centerAssets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
        $centerPackageReferences=@{}
        foreach ($centerEntry in $centerAssets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
            if ($centerEntry.Value.PSObject.Properties.Name -contains 'compile') {
                foreach ($centerItem in $centerEntry.Value.compile.PSObject.Properties.Name) {
                    if ($centerItem -like '*.dll') { $centerPackageReferences[[IO.Path]::GetFileNameWithoutExtension($centerItem)]=Join-Path $centerPackageRoot ($centerAssets.libraries.($centerEntry.Name).path + '/' + $centerItem) }
                }
            }
        }
        $centerProductDirectory=Split-Path -Parent $AssemblyPath
        $centerResolve=[ResolveEventHandler]{ param($sender,$eventArgs)
            $centerName=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
            if ($centerName.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { return $null }
            $centerAdjacent=Join-Path $centerProductDirectory ($centerName + '.dll')
            if (Test-Path -LiteralPath $centerAdjacent -PathType Leaf) { return [Reflection.Assembly]::LoadFrom($centerAdjacent) }
            if ($centerPackageReferences.ContainsKey($centerName)) { return [Reflection.Assembly]::LoadFrom($centerPackageReferences[$centerName]) }
            return $null
        }
        [AppDomain]::CurrentDomain.add_AssemblyResolve($centerResolve)
        $centerProduct=[Reflection.Assembly]::LoadFrom($AssemblyPath)
        if ($centerPackageReferences.ContainsKey('Microsoft.VisualStudio.Shell.15.0')) { $null=[Reflection.Assembly]::LoadFrom($centerPackageReferences['Microsoft.VisualStudio.Shell.15.0']) }
        $centerView=[Activator]::CreateInstance($centerProduct.GetType('VS2017.AIAssistant.Views.AssistantControl',$true))
        $centerMode='FrozenProductMemoryLayout'
    }
    else { $centerView=New-Object VS2017.AIAssistant.Views.AssistantControl }
    $centerResult=[CodexCode.Task022.CenterChecks]::Run($centerView)
    $centerReport=[ordered]@{ Succeeded=$centerResult.Succeeded; Passed=@($centerResult.Passed).Count; Failed=@($centerResult.Failures).Count; Checks=$centerResult.Passed; Failures=$centerResult.Failures; Evidence=$centerResult.Evidence; Error=$centerResult.Error; Mode=$centerMode; ProductAssemblyPath=$AssemblyPath; ProductAssemblySha256=$centerProductHash; WindowShown=$false; Rendered=$false; DispatcherLoopStarted=$false; AiInvoked=$false; PersonalSettingsRead=$false; SharedProductBuild=$false; SourceHashes=@(Get-FileHash -LiteralPath ($centerProductSources + $centerSources | Select-Object -Unique) -Algorithm SHA256 | Select-Object Path,Hash); CheckedAt=(Get-Date).ToString('O') }
    $centerReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'result.json') -Encoding UTF8
    $centerReport | ConvertTo-Json -Depth 6
    if (!$centerResult.Succeeded) { exit 1 }
}
finally {
    if ($centerResolve) { [AppDomain]::CurrentDomain.remove_AssemblyResolve($centerResolve) }
    $env:VS2017_AI_SETTINGS_PATH=$centerPriorSettings; $env:VS2017_AI_WORKSPACE_PATH=$centerPriorWorkspace; $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$centerPriorDiscovery
}
exit 0
