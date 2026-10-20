#requires -Version 5.1
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Run in Windows PowerShell 5.1 -STA.' }

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$candidate = Join-Path $repo 'artifacts\task069\candidate3'
$dll = Join-Path $candidate '2017\Release\VS2017.AIAssistant.dll'
$assets = Join-Path $candidate 'source\src\VS2017.AIAssistant\obj\project.assets.json'
$themes = Join-Path $repo 'artifacts\task069\themes-final1'
$output = Join-Path $repo 'artifacts\task069\orig4\candidate3-actual-ui'
$source = Join-Path $PSScriptRoot 'C3ActualUiChecks.cs'
if (!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $assets)) { throw 'Frozen C3 DLL/assets are missing.' }
$null = New-Item -ItemType Directory -Path $output -Force
$profile = Join-Path $output 'profile'
$null = New-Item -ItemType Directory -Path $profile -Force
$env:APPDATA = Join-Path $profile 'Roaming'
$env:LOCALAPPDATA = Join-Path $profile 'Local'
$env:USERPROFILE = $profile
$env:TASK069_C3_DLL = $dll
$env:TASK069_C3_OUTPUT = $output
$env:TASK069_VS_ASSEMBLIES = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\IDE\PublicAssemblies'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$referenceDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$compiler = Join-Path $framework 'csc.exe'
$exe = Join-Path $output 'C3ActualUiChecks.exe'
$references = @('/r:System.dll','/r:System.Core.dll','/r:System.Web.Extensions.dll',"/r:$referenceDir\WindowsBase.dll","/r:$referenceDir\PresentationCore.dll","/r:$referenceDir\PresentationFramework.dll","/r:$referenceDir\System.Xaml.dll")
$before = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
$assetsHash = (Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash
& $compiler /nologo /target:exe /out:$exe @references $source
if ($LASTEXITCODE -ne 0) { throw "Harness compilation failed: $LASTEXITCODE" }
& $exe
$exit = $LASTEXITCODE
if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $before) { throw 'Frozen candidate DLL changed during checks.' }

# The 2017 theme VSIX embedded by C3 must be byte-identical to the clean 0.14.0 theme build.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$assistantVsix = Join-Path $candidate '2017\Release\VS2017.AIAssistant.vsix'
$assistantZip = [IO.Compression.ZipFile]::OpenRead($assistantVsix)
try {
  $embedded = $assistantZip.GetEntry('Themes/Grokeen-VisualStudio-Themes-0.14.0-VS2017.vsix')
  if ($null -eq $embedded) { throw 'C3 assistant VSIX does not contain the expected 0.14.0 VS2017 theme package.' }
  $embeddedStream = $embedded.Open()
  try { $embeddedHash = [BitConverter]::ToString(([Security.Cryptography.SHA256]::Create()).ComputeHash($embeddedStream)).Replace('-','') }
  finally { $embeddedStream.Dispose() }
} finally { $assistantZip.Dispose() }
$themeVsix = Join-Path $themes 'Grokeen-VisualStudio-Themes-0.14.0-VS2017.vsix'
$standaloneThemeHash = (Get-FileHash -LiteralPath $themeVsix -Algorithm SHA256).Hash
if ($embeddedHash -ne $standaloneThemeHash) { throw 'C3 embedded theme VSIX differs from themes-final1.' }

function Get-ThemeColors([string]$path, [string[]]$names) {
  [xml]$xml = Get-Content -LiteralPath $path -Raw
  $out = [ordered]@{}
  foreach ($name in $names) {
    $node = $xml.SelectSingleNode("//*[local-name()='Color' and @Name='$name']")
    if ($null -eq $node) { throw "Theme color missing: $name in $path" }
    $background = $node.SelectSingleNode("./*[local-name()='Background']")
    $foreground = $node.SelectSingleNode("./*[local-name()='Foreground']")
    $out[$name] = [ordered]@{Background=if($null -eq $background){$null}else{$background.GetAttribute('Source')};Foreground=if($null -eq $foreground){$null}else{$foreground.GetAttribute('Source')}}
  }
  return $out
}
$dracula = Get-ThemeColors (Join-Path $themes 'definitions\Dracula.xml') @('ScrollBarBackground','ScrollBarThumbBackground','ScrollBarThumbMouseOverBackground','ScrollBarThumbPressedBackground')
$kakao = Get-ThemeColors (Join-Path $themes 'definitions\Kakao.xml') @('ToolWindowTabText','ToolWindowTabMouseOverBackgroundBegin','ToolWindowTabMouseOverBackgroundEnd','ToolWindowTabMouseOverBorder','ToolWindowTabMouseOverText','ToolWindowTabSelectedTab','ToolWindowTabSelectedText','ToolWindowTabSelectedBorder')
$pngHashes = @{}
Get-ChildItem -LiteralPath $output -Filter 'progress-*.png' -File | Sort-Object Name | ForEach-Object { $pngHashes[$_.Name] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
$report = [ordered]@{
  Candidate='TASK069 C3 actual VS2017 Release DLL'
  DllPath=$dll; DllSha256=$before; ProjectAssetsSha256=$assetsHash
  OffscreenUiResults=(Get-Content -LiteralPath (Join-Path $output 'actual-ui-results.json') -Raw | ConvertFrom-Json)
  ThemePackage=[ordered]@{AssistantVsixSha256=(Get-FileHash -LiteralPath $assistantVsix -Algorithm SHA256).Hash;EmbeddedThemeVsixSha256=$embeddedHash;StandaloneThemeVsixSha256=$standaloneThemeHash;ByteIdentical=($embeddedHash -eq $standaloneThemeHash)}
  DraculaPalette=$dracula; KakaoPalette=$kakao; PngSha256=$pngHashes
  WindowShown=$false;NativePointerUsed=$false;ProductSourceModified=$false;CandidateDllModified=$false
}
$report | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$report | ConvertTo-Json -Depth 9
if ($exit -ne 0) { throw "Actual C3 UI checks failed; inspect $output\actual-ui-results.json" }
