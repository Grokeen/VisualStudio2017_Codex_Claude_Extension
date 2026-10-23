#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : OfflineCli.Common
desc         : 계정 자료를 제외한 오프라인 CLI 묶음의 경로·무결성·실행파일 구조를 검사한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-038 설치 단계에서 외부 연결·CLI 실행 없이 검증.
-------------------------------------------------------------------------
#>
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'

<#
-------------------------------------------------------------------------
name         : Assert-OfflinePlainPath
desc         : 기존 경로와 상위 폴더에 junction·symlink가 있으면 경로 우회를 차단한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 대상 밖 복사와 기존 설치의 간접 변경 방지.
-------------------------------------------------------------------------
#>
function Assert-OfflinePlainPath {
    param([string]$Path)
    $current=[IO.Path]::GetFullPath($Path)
    if($current.StartsWith('\\') -or [IO.Path]::GetPathRoot($current) -notmatch '^[A-Za-z]:\\$'){throw '오프라인 설치에는 USB/로컬 드라이브 경로를 사용하세요. UNC/장치 경로는 허용하지 않습니다.'}
    $drive=New-Object IO.DriveInfo([IO.Path]::GetPathRoot($current))
    if($drive.DriveType -eq [IO.DriveType]::Network){throw '네트워크 드라이브 대신 USB/로컬 드라이브를 사용하세요.'}
    $volume=[IO.Path]::GetPathRoot($current)
    $cursor=$volume
    foreach($segment in $current.Substring($volume.Length).Split('\')) {
        if(!$segment){continue}
        $cursor=Join-Path $cursor $segment
        # 상위 폴더부터 조사하고 링크 대상 존재 여부를 먼저 조회하지 않는다.
        $item=Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
        if($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "연결 경로는 사용하지 않습니다: $cursor"}
    }
}

<#
-------------------------------------------------------------------------
name         : Get-OfflinePlainFiles
desc         : 연결 폴더를 따라가지 않고 로컬 파일 트리만 제한된 깊이로 열거한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 계정 프로필·경로 우회가 묶음에 유입되지 않도록 검사.
-------------------------------------------------------------------------
#>
function Get-OfflinePlainFiles {
    param([string]$Root)
    Assert-OfflinePlainPath $Root
    $pending=New-Object 'System.Collections.Generic.Queue[string]';$pending.Enqueue([IO.Path]::GetFullPath($Root))
    $count=0
    while($pending.Count) {
        $directory=$pending.Dequeue()
        foreach($item in Get-ChildItem -LiteralPath $directory -Force) {
            if(($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "연결된 파일/폴더가 있습니다: $($item.FullName)"}
            if(++$count -gt 8192){throw '묶음 파일/폴더 수 상한을 넘었습니다.'}
            if($item.PSIsContainer){$pending.Enqueue($item.FullName)}else{$item}
        }
    }
}

<#
-------------------------------------------------------------------------
name         : Resolve-OfflinePayloadPath
desc         : CLI 전용 상대 경로만 허용하며 상위 이동·ADS·숨김 계정 경로를 차단한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 대소문자 중복과 Windows 경로 이탈 검사의 공통 경계.
-------------------------------------------------------------------------
#>
function Resolve-OfflinePayloadPath {
    param([string]$Root,[string]$Relative)
    if($Relative -notmatch '^cli/(codex/(codex-package\.json|(bin|codex-path|codex-resources)/[A-Za-z0-9_][A-Za-z0-9_./-]*)|claude/claude\.exe)$' -or
        $Relative -match '(^|/)(\.{1,2}|auth\.json|config\.toml|settings\.json|sessions|history\.jsonl|\.credentials[^/]*)(/|$)' -or
        @($Relative.Split('/')|Where-Object {$_ -match '[. ]$' -or $_ -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)'}).Count){throw "허용하지 않은 CLI 경로: $Relative"}
    $base=[IO.Path]::GetFullPath($Root).TrimEnd('\')
    $path=[IO.Path]::GetFullPath((Join-Path $base $Relative))
    if(!$path.StartsWith($base+'\',[StringComparison]::OrdinalIgnoreCase)){throw '묶음 밖 경로입니다.'}
    Assert-OfflinePlainPath $path
    return $path
}

<#
-------------------------------------------------------------------------
name         : Get-OfflinePeArchitecture
desc         : 프로그램을 실행하지 않고 PE 헤더로 아키텍처를 판별한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 오프라인 설치 시 --version·네이티브 설치기를 실행하지 않음.
-------------------------------------------------------------------------
#>
function Get-OfflinePeArchitecture {
    param([string]$Path)
    $reader=New-Object IO.BinaryReader([IO.File]::OpenRead($Path))
    try {
        if($reader.ReadUInt16() -ne 0x5a4d){throw 'PE DOS header mismatch.'}
        $reader.BaseStream.Position=0x3c;$offset=$reader.ReadInt32()
        if($offset -lt 64 -or $offset -gt $reader.BaseStream.Length-6){throw 'PE offset is invalid.'}
        $reader.BaseStream.Position=$offset
        if($reader.ReadUInt32() -ne 0x4550){throw 'PE signature mismatch.'}
        switch($reader.ReadUInt16()){0x8664{return 'amd64'} 0xaa64{return 'arm64'} 0x014c{return 'x86'} default{throw 'Unsupported PE machine.'}}
    }finally{$reader.Dispose()}
}

<#
-------------------------------------------------------------------------
name         : Read-OfflineCliBundle
desc         : 필수 Codex 런타임 전체와 Claude 본체를 먼저 검증한 후 복사를 허용한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : JSON 상한·중복·경로·크기·SHA·추가 파일과 누락 보조 도구 확인.
-------------------------------------------------------------------------
#>
function Read-OfflineCliBundle {
    param([string]$Directory)
    $root=[IO.Path]::GetFullPath($Directory)
    Assert-OfflinePlainPath $root
    $path=Join-Path $root 'offline-manifest.json'
    Assert-OfflinePlainPath $path
    $manifestFile=Get-Item -LiteralPath $path -Force
    if($manifestFile.PSIsContainer -or $manifestFile.Length -gt 1048576){throw 'Manifest must be a file of at most 1 MiB.'}
    $manifest=Get-Content -LiteralPath $path -Raw -Encoding UTF8|ConvertFrom-Json
    if($manifest.SchemaVersion -ne 1 -or $manifest.Architecture -ne 'amd64' -or
        $manifest.CodexVersion -notmatch '^\d+\.\d+\.\d+$' -or $manifest.ClaudeVersion -notmatch '^\d+\.\d+\.\d+$') {throw '지원하지 않는 묶음 형식/버전/아키텍처입니다.'}
    $id='codex-'+$manifest.CodexVersion+'_claude-'+$manifest.ClaudeVersion+'_win-x64'
    if($manifest.BundleId -cne $id){throw '묶음 ID가 버전과 일치하지 않습니다.'}
    $files=@($manifest.Files)
    if(!$files.Count -or $files.Count -gt 4096){throw 'Manifest file count is invalid.'}
    $names=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach($row in $files) {
        if(!$names.Add([string]$row.Path)){throw 'Duplicate CLI manifest path.'}
        $file=Resolve-OfflinePayloadPath $root $row.Path
        if($row.Sha256 -notmatch '^[A-Fa-f0-9]{64}$' -or $row.Bytes -le 0 -or $row.Bytes -gt 1073741824){throw 'Invalid file size/hash declaration.'}
        if((Get-Item -LiteralPath $file).Length -ne $row.Bytes -or (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $row.Sha256){throw "묶음 파일이 손상되거나 변경됐습니다: $($row.Path)"}
    }
    $required=@('cli/codex/codex-package.json','cli/codex/bin/codex.exe','cli/codex/bin/codex-code-mode-host.exe','cli/codex/codex-path/rg.exe','cli/codex/codex-resources/codex-command-runner.exe','cli/codex/codex-resources/codex-windows-sandbox-setup.exe','cli/claude/claude.exe')
    foreach($name in $required){if(!$names.Contains($name)){throw "필수 실행 자료가 없습니다: $name"}}
    $metadataPath=Join-Path $root 'cli/codex/codex-package.json'
    if((Get-Item -LiteralPath $metadataPath).Length -gt 1048576){throw 'Codex metadata exceeds 1 MiB.'}
    $metadata=Get-Content -LiteralPath $metadataPath -Raw -Encoding UTF8|ConvertFrom-Json
    if($metadata.layoutVersion -ne 1 -or $metadata.version -ne $manifest.CodexVersion -or $metadata.target -ne 'x86_64-pc-windows-msvc' -or
        $metadata.entrypoint -ne 'bin/codex.exe' -or $metadata.resourcesDir -ne 'codex-resources' -or $metadata.pathDir -ne 'codex-path'){throw 'Codex standalone package metadata differs.'}
    foreach($name in $required|Where-Object {$_ -like '*.exe'}){if((Get-OfflinePeArchitecture (Join-Path $root $name)) -ne 'amd64'){throw "x64 실행파일이 아닙니다: $name"}}
    $actual=@(Get-OfflinePlainFiles (Join-Path $root 'cli'))
    if($actual.Count -ne $files.Count){throw 'CLI 폴더에 선언하지 않은 파일이 있습니다.'}
    foreach($file in $actual){$relative=$file.FullName.Substring($root.TrimEnd('\').Length+1).Replace('\','/');if(!$names.Contains($relative)){throw 'Unlisted CLI payload file.'}}
    return $manifest
}
