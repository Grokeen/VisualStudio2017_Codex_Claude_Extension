#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$AssemblyPath = 'artifacts/task069/candidate3/2017/Release/VS2017.AIAssistant.dll',
    [string]$AssetsPath = 'artifacts/task069/candidate3/source/src/VS2017.AIAssistant/obj/project.assets.json',
    [string]$OutputDirectory = 'artifacts/task069/orig4/messages-c3-captures'
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Windows PowerShell 5.1 -STA is required.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$product = [IO.Path]::GetFullPath((Join-Path $root $AssemblyPath))
$assetsPathExact = [IO.Path]::GetFullPath((Join-Path $root $AssetsPath))
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (Test-Path -LiteralPath $output) { throw "Evidence directory already exists; preserve it and choose a fresh path: $output" }
if (!(Test-Path -LiteralPath $product) -or !(Test-Path -LiteralPath $assetsPathExact)) { throw 'Exact candidate DLL or assets file is missing.' }
$productHash = (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
$assetsHash = (Get-FileHash -LiteralPath $assetsPathExact -Algorithm SHA256).Hash
$null = New-Item -ItemType Directory -Path $output
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Capture-MessageCenterC3.cs') -Destination $output
$runnerProduct = Join-Path $output ([IO.Path]::GetFileName($product))
Copy-Item -LiteralPath $product -Destination $runnerProduct
foreach ($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $product) -Filter '*.dll' -File) {
    if ($sibling.FullName -ne $product) { Copy-Item -LiteralPath $sibling.FullName -Destination $output }
}

$assets = Get-Content -LiteralPath $assetsPathExact -Raw -Encoding UTF8 | ConvertFrom-Json
$packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$framework = $assets.targets.PSObject.Properties | Where-Object { $_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*' } | Select-Object -First 1
$dependencies = @{}
foreach ($entry in $framework.Value.PSObject.Properties) {
    foreach ($kind in @('compile', 'runtime')) {
        if ($entry.Value.PSObject.Properties.Name -contains $kind) {
            foreach ($part in $entry.Value.$kind.PSObject.Properties.Name) {
                if ($part -like '*.dll') {
                    $dependencies[[IO.Path]::GetFileNameWithoutExtension($part)] = Join-Path $packageRoot ($assets.libraries.($entry.Name).path + '/' + $part)
                }
            }
        }
    }
}
foreach ($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $product) -Filter '*.dll' -File) {
    if ($sibling.FullName -ne $product) { $dependencies[$sibling.BaseName] = $sibling.FullName }
}
$map = Join-Path $output 'dependencies.json'
$dependencies | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $map -Encoding UTF8

$compiler = 'C:/Program Files (x86)/Microsoft Visual Studio/2017/Community/MSBuild/15.0/Bin/Roslyn/csc.exe'
$referenceRoot = 'C:/Program Files (x86)/Reference Assemblies/Microsoft/Framework/.NETFramework/v4.6.2'
if (!(Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler" }
$runner = Join-Path $output 'CaptureMessageCenterC3.exe'
$references = @('/nologo', '/target:exe', '/langversion:7', ('/out:' + $runner), ('/r:' + $product))
foreach ($name in @('System', 'System.Core', 'System.Web.Extensions', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) {
    $references += '/r:' + (Join-Path $referenceRoot ($name + '.dll'))
}
$references += (Join-Path $PSScriptRoot 'Capture-MessageCenterC3.cs')
& $compiler @references 2>&1 | Tee-Object (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Capture runner compilation failed.' }

$manifest = [ordered]@{
    ProductAssemblyPath = $product
    ProductAssemblySha256 = $productHash
    AssetsPath = $assetsPathExact
    AssetsSha256 = $assetsHash
    CaptureSourcePath = (Join-Path $PSScriptRoot 'Capture-MessageCenterC3.cs')
    CaptureSourceSha256 = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Capture-MessageCenterC3.cs') -Algorithm SHA256).Hash
    WindowShown = $false
    PopupOpened = $false
    OsInputInjected = $false
    CliOrAiStarted = $false
    NetworkRequested = $false
    Status = 'running'
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$stdout = Join-Path $output 'stdout.log'
$stderr = Join-Path $output 'stderr.log'
$line = '"' + $runnerProduct + '" "' + $map + '" "' + $output + '" "' + $productHash + '"'
$child = Start-Process -FilePath $runner -ArgumentList $line -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try {
    $null = $child.Handle
    if (!$child.WaitForExit(60000)) { $child.Kill(); $child.WaitForExit(); throw 'Offscreen capture exceeded 60 seconds.' }
    $child.WaitForExit(); $exitCode = $child.ExitCode
} finally { $child.Dispose() }
if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Encoding UTF8 }
$resultPath = Join-Path $output 'capture-result.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw 'Capture runner did not write capture-result.json.' }
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
$images = @('message-center-draft.png', 'message-center-inbox.png', 'message-center-outbox.png')
foreach ($image in $images) {
    $path = Join-Path $output $image
    if (!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -lt 4000) { throw "Capture missing or unexpectedly small: $image" }
}
if ((Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash -ne $productHash -or
    (Get-FileHash -LiteralPath $assetsPathExact -Algorithm SHA256).Hash -ne $assetsHash) {
    throw 'Frozen candidate DLL or assets changed during capture.'
}
if ($exitCode -ne 0 -or !$result.Succeeded) { throw "Offscreen capture failed; preserve evidence at $output" }
$manifest.Status = 'PASS'
$manifest.ResultPath = $resultPath
$manifest.ImagePaths = $images | ForEach-Object { Join-Path $output $_ }
$manifest.ProductAssemblySha256After = (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
$manifest.AssetsSha256After = (Get-FileHash -LiteralPath $assetsPathExact -Algorithm SHA256).Hash
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Write-Output (Get-Content -LiteralPath (Join-Path $output 'manifest.json') -Raw -Encoding UTF8)
