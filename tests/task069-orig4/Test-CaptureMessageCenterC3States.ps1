#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$AssemblyPath = 'artifacts/task069/candidate3/2017/Release/VS2017.AIAssistant.dll',
    [string]$AssetsPath = 'artifacts/task069/candidate3/source/src/VS2017.AIAssistant/obj/project.assets.json',
    [string]$OutputDirectory = 'artifacts/task069/orig4/messages-c3-captures/pass-5'
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Windows PowerShell 5.1 -STA is required.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$product = [IO.Path]::GetFullPath((Join-Path $root $AssemblyPath))
$assetsExact = [IO.Path]::GetFullPath((Join-Path $root $AssetsPath))
$output = [IO.Path]::GetFullPath((Join-Path $root $OutputDirectory))
if (Test-Path -LiteralPath $output) { throw "Evidence directory already exists; preserve it: $output" }
if (!(Test-Path -LiteralPath $product) -or !(Test-Path -LiteralPath $assetsExact)) { throw 'Exact candidate DLL or assets file is missing.' }
$productHash = (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
$assetsHash = (Get-FileHash -LiteralPath $assetsExact -Algorithm SHA256).Hash
$null = New-Item -ItemType Directory -Path $output
$source = Join-Path $PSScriptRoot 'Capture-MessageCenterC3States.cs'
Copy-Item -LiteralPath $source -Destination $output
$runnerProduct = Join-Path $output ([IO.Path]::GetFileName($product))
Copy-Item -LiteralPath $product -Destination $runnerProduct
foreach ($sibling in Get-ChildItem -LiteralPath (Split-Path -Parent $product) -Filter '*.dll' -File) {
    if ($sibling.FullName -ne $product) { Copy-Item -LiteralPath $sibling.FullName -Destination $output }
}

$assets = Get-Content -LiteralPath $assetsExact -Raw -Encoding UTF8 | ConvertFrom-Json
$packageRoot = $assets.packageFolders.PSObject.Properties.Name | Select-Object -First 1
$framework = $assets.targets.PSObject.Properties | Where-Object { $_.Name -like '.NETFramework,Version=v*' -and $_.Name -notlike '*/win*' } | Select-Object -First 1
$dependencies = @{}
foreach ($entry in $framework.Value.PSObject.Properties) {
    foreach ($kind in @('compile', 'runtime')) {
        if ($entry.Value.PSObject.Properties.Name -contains $kind) {
            foreach ($part in $entry.Value.$kind.PSObject.Properties.Name) {
                if ($part -like '*.dll') { $dependencies[[IO.Path]::GetFileNameWithoutExtension($part)] = Join-Path $packageRoot ($assets.libraries.($entry.Name).path + '/' + $part) }
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
$runner = Join-Path $output 'CaptureMessageCenterC3States.exe'
$arguments = @('/nologo', '/target:exe', '/langversion:7', ('/out:' + $runner), ('/r:' + $product))
foreach ($name in @('System', 'System.Core', 'System.Web.Extensions', 'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Xaml')) { $arguments += '/r:' + (Join-Path $referenceRoot ($name + '.dll')) }
$arguments += $source
& $compiler @arguments 2>&1 | Tee-Object (Join-Path $output 'compile.log')
if ($LASTEXITCODE -ne 0) { throw 'Synthetic-state capture runner compilation failed.' }

$manifest = [ordered]@{
    Status = 'running'
    ProductAssemblyPath = $product
    ProductAssemblySha256 = $productHash
    AssetsPath = $assetsExact
    AssetsSha256 = $assetsHash
    CaptureSourcePath = $source
    CaptureSourceSha256 = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    SyntheticFixture = $true
    SyntheticReply = $true
    SyntheticTransitionPath = 'ConversationCollaborationStore.Send -> Receive -> Reply -> Deliver'
    SyntheticReplyNote = 'Reply text is directly entered into the exact product store API solely to visualize lifecycle state. It is not an AI/model response or real external delivery.'
    Captures = @('recipient-display-defect.png', 'pending-outbox.png', 'replied-outbox-synthetic.png', 'delivered-outbox-synthetic.png')
    WindowShown = $false
    PopupOpened = $false
    OsInputInjected = $false
    CliOrAiStarted = $false
    NetworkRequested = $false
    C3UiDefect = 'Selected recipient displays CLR type name instead of its display name; retained unaltered in recipient-display-defect.png.'
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
$stdout = Join-Path $output 'stdout.log'; $stderr = Join-Path $output 'stderr.log'
$line = '"' + $runnerProduct + '" "' + $map + '" "' + $output + '" "' + $productHash + '"'
$child = Start-Process -FilePath $runner -ArgumentList $line -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
try { $null = $child.Handle; if (!$child.WaitForExit(60000)) { $child.Kill(); $child.WaitForExit(); throw 'Capture exceeded 60 seconds.' }; $child.WaitForExit(); $exitCode = $child.ExitCode } finally { $child.Dispose() }
if (Test-Path -LiteralPath $stderr) { Get-Content -LiteralPath $stderr -Encoding UTF8 }
$resultPath = Join-Path $output 'capture-result.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw "Capture result missing; preserve evidence at $output" }
$result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($name in $manifest.Captures) { $path = Join-Path $output $name; if (!(Test-Path -LiteralPath $path) -or (Get-Item -LiteralPath $path).Length -lt 4000) { throw "Capture missing or too small: $name" } }
if ((Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash -ne $productHash -or (Get-FileHash -LiteralPath $assetsExact -Algorithm SHA256).Hash -ne $assetsHash) { throw 'Frozen C3 DLL/assets changed during capture.' }
if ($exitCode -ne 0 -or !$result.Succeeded -or ($result.CapturedStates -join ',') -ne 'Pending,Replied,Delivered') { throw 'Synthetic state capture failed or missed an expected state.' }
$manifest.Status = 'PASS'
$manifest.ResultPath = $resultPath
$manifest.ImagePaths = $manifest.Captures | ForEach-Object { Join-Path $output $_ }
$manifest.ProductAssemblySha256After = (Get-FileHash -LiteralPath $product -Algorithm SHA256).Hash
$manifest.AssetsSha256After = (Get-FileHash -LiteralPath $assetsExact -Algorithm SHA256).Hash
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $output 'manifest.json') -Encoding UTF8
Write-Output (Get-Content -LiteralPath (Join-Path $output 'manifest.json') -Raw -Encoding UTF8)
