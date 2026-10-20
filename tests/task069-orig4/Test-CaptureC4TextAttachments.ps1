#requires -Version 5.1
[CmdletBinding()]
param([string]$OutputDirectory = 'artifacts\task069\orig4\candidate4-text-attachments-pass2')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Run in Windows PowerShell 5.1 -STA.' }

$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$candidate = Join-Path $repo 'artifacts\task069\candidate4'
$dll = Join-Path $candidate '2017\Release\VS2017.AIAssistant.dll'
$assets = Join-Path $candidate 'source\src\VS2017.AIAssistant\obj\project.assets.json'
$source = Join-Path $PSScriptRoot 'Capture-C4TextAttachments.cs'
$output = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if (!(Test-Path -LiteralPath $dll) -or !(Test-Path -LiteralPath $assets)) { throw 'Frozen C4 DLL/assets are missing.' }
if (Test-Path -LiteralPath $output) { throw "Evidence output exists; preserving it and requiring a fresh path: $output" }
$dllHash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
$assetsHash = (Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash
$null = New-Item -ItemType Directory -Path $output
$profile = Join-Path $output 'private-profile'
$null = New-Item -ItemType Directory -Path $profile
$env:APPDATA = Join-Path $profile 'Roaming'
$env:LOCALAPPDATA = Join-Path $profile 'Local'
$env:USERPROFILE = $profile
$env:VS2017_AI_SETTINGS_PATH = Join-Path $profile 'settings.json'
$env:VS2017_AI_WORKSPACE_PATH = Join-Path $profile 'workspace'
$env:VS2017_AI_DISABLE_CLI_DISCOVERY = '1'
$env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = '1'
$env:TASK069_C4_CAPTURE_OUTPUT = $output
$null = New-Item -ItemType Directory -Path $env:VS2017_AI_WORKSPACE_PATH
Set-Content -LiteralPath $env:VS2017_AI_SETTINGS_PATH -Value '{"HideWelcome":true,"ShowHistory":false,"EnableCompletionNotifications":false}' -Encoding UTF8

$assetsObject = Get-Content -LiteralPath $assets -Raw -Encoding UTF8 | ConvertFrom-Json
$packageRoot = $assetsObject.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$frameworkTarget = $assetsObject.targets.PSObject.Properties | Where-Object { $_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*' } | Select-Object -First 1
$dependencies = @{}
foreach ($entry in $frameworkTarget.Value.PSObject.Properties) {
  foreach ($kind in @('compile','runtime')) {
    if ($entry.Value.PSObject.Properties.Name -contains $kind) {
      foreach ($part in $entry.Value.$kind.PSObject.Properties.Name) {
        if ($part -like '*.dll') { $dependencies[[IO.Path]::GetFileNameWithoutExtension($part)] = Join-Path $packageRoot ($assetsObject.libraries.($entry.Name).path + '/' + $part) }
      }
    }
  }
}
foreach ($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $dll) -Filter '*.dll' -File) {
  if ($sibling.FullName -ne $dll) { $dependencies[$sibling.BaseName] = $sibling.FullName; Copy-Item -LiteralPath $sibling.FullName -Destination $output }
}
$dllCopy = Join-Path $output ([IO.Path]::GetFileName($dll))
Copy-Item -LiteralPath $dll -Destination $dllCopy
$dependencyPath = Join-Path $output 'dependencies.json'
$dependencies | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $dependencyPath -Encoding UTF8

$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\Roslyn\csc.exe'
$referenceDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.2'
$runner = Join-Path $output 'CaptureC4TextAttachments.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }
$references = @('/nologo','/target:exe','/langversion:7',('/out:'+$runner),('/r:'+$dllCopy),('/r:'+(Join-Path $referenceDir 'WindowsBase.dll')),('/r:'+(Join-Path $referenceDir 'PresentationCore.dll')),('/r:'+(Join-Path $referenceDir 'PresentationFramework.dll')),('/r:'+(Join-Path $referenceDir 'System.Xaml.dll')),('/r:System.dll'),('/r:System.Core.dll'),('/r:System.Web.Extensions.dll'),$source)
& $compiler @references 2>&1 | Tee-Object (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw "Capture harness compilation failed: $LASTEXITCODE" }

$manifest = [ordered]@{CandidateDllPath=$dll;CandidateDllSha256=$dllHash;AssetsPath=$assets;AssetsSha256=$assetsHash;CaptureSource=$source;CaptureSourceSha256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash;WindowShown=$false;PopupOpened=$false;OsInputUsed=$false;NetworkUsed=$false;Status='running'}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$stdout = Join-Path $output 'stdout.log'; $stderr = Join-Path $output 'stderr.log'
$arguments = '"' + $dllCopy + '" "' + $dependencyPath + '"'
$process = Start-Process -FilePath $runner -ArgumentList $arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try { $null=$process.Handle; if (!$process.WaitForExit(60000)) { $process.Kill();$process.WaitForExit();throw 'Capture exceeded 60 seconds.' };$process.WaitForExit();$exitCode=$process.ExitCode } finally { $process.Dispose() }
if (Test-Path $stderr) { Get-Content -LiteralPath $stderr -Encoding UTF8 }
$resultPath = Join-Path $output 'capture-result.json'
if (!(Test-Path -LiteralPath $resultPath) -or $exitCode -ne 0) { throw "Capture failed with exit $exitCode; see $output" }
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($image in $result.Images) { if (!(Test-Path -LiteralPath (Join-Path $output $image)) -or (Get-Item (Join-Path $output $image)).Length -lt 5000) { throw "Missing rendered image: $image" } }
if ((Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash -ne $dllHash -or (Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash -ne $assetsHash) { throw 'Frozen C4 DLL/assets changed during capture.' }
$manifest.Status='PASS';$manifest.ResultPath=$resultPath;$manifest.Images=$result.Images;$manifest.ProductDllSha256After=(Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash;$manifest.AssetsSha256After=(Get-FileHash -LiteralPath $assets -Algorithm SHA256).Hash
$manifest.PngSha256=@{}; foreach ($image in $result.Images) { $manifest.PngSha256[$image]=(Get-FileHash -LiteralPath (Join-Path $output $image) -Algorithm SHA256).Hash }
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Get-Content -LiteralPath (Join-Path $output 'manifest.json') -Raw -Encoding UTF8
