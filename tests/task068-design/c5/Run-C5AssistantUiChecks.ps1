#requires -Version 5.1
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Run in Windows PowerShell 5.1 -STA.' }

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$candidate = Join-Path $repo 'artifacts\task068\candidate5'
$dll = Join-Path $candidate '2017\Release\VS2017.AIAssistant.dll'
$assetManifest = Join-Path $candidate 'source-hashes.json'
$assets = Join-Path $candidate 'source\src\VS2017.AIAssistant\obj\project.assets.json'
$output = Join-Path $repo 'artifacts\task068-design\c5-actual'
$harness = Join-Path $PSScriptRoot 'C5AssistantUiChecks.cs'
if (!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $assetManifest) -or !(Test-Path -LiteralPath $assets)) { throw 'The frozen C5 DLL or its source/package manifests are missing.' }

$settingsRoot = Join-Path $output 'process-profile'
$null = New-Item -ItemType Directory -Path $settingsRoot -Force
$env:APPDATA = Join-Path $settingsRoot 'Roaming'
$env:LOCALAPPDATA = Join-Path $settingsRoot 'Local'
$env:USERPROFILE = $settingsRoot
$env:TASK068_ROOT = $repo
$env:TASK068_DLL = $dll
$env:TASK068_OUTPUT = $output
$env:TASK068_RUN_ID = [Guid]::NewGuid().ToString('N')
$env:TASK068_VS_ASSEMBLIES = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\PublicAssemblies'
$env:VS2017_AI_SETTINGS_PATH = Join-Path $settingsRoot 'settings.json'
$env:VS2017_AI_WORKSPACE_PATH = Join-Path $settingsRoot 'workspace'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$referenceDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$compiler = Join-Path $framework 'csc.exe'
$exe = Join-Path $output 'C5AssistantUiChecks.exe'
$references = @(
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Web.Extensions.dll',
  "/r:$referenceDir\WindowsBase.dll", "/r:$referenceDir\PresentationCore.dll", "/r:$referenceDir\PresentationFramework.dll", "/r:$referenceDir\System.Xaml.dll",
  "/r:$referenceDir\UIAutomationTypes.dll", "/r:$referenceDir\UIAutomationProvider.dll"
)
& $compiler /nologo /target:exe /out:$exe @references $harness
if ($LASTEXITCODE -ne 0) { throw "Harness compilation failed: $LASTEXITCODE" }

$before = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
& $exe
$exit = $LASTEXITCODE
$after = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
if ($before -ne $after) { throw 'Candidate DLL changed during its read-only rendering check.' }
$runResult = Get-Content (Join-Path $output 'actual-ui-results.json') -Encoding UTF8 -Raw | ConvertFrom-Json
$warningCounts = @{}
foreach ($build in (Get-Content (Join-Path $candidate 'builds.json') -Raw | ConvertFrom-Json)) { $warningCounts[$build.Target] = $build.Warnings }
$imageHashes = @{}
Get-ChildItem -LiteralPath $output -Filter '*.png' -File | ForEach-Object { $imageHashes[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }

$manifest = [ordered]@{
  Candidate = 'C5 actual VS2017.AIAssistant.dll'
  DllSha256 = $before
  SourceHashManifestSha256 = (Get-FileHash -LiteralPath $assetManifest -Algorithm SHA256).Hash
  ProjectAssetsSha256 = (Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash
  BuildWarningsBySku = $warningCounts
  TestPassed = ($exit -eq 0)
  FailedAssertions = $runResult.Failures
  WindowCreated = $false
  WindowShown = $false
  OSInputUsed = $false
  ProductSourceModified = $false
  FocusVerification = 'Logical focus could not be verified without a PresentationSource; item.Focus() returned false in detached offscreen rendering.'
  ResultPath = 'actual-ui-results.json'
  PngSha256 = $imageHashes
  RenderDirectory = $output
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$manifest | ConvertTo-Json -Depth 5
if ($exit -ne 0) { throw "C5 actual UI checks failed with exit code $exit; inspect actual-ui-results.json." }


