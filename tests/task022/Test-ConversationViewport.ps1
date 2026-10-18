#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-ConversationViewport
desc         : 고정 제품 DLL의 대화 viewport를 별도 STA·설정·워크스페이스에서 검증한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : source-only 수학 검사와 실제 제품 UI·PNG·DLL 해시 증거를 구별.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$AssemblyPath, [string]$OutputDirectory, [switch]$SourceOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (!$SourceOnly -and !$AssemblyPath) { throw '실제 UI 검사에는 고정된 -AssemblyPath가 필요합니다.' }
if (!$SourceOnly -and [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw '실제 UI 검사는 Windows PowerShell 5.1 -STA로 실행하세요.' }
$viewportRepository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $viewportRepository ('artifacts\task022\viewport-' + [guid]::NewGuid().ToString('N')) }
$viewportOutput = [IO.Path]::GetFullPath($OutputDirectory)
$viewportResultPath = Join-Path $viewportOutput 'result.json'
if (Test-Path -LiteralPath $viewportResultPath) { throw '기존 결과를 보존하도록 새로운 -OutputDirectory를 지정하세요.' }
$null = New-Item -ItemType Directory -Path $viewportOutput -Force
$viewportSource = Join-Path $viewportRepository 'src\VS2017.AIAssistant\Views\AssistantControl.Viewport.cs'
$viewportThinking = Join-Path $viewportRepository 'src\VS2017.AIAssistant\Views\AssistantControl.Thinking.cs'
$viewportXaml = Join-Path $viewportRepository 'src\VS2017.AIAssistant\Views\AssistantControl.xaml'
$viewportTheme = Join-Path $viewportRepository 'src\VS2017.AIAssistant\Views\AssistantTheme.xaml'
if ($SourceOnly) {
    $viewportCompiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
    $viewportFramework = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
    $viewportCompileArgs = @('/nologo', '/noconfig', '/nostdlib+', '/langversion:7.0', '/codepage:65001', '/warnaserror+', '/target:exe')
    foreach ($viewportRef in @('mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Web.Extensions', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) { $viewportCompileArgs += '/reference:' + (Join-Path $viewportFramework ($viewportRef + '.dll')) }
    $viewportExecutable = Join-Path $viewportOutput 'ViewPolicyChecks.exe'
    & $viewportCompiler @viewportCompileArgs ('/out:' + $viewportExecutable) $viewportSource $viewportThinking (Join-Path $PSScriptRoot 'ViewHostStub.cs') (Join-Path $PSScriptRoot 'ViewPolicyChecks.cs')
    if ($LASTEXITCODE -ne 0) { throw '실제 viewport·thinking 소스의 독립 컴파일 실패' }
    & $viewportExecutable $viewportXaml $viewportTheme $viewportResultPath 2>&1 | Tee-Object -FilePath (Join-Path $viewportOutput 'results.log')
    if ($LASTEXITCODE -ne 0) { throw "source-only viewport 검사 실패: $viewportOutput" }
    Write-Output "source-only 결과(제품 UI 증명 아님): $viewportResultPath"
    return
}
$viewportInput = (Resolve-Path -LiteralPath $AssemblyPath).Path
$viewportHash = (Get-FileHash -LiteralPath $viewportInput -Algorithm SHA256).Hash
$viewportProductDir = Join-Path $viewportOutput 'product-copy'
$null = New-Item -ItemType Directory -Path $viewportProductDir
$viewportAssembly = Join-Path $viewportProductDir ([IO.Path]::GetFileName($viewportInput))
Copy-Item -LiteralPath $viewportInput -Destination $viewportAssembly
if ((Get-FileHash -LiteralPath $viewportAssembly -Algorithm SHA256).Hash -ne $viewportHash) { throw '고정 DLL 복사본 무결성 확인 실패' }
foreach ($viewportFrameworkAssembly in @('WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml', 'System.Web.Extensions', 'System.Xml.Linq')) { Add-Type -AssemblyName $viewportFrameworkAssembly }
$viewportAssets = Get-Content -Encoding UTF8 -LiteralPath (Join-Path $viewportRepository 'src\VS2017.AIAssistant\obj\project.assets.json') -Raw | ConvertFrom-Json
$viewportPackageRoot = $viewportAssets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$viewportReferences = @{}
foreach ($viewportEntry in $viewportAssets.targets.'.NETFramework,Version=v4.6.2'.PSObject.Properties) {
    if ($viewportEntry.Value.PSObject.Properties.Name -contains 'compile') {
        foreach ($viewportItem in $viewportEntry.Value.compile.PSObject.Properties.Name) {
            if ($viewportItem -like '*.dll') { $viewportReferences[[IO.Path]::GetFileNameWithoutExtension($viewportItem)] = Join-Path $viewportPackageRoot ($viewportAssets.libraries.($viewportEntry.Name).path + '/' + $viewportItem) }
        }
    }
}
$viewportResolver = [ResolveEventHandler]{param($sender,$eventArgs) $viewportName = (New-Object Reflection.AssemblyName($eventArgs.Name)).Name; if ($viewportReferences.ContainsKey($viewportName)) { return [Reflection.Assembly]::LoadFrom($viewportReferences[$viewportName]) }; return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($viewportResolver)
$viewportOldSettings = $env:VS2017_AI_SETTINGS_PATH; $viewportOldWorkspace = $env:VS2017_AI_WORKSPACE_PATH; $viewportOldDiscovery = $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY
try {
    $env:VS2017_AI_SETTINGS_PATH = Join-Path $viewportOutput 'isolated-settings.json'
    $env:VS2017_AI_WORKSPACE_PATH = Join-Path $viewportOutput 'isolated-workspace'
    $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = '1'
    [Reflection.Assembly]::LoadFrom($viewportAssembly) | Out-Null
    if ($viewportReferences.ContainsKey('Microsoft.VisualStudio.Shell.15.0')) { [Reflection.Assembly]::LoadFrom($viewportReferences['Microsoft.VisualStudio.Shell.15.0']) | Out-Null }
    $viewportTestRefs = @($viewportAssembly, 'System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Web.Extensions.dll', [Windows.DependencyObject].Assembly.Location, [Windows.Media.Visual].Assembly.Location, [Windows.Window].Assembly.Location, [System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path @((Join-Path $PSScriptRoot 'ViewPolicyChecks.cs'), (Join-Path $PSScriptRoot 'ViewportChecks.cs')) -ReferencedAssemblies $viewportTestRefs
    [ViewportChecks]::Run($viewportOutput)
    $viewportResult = [pscustomobject]@{
        Succeeded = [ViewportChecks]::Errors.Count -eq 0
        Passed = [ViewportChecks]::Checks.Count
        Failed = [ViewportChecks]::Errors.Count
        SourceOnly = $false
        Checks = @([ViewportChecks]::Checks)
        Errors = @([ViewportChecks]::Errors)
        AssemblyPath = $viewportInput
        LoadedAssemblyPath = $viewportAssembly
        AssemblySha256 = $viewportHash
        CopySha256 = (Get-FileHash -LiteralPath $viewportAssembly -Algorithm SHA256).Hash
        NetworkRequested = $false
        PersonalCredentialsRead = $false
        NativeExecutionPerformed = $false
        CheckedAt = [DateTimeOffset]::Now.ToString('o')
    }
    if ((Get-FileHash -LiteralPath $viewportInput -Algorithm SHA256).Hash -ne $viewportHash) { throw '검사 중 입력 고정 DLL이 변경되었습니다.' }
    $viewportResult | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $viewportResultPath -Encoding UTF8
    $viewportResult | ConvertTo-Json -Depth 7
    if (!$viewportResult.Succeeded) { throw "실제 제품 viewport 검사 실패: $viewportResultPath" }
} finally {
    $env:VS2017_AI_SETTINGS_PATH = $viewportOldSettings; $env:VS2017_AI_WORKSPACE_PATH = $viewportOldWorkspace; $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = $viewportOldDiscovery
    [AppDomain]::CurrentDomain.remove_AssemblyResolve($viewportResolver)
}
