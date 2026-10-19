#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Install-OfflineCli
desc         : 인터넷·CLI 실행 없이 검증된 로컬 런타임을 새 사용자 전용 폴더에 설치한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-038 기존 CLI·PATH·계정·VS 설정을 보존.
               2026-10-19 CodexOrig : TASK-041 제한된 토큰에서 상위 경로를 조회하지 않고 동일 부모 안에서 이동.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$BundleDirectory=$PSScriptRoot,[string]$DestinationRoot=(Join-Path $env:LOCALAPPDATA 'Programs/VisualStudioAIAssistant/OfflineCLI'),[switch]$Install,[string]$ReportPath)
$ErrorActionPreference='Stop'
$common=Join-Path $PSScriptRoot 'OfflineCli.Common.ps1'
if(!(Test-Path -LiteralPath $common)){$common=Join-Path $PSScriptRoot 'scripts/OfflineCli.Common.ps1'}
. $common
try {
    if(!$ReportPath){$ReportPath=Join-Path ([IO.Path]::GetTempPath()) ('OfflineCLI-install-'+[guid]::NewGuid().ToString('N')+'.json')}
    Assert-OfflinePlainPath $ReportPath
    if([IO.Path]::GetExtension($ReportPath) -ne '.json' -or (Test-Path -LiteralPath $ReportPath)){throw '새 JSON 로그 경로를 지정하세요. 기존 로그는 보존합니다.'}
    if(!(Test-Path -LiteralPath (Split-Path -Parent ([IO.Path]::GetFullPath($ReportPath))) -PathType Container)){throw '로그 폴더를 먼저 만들고 새 JSON 경로를 지정하세요.'}
    $manifest=Read-OfflineCliBundle $BundleDirectory
    $architecture=if($env:PROCESSOR_ARCHITEW6432){$env:PROCESSOR_ARCHITEW6432}else{$env:PROCESSOR_ARCHITECTURE}
    if($architecture -ne 'AMD64'){throw '이 묶음은 Windows x64 PC용입니다. ARM64/x86에서는 설치하지 않습니다.'}
    $windows=[Microsoft.Win32.Registry]::LocalMachine.OpenSubKey('SOFTWARE\Microsoft\Windows NT\CurrentVersion')
    try{$build=[int]$windows.GetValue('CurrentBuildNumber')}finally{$windows.Dispose()}
    if($build -lt 17763){throw '동봉 Claude CLI는 Windows 10 1809/Server 2019 이상이 필요합니다. VS 버전과 Windows 지원 버전은 별도입니다.'}
    $root=[IO.Path]::GetFullPath($DestinationRoot).TrimEnd('\')
    Assert-OfflinePlainPath $root
    $destination=Join-Path $root $manifest.BundleId
    $state='VerifiedOnly'
    if($Install) {
        if(Test-Path -LiteralPath $destination) {
            Assert-OfflinePlainPath $destination
            foreach($row in $manifest.Files){$file=Resolve-OfflinePayloadPath $destination $row.Path;if((Get-FileHash -LiteralPath $file).Hash -ne $row.Sha256){throw '기존 대상 내용이 다릅니다. 새 DestinationRoot를 지정하세요. 기존 파일은 보존합니다.'}}
            if(@(Get-OfflinePlainFiles (Join-Path $destination 'cli')).Count -ne @($manifest.Files).Count){throw '기존 설치에 예상하지 않은 파일이 있어 변경하지 않습니다.'}
            $state='AlreadyInstalled'
        }else{
            $null=New-Item -ItemType Directory -Path $root -Force
            $stage=Join-Path $root ('.install-'+[guid]::NewGuid().ToString('N'))
            $null=New-Item -ItemType Directory -Path $stage
            foreach($row in $manifest.Files) {
                $source=Resolve-OfflinePayloadPath $BundleDirectory $row.Path
                $target=Resolve-OfflinePayloadPath $stage $row.Path
                $null=New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force
                Copy-Item -LiteralPath $source -Destination $target
                if((Get-FileHash -LiteralPath $target).Hash -ne $row.Sha256){throw "복사 검증 실패. 불완전한 준비 폴더를 보존합니다: $stage"}
            }
            if(!$stage.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase) -or !$destination.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing an unexpected directory move.'}
            Assert-OfflinePlainPath $root
            Assert-OfflinePlainPath $stage
            Assert-OfflinePlainPath $destination
            if(Test-Path -LiteralPath $destination){throw '설치 중 대상 폴더가 생겼습니다. 기존 내용을 변경하지 않습니다.'}
            [IO.Directory]::Move($stage,$destination)
            $state='Installed'
        }
    }
    $report=[pscustomobject]@{SchemaVersion=1;Status=$state;CheckedAt=[DateTimeOffset]::Now.ToString('o');BundleId=$manifest.BundleId;FilesVerified=@($manifest.Files).Count;CodexVersion=$manifest.CodexVersion;ClaudeVersion=$manifest.ClaudeVersion;Destination=$destination;CodexExecutable=(Join-Path $destination 'cli/codex/bin/codex.exe');ClaudeExecutable=(Join-Path $destination 'cli/claude/claude.exe');NetworkRequestsPerformed=$false;CliProcessesStarted=$false;LoginPerformed=$false;PathChanged=$false;SettingsChanged=$false}
    $report|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $ReportPath -Encoding UTF8
    Write-Host "오프라인 CLI 결과: $state / 검증 $($report.FilesVerified)개"
    Write-Host "Codex 경로: $($report.CodexExecutable)"
    Write-Host "Claude 경로: $($report.ClaudeExecutable)"
    Write-Host "로그: $ReportPath"
    Write-Host 'VS 설정에 위 경로를 지정하세요. 로그인·AI 사용은 네트워크 연결 후 본인 계정으로 진행합니다.'
}catch{Write-Error $_;exit 1}
