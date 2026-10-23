#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-OfflineSetupOnTarget
desc         : 실제 대상 PC에서 로컬 CLI·VSIX 설치와 설치 파일 확인 결과를 한 폴더에 기록한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-041 오프라인 사용자 확인과 실제 설치·IDE 확인을 구분한다.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$BundleDirectory=$PSScriptRoot,[string]$OutputDirectory,[string]$InstanceId,[switch]$Install,[switch]$LibraryOnly)
$ErrorActionPreference='Stop'

<#
-------------------------------------------------------------------------
name         : Find-OfflineInstalledExtension
desc         : 선택 IDE의 사용자 확장 폴더에서 제품 ID·버전·DLL 해시가 일치하는 설치본을 찾는다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 다른 확장·설정·로그인 정보를 변경하지 않는다.
-------------------------------------------------------------------------
#>
function Find-OfflineInstalledExtension {
    param([string]$Directory,[string]$ProductId,[string]$Version,[string]$DllHash)
    Assert-OfflinePlainPath $Directory
    if(!(Test-Path -LiteralPath $Directory -PathType Container)){return}
    $directories=@(Get-ChildItem -LiteralPath $Directory -Directory -Force)
    foreach($folder in $directories) {
        if(($folder.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){continue}
        $path=Join-Path $folder.FullName 'extension.vsixmanifest'
        try {
            Assert-OfflinePlainPath $path
            if(!(Test-Path -LiteralPath $path -PathType Leaf)){continue}
            if((Get-Item -LiteralPath $path).Length -gt 1048576){continue}
            $settings=New-Object Xml.XmlReaderSettings;$settings.DtdProcessing=[Xml.DtdProcessing]::Prohibit;$settings.XmlResolver=$null
            $reader=[Xml.XmlReader]::Create($path,$settings)
            try{$xml=New-Object Xml.XmlDocument;$xml.XmlResolver=$null;$xml.Load($reader)}finally{$reader.Dispose()}
            $ns=New-Object Xml.XmlNamespaceManager($xml.NameTable);$ns.AddNamespace('v','http://schemas.microsoft.com/developer/vsx-schema/2011')
            $identity=$xml.SelectSingleNode('/v:PackageManifest/v:Metadata/v:Identity',$ns)
            if(!$identity -or $identity.GetAttribute('Id') -cne $ProductId -or $identity.GetAttribute('Version') -cne $Version){continue}
            $dll=Join-Path $folder.FullName 'VS2017.AIAssistant.dll';Assert-OfflinePlainPath $dll
            if((Get-FileHash -LiteralPath $dll).Hash -eq $DllHash){[pscustomobject]@{Directory=$folder.FullName;ProductId=$ProductId;Version=$Version;DllSha256=$DllHash}}
        }catch{continue}
    }
}

<#
-------------------------------------------------------------------------
name         : Invoke-OfflineTargetVersion
desc         : 새 임시 프로필과 인증 환경 변수 제거 상태에서 설치 CLI의 버전만 실행한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 모델 요청·로그인·업데이트 없이 시작 여부를 확인한다.
-------------------------------------------------------------------------
#>
function Invoke-OfflineTargetVersion {
    param([string]$Name,[string]$Executable,[string]$Expected,[string]$Profile)
    $null=New-Item -ItemType Directory -Path $Profile
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$Executable;$info.Arguments='--version';$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
    $info.EnvironmentVariables['CODEX_HOME']=$Profile;$info.EnvironmentVariables['CLAUDE_CONFIG_DIR']=$Profile;$info.EnvironmentVariables['DISABLE_AUTOUPDATER']='1'
    foreach($key in @('OPENAI_API_KEY','CODEX_API_KEY','OPENAI_ACCESS_TOKEN','ANTHROPIC_API_KEY','ANTHROPIC_AUTH_TOKEN','CLAUDE_CODE_OAUTH_TOKEN')){$info.EnvironmentVariables.Remove($key)}
    $process=New-Object Diagnostics.Process;$process.StartInfo=$info
    try {
        if(!$process.Start()){throw 'CLI 시작 실패.'}
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(15000)){$process.Kill();throw 'CLI 버전 확인 시간 초과.'}
        $actual=$stdout.GetAwaiter().GetResult().Trim()
        if($process.ExitCode -ne 0 -or $actual -notmatch $Expected){throw ('CLI 버전 확인 실패: '+$Name)}
        [pscustomobject]@{Tool=$Name;Version=$actual;ExitCode=$process.ExitCode;Sha256=(Get-FileHash -LiteralPath $Executable).Hash;Passed=$true}
    }finally{$process.Dispose()}
}

if($LibraryOnly){return}
$common=Join-Path $BundleDirectory 'OfflineCli.Common.ps1';. $common
if(!$OutputDirectory){$OutputDirectory=Join-Path ([IO.Path]::GetTempPath()) ('OfflineSetupVerify-'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
Assert-OfflinePlainPath $OutputDirectory
if(Test-Path -LiteralPath $OutputDirectory){throw '새 결과 폴더를 지정하세요. 기존 결과는 보존합니다.'}
$null=New-Item -ItemType Directory -Path $OutputDirectory
$summary=[ordered]@{Task='TASK-041';StartedAt=[DateTimeOffset]::Now.ToString('o');Mode=$(if($Install){'Install'}else{'DiagnosticOnly'});Status='Started';UserConfirmedInstallDownloadNetworkBlocked=$false;NetworkIsolationEnforcedByTool=$false;AuthenticationPerformed=$false;ModelRequestPerformed=$false;IdeLaunchedByTool=$false;IdeToolWindowConfirmed=$false;OutputDirectory=$OutputDirectory;Steps=@()}
try {
    $manifest=Read-OfflineCliBundle $BundleDirectory
    $summary.BundleId=$manifest.BundleId
    & (Join-Path $BundleDirectory 'Install-OfflineCli.ps1') -BundleDirectory $BundleDirectory -ReportPath (Join-Path $OutputDirectory 'cli-preflight.json') *> (Join-Path $OutputDirectory 'cli-preflight.log')
    & (Join-Path $BundleDirectory 'Install-AIAssistant.ps1') -PackageDirectory $BundleDirectory -ReportPath (Join-Path $OutputDirectory 'vsix-preflight.json') *> (Join-Path $OutputDirectory 'vsix-preflight.log')
    $preflight=Get-Content (Join-Path $OutputDirectory 'vsix-preflight.json') -Raw -Encoding UTF8|ConvertFrom-Json
    $ready=@($preflight.Plans|Where-Object Status -EQ 'Ready')
    if($InstanceId){$ready=@($ready|Where-Object InstanceId -CEQ $InstanceId)}
    if(!$ready.Count){throw '설치 가능한 IDE가 없습니다. vsix-preflight.json을 확인하세요.'}
    $summary.Steps+=@{Name='Local CLI and VSIX preflight';Passed=$true}
    $summary.Status='ReadyDiagnosticOnly'
    if($Install) {
        Write-Host '이 검사는 실제 CLI와 선택한 Visual Studio 확장을 설치합니다. 로그인이나 AI 요청은 하지 않습니다.'
        if((Read-Host '현재 PC는 설치용 인터넷 접속이 차단된 오프라인 테스트 PC입니까? Y/N') -notmatch '^[Yy]$'){throw '오프라인 대상 PC에서 다시 실행하세요. 설치는 시작하지 않았습니다.'}
        $summary.UserConfirmedInstallDownloadNetworkBlocked=$true
        if(Get-Process -Name devenv -ErrorAction SilentlyContinue){throw 'Visual Studio 창을 모두 닫고 새 결과 폴더로 다시 실행하세요.'}
        if($ready.Count -gt 1) {
            for($i=0;$i -lt $ready.Count;$i++){Write-Host ('{0}. {1} ({2})' -f ($i+1),$ready[$i].DisplayName,$ready[$i].InstallationVersion)}
            $selection=Read-Host '설치할 Visual Studio 번호';$index=0
            if(![int]::TryParse($selection,[ref]$index) -or $index -lt 1 -or $index -gt $ready.Count){throw '설치 대상 선택 취소.'}
            $ready=@($ready[$index-1])
        }
        $selected=$ready[0];$summary.SelectedInstance=$selected
        & (Join-Path $BundleDirectory 'Install-OfflineCli.ps1') -BundleDirectory $BundleDirectory -Install -ReportPath (Join-Path $OutputDirectory 'cli-installed.json') *> (Join-Path $OutputDirectory 'cli-install.log')
        $receipt=Get-Content (Join-Path $OutputDirectory 'cli-installed.json') -Raw -Encoding UTF8|ConvertFrom-Json
        $summary.CliInstallation=$receipt.Status
        $summary.CliVersionChecks=@(Invoke-OfflineTargetVersion 'Codex' $receipt.CodexExecutable ('^codex-cli '+[regex]::Escape($manifest.CodexVersion)+'$') (Join-Path $OutputDirectory 'codex-profile');Invoke-OfflineTargetVersion 'Claude' $receipt.ClaudeExecutable ('^'+[regex]::Escape($manifest.ClaudeVersion)+' \(Claude Code\)$') (Join-Path $OutputDirectory 'claude-profile'))
        $summary.Steps+=@{Name='CLI install and isolated-profile version checks';Passed=$true}
        & (Join-Path $BundleDirectory 'Install-AIAssistant.ps1') -PackageDirectory $BundleDirectory -InstanceId $selected.InstanceId -Install -ReportPath (Join-Path $OutputDirectory 'vsix-install.json') *> (Join-Path $OutputDirectory 'vsix-install.log')
        $summary.Steps+=@{Name='Official VSIXInstaller returned success';Passed=$true}
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip=[IO.Compression.ZipFile]::OpenRead($selected.Package.Path);$sha=[Security.Cryptography.SHA256]::Create()
        try{$stream=$zip.GetEntry('VS2017.AIAssistant.dll').Open();try{$dllHash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose()}}finally{$sha.Dispose();$zip.Dispose()}
        $major=([version]$selected.InstallationVersion).Major
        $extensionRoot=Join-Path $env:LOCALAPPDATA ('Microsoft/VisualStudio/'+$major+'.0_'+$selected.InstanceId+'/Extensions')
        $summary.InstalledFiles=@(Find-OfflineInstalledExtension $extensionRoot ('Grokeen.VS'+$selected.Year+'.AIAssistant') $selected.Package.Version $dllHash)
        $summary.Status=if($summary.InstalledFiles.Count){'InstalledPendingIdeCheck'}else{'InstallerSucceededPendingInstalledFileCheck'}
        Write-Host 'Visual Studio를 열고 도구 → AI Assistant 창이 열리는지 확인하세요. 실제 AI 응답은 연결·로그인 후 별도 확인합니다.'
    }
}catch{
    $summary.Status='Failed';$summary.Failure=$_.Exception.Message
    ($_|Format-List * -Force|Out-String)|Set-Content (Join-Path $OutputDirectory 'failure.txt') -Encoding UTF8
}finally{
    $summary.CompletedAt=[DateTimeOffset]::Now.ToString('o')
    $summary|ConvertTo-Json -Depth 14|Set-Content (Join-Path $OutputDirectory 'RESULT.json') -Encoding UTF8
    Write-Host ('검사 결과: '+$summary.Status)
    Write-Host ('결과 폴더: '+$OutputDirectory)
    Write-Host '결과를 알려주실 때 상태와 오류 문구를 전달하세요. 계정·토큰은 보내지 마세요.'
}
if($summary.Status -eq 'Failed'){exit 1}
