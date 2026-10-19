#requires -Version 5.1
<#
name         : Test-RequestQueue
desc         : 고정 제품 DLL의 입력 FIFO·승인·소유권·실제 합성 CLI를 STA WPF에서 검사한다.
author       : CodexOrig
create date  : 2026-10-20
update date  : 2026-10-20 CodexOrig : 보조 개발 TASK055 고정 DLL·WPF 문맥·프로세스 경계 검사.
#>
[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$AssemblyPath, [Parameter(Mandatory=$true)][string]$OutputDirectory,[string]$AssetsPath,[switch]$PerformanceOnly,[switch]$StatusOnly)
$ErrorActionPreference='Stop'
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Windows PowerShell 5.1 -STA가 필요합니다.' }
$repository=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output=[IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { if (@(Get-ChildItem -LiteralPath $output -Force).Count -gt 0) { throw '새로운 비어 있는 출력 폴더가 필요합니다.' } }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$original=[IO.Path]::GetFullPath($AssemblyPath)
if (!(Test-Path -LiteralPath $original -PathType Leaf)) { throw '제품 DLL이 없습니다.' }
$expected=(Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash
$copy=Join-Path $output ([IO.Path]::GetFileName($original))
Copy-Item -LiteralPath $original -Destination $copy
if ((Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash -ne $expected) { throw '제품 DLL 복사본 해시가 다릅니다.' }
$oldFixture=$env:TASK055_FIXTURE_EXE
$fixture=Join-Path $output 'RequestQueueProtocolFixture.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /reference:System.Web.Extensions.dll ("/out:"+$fixture) (Join-Path $PSScriptRoot 'RequestQueueProtocolFixture.cs')
if($LASTEXITCODE -ne 0){throw 'Queue protocol fixture compile failed.'}
$env:TASK055_FIXTURE_EXE=$fixture
$oldSettings=$env:VS2017_AI_SETTINGS_PATH
$oldWorkspace=$env:VS2017_AI_WORKSPACE_PATH
$oldDiscovery=$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
$oldCliDiscovery=$env:VS2017_AI_DISABLE_CLI_DISCOVERY
$env:VS2017_AI_SETTINGS_PATH=Join-Path $output 'settings.json'
$env:VS2017_AI_WORKSPACE_PATH=Join-Path $output 'workspace'
$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY='1'
$env:VS2017_AI_DISABLE_CLI_DISCOVERY='1'
$resolver=$null
try {
    foreach ($name in @('WindowsBase','PresentationCore','PresentationFramework','System.Xaml','System.Web.Extensions','System.Xml.Linq')) { Add-Type -AssemblyName $name }
    if(!$AssetsPath){$AssetsPath=Join-Path $repository 'src/VS2017.AIAssistant/obj/project.assets.json'}
    $assets=Get-Content -LiteralPath $AssetsPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $packageRoot=$assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
    $dependencies=@{}
    $framework=$assets.targets.PSObject.Properties | Where-Object{$_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*'} | Select-Object -First 1
    foreach ($item in $framework.Value.PSObject.Properties) {
        if ($item.Value.PSObject.Properties.Name -contains 'compile') {
            foreach ($relative in $item.Value.compile.PSObject.Properties.Name) {
                if ($relative -like '*.dll') { $dependencies[[IO.Path]::GetFileNameWithoutExtension($relative)]=Join-Path $packageRoot ($assets.libraries.($item.Name).path+'/'+$relative) }
            }
        }
    }
    $productDirectory=[IO.Path]::GetDirectoryName($original)
    $resolver=[ResolveEventHandler]{param($sender,$eventArgs)
        $name=(New-Object Reflection.AssemblyName($eventArgs.Name)).Name
        $sibling=Join-Path $productDirectory ($name+'.dll')
        if (Test-Path -LiteralPath $sibling -PathType Leaf) { return [Reflection.Assembly]::LoadFrom($sibling) }
        if ($dependencies.ContainsKey($name)) { return [Reflection.Assembly]::LoadFrom($dependencies[$name]) }
        return $null
    }
    [AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
    $product=[Reflection.Assembly]::LoadFrom($copy)
    $references=@($copy,'System.dll','System.Core.dll','System.Xml.dll','System.Web.Extensions.dll',[Windows.DependencyObject].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.Window].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
    $mapPath=Join-Path $output 'dependencies.json'
    $dependencies | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $mapPath -Encoding UTF8
    $runner=Join-Path $output 'RequestQueueRunner.exe'
    $compilerArgs=@('/nologo','/target:exe',('/out:'+$runner))
    $compilerArgs+=@($references | ForEach-Object{'/reference:'+$_})
    $compilerArgs+=@(Join-Path $PSScriptRoot 'RequestQueueChecks.cs'),@(Join-Path $PSScriptRoot 'RequestQueueRunner.cs')
    & $compiler @compilerArgs
    if($LASTEXITCODE -ne 0){throw 'Queue runner compile failed.'}
    $mode=if($StatusOnly){'status'}elseif($PerformanceOnly){'performance'}else{'suite'}
    & $runner $copy $mapPath (Join-Path $output 'result.json') $mode $expected
    if($LASTEXITCODE -ne 0){throw '입력 FIFO 검사가 실패했습니다.'}
} finally {
    if ($resolver -ne $null) { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
    $env:TASK055_FIXTURE_EXE=$oldFixture
    $env:VS2017_AI_SETTINGS_PATH=$oldSettings
    $env:VS2017_AI_WORKSPACE_PATH=$oldWorkspace
    $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY=$oldDiscovery
    $env:VS2017_AI_DISABLE_CLI_DISCOVERY=$oldCliDiscovery
}
