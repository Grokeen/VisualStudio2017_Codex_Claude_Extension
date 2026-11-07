[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$batch=Join-Path $repo 'InstallAIAssistant.bat'
$text=[IO.File]::ReadAllText($batch)
if($text -notmatch 'set "AI_ASSISTANT_PACKAGE_VERSION=(\d+\.\d+\.\d+)"'){throw 'Missing explicit current package version.'}
$version=$matches[1]
$output=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $output){throw 'Use a new output directory.'}
[void][IO.Directory]::CreateDirectory($output)
$results=@()
foreach($name in @('missing','old-only','current-release','root-package')){
    $root=Join-Path $output ('fixture '+$name)
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'scripts'))
    Copy-Item -LiteralPath $batch -Destination (Join-Path $root 'InstallAIAssistant.bat')
    $fake=@'
param([string]$PackageDirectory,[string]$PackageVersion,[switch]$Install)
if(!$Install){exit 3}
[IO.File]::WriteAllText((Join-Path (Split-Path -Parent $PSScriptRoot) 'synthetic-called.txt'),([IO.Path]::GetFullPath($PackageDirectory)+'|'+$PackageVersion))
exit 0
'@
    [IO.File]::WriteAllText((Join-Path $root 'scripts/Install-AIAssistant.ps1'),$fake)
    if($name -eq 'old-only'){$dir=Join-Path $root 'releases/v0.11.1';[void][IO.Directory]::CreateDirectory($dir);[IO.File]::WriteAllText((Join-Path $dir 'SHA256SUMS.txt'),'old')}
    if($name -eq 'current-release'){$dir=Join-Path $root ('releases/v'+$version);[void][IO.Directory]::CreateDirectory($dir);[IO.File]::WriteAllText((Join-Path $dir 'SHA256SUMS.txt'),'current')}
    if($name -eq 'root-package'){$dir=$root;[IO.File]::WriteAllText((Join-Path $dir 'SHA256SUMS.txt'),'current')}
    $run=@(& cmd.exe /d /c (Join-Path $root 'InstallAIAssistant.bat') --noninteractive 2>&1)
    $code=$LASTEXITCODE;$marker=Join-Path $root 'synthetic-called.txt'
    if($name -in @('missing','old-only')){
        if($code -eq 0 -or (Test-Path -LiteralPath $marker)){throw "$name unexpectedly invoked synthetic installer"}
    }else{
        $expected=[IO.Path]::GetFullPath($dir).TrimEnd('\')+'|'+$version
        if($code -ne 0 -or !(Test-Path -LiteralPath $marker) -or [IO.File]::ReadAllText($marker) -cne $expected){throw "$name did not select the current package and version"}
    }
    $results+=[pscustomobject]@{Case=$name;ExitCode=$code;SyntheticInstallerInvoked=(Test-Path -LiteralPath $marker)}
}
[pscustomobject]@{Succeeded=$true;PackageVersion=$version;Cases=$results;RealInstallerExecuted=$false;RealCliExecuted=$false;NetworkUsed=$false}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
Write-Output ('PASS '+$results.Count+' package selection cases with a synthetic installer only.')
