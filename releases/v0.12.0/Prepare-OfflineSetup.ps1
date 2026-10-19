#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Prepare-OfflineSetup
desc         : 공식 설치된 CLI의 전체 런타임과 VSIX를 계정 정보 없이 오프라인 이동용 묶음으로 만든다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-038 준비 PC에서 서명 확인, 대상 PC에서는 다운로드 불필요.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory=(Join-Path (Get-Location) ('PreparedOffline-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$CodexPackageDirectory=(Join-Path $env:USERPROFILE '.codex/packages/standalone/current'),
    [string]$ClaudeExecutable=(Join-Path $env:USERPROFILE '.local/bin/claude.exe'),
    [string]$ExtensionPackageDirectory,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$PackageVersion='0.12.0',
    [switch]$CreateZip
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'OfflineCli.Common.ps1')

<#
-------------------------------------------------------------------------
name         : Assert-OfflineSourcePublisher
desc         : 연결 가능한 준비 PC에서 공식 게시자 서명을 확인한다. 설치 대상에서는 호출하지 않는다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : unsigned 임의 실행파일의 오프라인 이동 방지.
-------------------------------------------------------------------------
#>
function Assert-OfflineSourcePublisher {
    param([string]$Path,[string]$Publisher)
    $signature=Get-AuthenticodeSignature -FilePath $Path
    if($signature.Status -ne 'Valid' -or !$signature.SignerCertificate -or $signature.SignerCertificate.Subject -notmatch $Publisher){throw "공식 게시자 서명을 확인하지 못했습니다: $Path"}
    if((Get-OfflinePeArchitecture $Path) -ne 'amd64'){throw 'Windows x64 CLI를 준비하세요.'}
    return [pscustomobject]@{Name=[IO.Path]::GetFileName($Path);Publisher=$signature.SignerCertificate.Subject;Thumbprint=$signature.SignerCertificate.Thumbprint;Status='Valid';Sha256=(Get-FileHash -LiteralPath $Path).Hash}
}

try {
    $output=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')
    Assert-OfflinePlainPath $output
    if(Test-Path -LiteralPath $output){throw '새 출력 폴더를 지정하세요. 기존 묶음은 덮어쓰지 않습니다.'}
    if($CreateZip -and (Test-Path -LiteralPath ($output+'.zip'))){throw '동일 이름 ZIP을 보존합니다. 새 출력 폴더를 지정하세요.'}
    $package=Get-Item -LiteralPath $CodexPackageDirectory -Force
    if(($package.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        # 공식 current junction만 런타임 release 디렉터리로 한 번 해석하고 내부 연결은 거부한다.
        if($package.Name -ne 'current' -or @($package.Target).Count -ne 1){throw 'Codex release 디렉터리를 직접 지정하세요.'}
        $CodexPackageDirectory=[string]@($package.Target)[0]
    }
    $codexRoot=[IO.Path]::GetFullPath($CodexPackageDirectory).TrimEnd('\')
    Assert-OfflinePlainPath $codexRoot;Assert-OfflinePlainPath $ClaudeExecutable
    if($output.StartsWith($codexRoot+'\',[StringComparison]::OrdinalIgnoreCase)){throw '출력 폴더는 Codex 원본 밖이어야 합니다.'}
    $metadataPath=Join-Path $codexRoot 'codex-package.json'
    Assert-OfflinePlainPath $metadataPath
    $metadataFile=Get-Item -LiteralPath $metadataPath
    if($metadataFile.PSIsContainer -or $metadataFile.Length -gt 1048576){throw 'Codex metadata must be a file of at most 1 MiB.'}
    $metadata=Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8|ConvertFrom-Json
    if($metadata.layoutVersion -ne 1 -or $metadata.version -notmatch '^\d+\.\d+\.\d+$' -or $metadata.target -ne 'x86_64-pc-windows-msvc' -or
       $metadata.entrypoint -ne 'bin/codex.exe' -or $metadata.resourcesDir -ne 'codex-resources' -or $metadata.pathDir -ne 'codex-path'){throw '공식 Windows x64 standalone 전체 패키지를 지정하세요. bin 폴더만으로는 준비할 수 없습니다.'}
    $signatures=@()
    foreach($relative in @('bin/codex.exe','bin/codex-code-mode-host.exe','codex-resources/codex-command-runner.exe','codex-resources/codex-windows-sandbox-setup.exe')) {
        $signatures+=Assert-OfflineSourcePublisher (Join-Path $codexRoot $relative) 'OpenAI OpCo, LLC'
    }
    $signatures+=Assert-OfflineSourcePublisher $ClaudeExecutable 'Anthropic, PBC'
    # FileVersion은 네트워크/CLI 실행 없이 읽는다. Claude 배포의 네이티브 제품 버전만 허용한다.
    $claudeVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($ClaudeExecutable)).ProductVersion
    if($claudeVersion -notmatch '^(\d+\.\d+\.\d+)(?:\.0)?$'){throw 'Claude ProductVersion을 확인할 수 없습니다. 공식 네이티브 CLI를 준비하세요.'}
    $claudeVersion=$matches[1]
    $sourceFiles=@([pscustomobject]@{Path='cli/codex/codex-package.json';Source=$metadataPath})
    foreach($directory in @('bin','codex-path','codex-resources')) {
        foreach($file in Get-OfflinePlainFiles (Join-Path $codexRoot $directory)) {
            $relative=$file.FullName.Substring($codexRoot.Length+1).Replace('\','/')
            $null=Resolve-OfflinePayloadPath $output ('cli/codex/'+$relative)
            $sourceFiles+=[pscustomobject]@{Path='cli/codex/'+$relative;Source=$file.FullName}
        }
    }
    $sourceFiles+=[pscustomobject]@{Path='cli/claude/claude.exe';Source=[IO.Path]::GetFullPath($ClaudeExecutable)}
    foreach($file in $sourceFiles) {
        $file|Add-Member -NotePropertyName ExpectedHash -NotePropertyValue (Get-FileHash -LiteralPath $file.Source).Hash
        $signed=@($signatures|Where-Object Name -CEQ ([IO.Path]::GetFileName($file.Source)))
        if($signed.Count -and $signed[0].Sha256 -ne $file.ExpectedHash){throw '서명 확인 뒤 실행파일이 변경됐습니다. 다시 준비하세요.'}
    }
    if(!$ExtensionPackageDirectory){
        $ExtensionPackageDirectory=if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'SHA256SUMS.txt')){$PSScriptRoot}else{Join-Path (Split-Path -Parent $PSScriptRoot) ('releases/v'+$PackageVersion)}
    }
    $extensionRoot=[IO.Path]::GetFullPath($ExtensionPackageDirectory)
    $sourceRoot=if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Install-AIAssistant.ps1')){$PSScriptRoot}else{throw '설치 도구 파일이 없습니다.'}
    $vsixNames=@("VS2017.15.7.AIAssistant-$PackageVersion.vsix","VS2017.AIAssistant-$PackageVersion.vsix","VS2019.AIAssistant-$PackageVersion.vsix","VS2022.AIAssistant-$PackageVersion.vsix",'SHA256SUMS.txt')
    foreach($name in $vsixNames){if(!(Test-Path -LiteralPath (Join-Path $extensionRoot $name) -PathType Leaf)){throw "확장 설치 파일이 없습니다: $name"}}
    $vsixHashes=@{}
    foreach($name in $vsixNames|Where-Object {$_ -like '*.vsix'}) {
        $entries=@([IO.File]::ReadAllLines((Join-Path $extensionRoot 'SHA256SUMS.txt'))|Where-Object {$_ -match ('^[A-Fa-f0-9]{64}\s+'+[regex]::Escape($name)+'$')})
        if($entries.Count -ne 1 -or (Get-FileHash -LiteralPath (Join-Path $extensionRoot $name)).Hash -ne $entries[0].Substring(0,64)){throw "확장 체크섬이 일치하지 않습니다: $name"}
        $vsixHashes[$name]=$entries[0].Substring(0,64)
    }
    $toolFiles=@('OfflineCli.Common.ps1','Install-OfflineCli.ps1','Install-AIAssistant.ps1','Test-OfflineSetupOnTarget.ps1')
    foreach($name in $toolFiles){if(!(Test-Path -LiteralPath (Join-Path $sourceRoot $name))){throw "필수 도구가 없습니다: $name"}}
    $repoRoot=Split-Path -Parent $PSScriptRoot
    $assetRoot=if(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'InstallOfflineCLI.bat')){$PSScriptRoot}else{$repoRoot}
    $guide=if(Test-Path -LiteralPath (Join-Path $assetRoot 'OFFLINE_INSTALL.md')){Join-Path $assetRoot 'OFFLINE_INSTALL.md'}else{Join-Path $repoRoot 'docs/OFFLINE_INSTALL.md'}
    foreach($p in @($guide,(Join-Path $assetRoot 'InstallOfflineCLI.bat'),(Join-Path $assetRoot 'InstallAIAssistant.bat'),(Join-Path $assetRoot 'VerifyOfflineSetup.bat'))){if(!(Test-Path -LiteralPath $p)){throw "안내/배치 파일이 없습니다: $p"}}
    $null=New-Item -ItemType Directory -Path $output
    $rows=@(foreach($row in $sourceFiles){
        $target=Resolve-OfflinePayloadPath $output $row.Path
        $null=New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
        Copy-Item -LiteralPath $row.Source -Destination $target
        if((Get-FileHash -LiteralPath $target).Hash -ne $row.ExpectedHash){throw '복사 중 원본이 변경됐습니다. 불완전한 출력은 보존하며 새 폴더로 다시 준비하세요.'}
        [pscustomobject]@{Path=$row.Path;Bytes=(Get-Item -LiteralPath $target).Length;Sha256=$row.ExpectedHash}
    })
    [pscustomobject]@{SchemaVersion=1;Architecture='amd64';BundleId=('codex-'+$metadata.version+'_claude-'+$claudeVersion+'_win-x64');CodexVersion=$metadata.version;ClaudeVersion=$claudeVersion;PreparedAt=[DateTimeOffset]::Now.ToString('o');Files=$rows;PublisherChecks=$signatures;ProfilesIncluded=$false;CliExecutedDuringPreparation=$false;AuthenticationCopied=$false}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $output 'offline-manifest.json') -Encoding UTF8
    foreach($name in $vsixNames){Copy-Item -LiteralPath (Join-Path $extensionRoot $name) -Destination $output}
    foreach($name in $vsixHashes.Keys){if((Get-FileHash -LiteralPath (Join-Path $output $name)).Hash -ne $vsixHashes[$name]){throw '확장 파일 복사 중 변경을 발견했습니다.'}}
    foreach($name in $toolFiles){Copy-Item -LiteralPath (Join-Path $sourceRoot $name) -Destination $output}
    foreach($name in @('InstallOfflineCLI.bat','InstallAIAssistant.bat','VerifyOfflineSetup.bat')){Copy-Item -LiteralPath (Join-Path $assetRoot $name) -Destination $output}
    Copy-Item -LiteralPath $guide -Destination (Join-Path $output 'OFFLINE_INSTALL.md')
    $null=Read-OfflineCliBundle $output
    $sums=@(Get-OfflinePlainFiles $output|ForEach-Object{(Get-FileHash -LiteralPath $_.FullName).Hash+'  '+$_.FullName.Substring($output.Length+1).Replace('\','/')})
    [IO.File]::WriteAllLines((Join-Path $output 'OFFLINE_SHA256SUMS.txt'),$sums,(New-Object Text.UTF8Encoding($false)))
    if($CreateZip) {
        Add-Type -AssemblyName System.IO.Compression;Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip=[IO.Compression.ZipFile]::Open($output+'.zip',[IO.Compression.ZipArchiveMode]::Create)
        try{foreach($file in Get-OfflinePlainFiles $output){$null=[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$file.FullName.Substring($output.Length+1).Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)}}finally{$zip.Dispose()}
        ((Get-FileHash -LiteralPath ($output+'.zip')).Hash+'  '+[IO.Path]::GetFileName($output+'.zip'))|Set-Content -LiteralPath ($output+'.zip.sha256') -Encoding ASCII
        Write-Host "이동할 ZIP: $output.zip"
    }
    Write-Host "준비 완료: $output"
    Write-Host "Codex $($metadata.version) 전체 런타임 + Claude $claudeVersion / CLI 파일 $($rows.Count)개"
    Write-Host '폴더 전체를 오프라인 PC로 옮겨 InstallOfflineCLI.bat와 InstallAIAssistant.bat를 실행하세요.'
}catch{Write-Error $_;exit 1}
