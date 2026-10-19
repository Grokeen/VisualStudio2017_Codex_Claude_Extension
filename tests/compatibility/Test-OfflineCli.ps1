#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Test-OfflineCli
desc         : 가짜 PE 자료로 오프라인 설치의 다운로드·실행 금지와 복사·경로 경계를 검사한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : TASK-038 손상·누락·프로필·연결 경로·기존 설치 보존 회귀.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $repo ('artifacts/offline-tests/'+[guid]::NewGuid().ToString('N'))}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory);$null=New-Item -ItemType Directory -Path $OutputDirectory -Force
. (Join-Path $repo 'scripts/OfflineCli.Common.ps1')
Add-Type @'
using System.Runtime.InteropServices;
/// ---------------------------------------------------------------------
/// <summary>
/// name         : OfflineTestLinks
/// desc         : Windows 10 테스트에서만 비관리자 symbolic link 생성 플래그를 사용한다.
/// author       : CodexOrig
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig : TASK-040 manifest 경계 회귀.
/// </summary>
/// ---------------------------------------------------------------------
public static class OfflineTestLinks {
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : CreateSymbolicLink
    /// desc         : 비관리자 테스트 링크를 만들며 설치 제품에는 포함되지 않는다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-040 링크 경계 회귀용 호출.
    /// </summary>
    /// ---------------------------------------------------------------------
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool CreateSymbolicLink(string link, string target, int flags);
}
'@
$installer=Join-Path $repo 'scripts/Install-OfflineCli.ps1'
$results=New-Object 'System.Collections.Generic.List[object]'
$template=Join-Path $OutputDirectory 'template';$null=New-Item -ItemType Directory -Path $template
$source=Join-Path $OutputDirectory 'NeverRun.cs'
@'
using System;
using System.IO;
/// ---------------------------------------------------------------------
/// <summary>
/// name         : NeverRun
/// desc         : 설치 검사 중 CLI 실행 여부를 파일 표식으로 검출하는 대역이다.
/// author       : CodexOrig
/// create date  : 2026-10-19
/// update date  : 2026-10-19 CodexOrig : TASK-038 미실행 검증 전용 자료.
/// </summary>
/// ---------------------------------------------------------------------
class NeverRun
{
    /// ---------------------------------------------------------------------
    /// <summary>
    /// name         : Main
    /// desc         : 잘못 실행되었을 때만 표식을 기록하고 실패 코드를 반환한다.
    /// author       : CodexOrig
    /// create date  : 2026-10-19
    /// update date  : 2026-10-19 CodexOrig : TASK-038 CLI 시작을 탐지한다.
    /// </summary>
    /// ---------------------------------------------------------------------
    static int Main()
    {
        File.WriteAllText(Environment.GetEnvironmentVariable("OFFLINE_TEST_MARKER"), "EXECUTED");
        return 79;
    }
}
'@|Set-Content -LiteralPath $source -Encoding UTF8
$exe=Join-Path $OutputDirectory 'NeverRun.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /platform:x64 "/out:$exe" $source
if($LASTEXITCODE -ne 0){throw 'Fixture compiler failed.'}
$required=@('cli/codex/bin/codex.exe','cli/codex/bin/codex-code-mode-host.exe','cli/codex/codex-path/rg.exe','cli/codex/codex-resources/codex-command-runner.exe','cli/codex/codex-resources/codex-windows-sandbox-setup.exe','cli/claude/claude.exe')
foreach($name in $required){$path=Join-Path $template $name;$null=New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force;Copy-Item -LiteralPath $exe -Destination $path}
@{layoutVersion=1;version='0.163.0';target='x86_64-pc-windows-msvc';entrypoint='bin/codex.exe';resourcesDir='codex-resources';pathDir='codex-path'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $template 'cli/codex/codex-package.json') -Encoding UTF8

<#
-------------------------------------------------------------------------
name         : Save-OfflineTestManifest
desc         : 테스트 자료의 선언 해시를 만들며 제품 설치기와 독립된 기대 조건을 제공한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 단순 손상과 선언 변조 뒤 경로 검사를 구분.
-------------------------------------------------------------------------
#>
function Save-OfflineTestManifest {
    param([string]$Root)
    $rows=@(Get-ChildItem -LiteralPath (Join-Path $Root 'cli') -File -Recurse|ForEach-Object{[pscustomobject]@{Path=$_.FullName.Substring($Root.Length+1).Replace('\','/');Bytes=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})
    [pscustomobject]@{SchemaVersion=1;Architecture='amd64';BundleId='codex-0.163.0_claude-2.1.302_win-x64';CodexVersion='0.163.0';ClaudeVersion='2.1.302';Files=$rows}|ConvertTo-Json -Depth 6|Set-Content -LiteralPath (Join-Path $Root 'offline-manifest.json') -Encoding UTF8
}
Save-OfflineTestManifest $template

<#
-------------------------------------------------------------------------
name         : Invoke-OfflineRejectCase
desc         : 새 사본에 하나의 잘못된 입력을 적용해 복사 전에 거부되는지 확인한다.
author       : CodexOrig
create date  : 2026-10-19
update date  : 2026-10-19 CodexOrig : 최초 검사 결과와 손상된 입력을 개별 보존.
-------------------------------------------------------------------------
#>
function Invoke-OfflineRejectCase {
    param([string]$Name,[scriptblock]$Mutation)
    $caseRoot=Join-Path $OutputDirectory ('case-'+$results.Count)
    Copy-Item -LiteralPath $template -Destination $caseRoot -Recurse
    $manifest=Get-Content -LiteralPath (Join-Path $caseRoot 'offline-manifest.json') -Raw -Encoding UTF8|ConvertFrom-Json
    & $Mutation $caseRoot $manifest
    $rejected=$false;$message=''
    try{$null=Read-OfflineCliBundle $caseRoot}catch{$rejected=$true;$message=$_.Exception.Message}
    $results.Add([pscustomobject]@{Name=$Name;Passed=$rejected;Detail=$message})
    if(!$rejected){throw "Expected rejection: $Name"}
    Write-Host "PASS $Name"
}
try {
    $valid=Read-OfflineCliBundle $template
    if(@($valid.Files).Count -ne 7){throw 'Valid fixture incomplete.'}
    $results.Add([pscustomobject]@{Name='Complete standalone payload is accepted';Passed=$true})
    Invoke-OfflineRejectCase 'Tampered bytes rejected' {param($r,$m) [IO.File]::AppendAllText((Join-Path $r 'cli/claude/claude.exe'),'bad')}
    Invoke-OfflineRejectCase 'Missing code-mode host rejected' {param($r,$m) $m.Files=@($m.Files|Where-Object Path -NE 'cli/codex/bin/codex-code-mode-host.exe');$m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $r 'offline-manifest.json') -Encoding UTF8}
    Invoke-OfflineRejectCase 'Missing sandbox helper rejected' {param($r,$m) $m.Files=@($m.Files|Where-Object Path -NE 'cli/codex/codex-resources/codex-windows-sandbox-setup.exe');$m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $r 'offline-manifest.json') -Encoding UTF8}
    foreach($badPath in @('../outside.exe','C:/outside.exe','cli/codex/bin/../../outside.exe','cli/codex/bin/a.exe:stream','cli/codex/bin/auth.json','cli/codex/bin/con.exe','cli/codex/bin/file.')) {
        Invoke-OfflineRejectCase "Unsafe path rejected: $badPath" {param($r,$m) $m.Files[0].Path=$badPath;$m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $r 'offline-manifest.json') -Encoding UTF8}
    }
    Invoke-OfflineRejectCase 'Duplicate declaration rejected' {param($r,$m) $m.Files+=@($m.Files[0]);$m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $r 'offline-manifest.json') -Encoding UTF8}
    Invoke-OfflineRejectCase 'Unlisted credentials rejected' {param($r,$m) '{}'|Set-Content -LiteralPath (Join-Path $r 'cli/codex/auth.json') -Encoding UTF8}
    Invoke-OfflineRejectCase 'Altered Codex entrypoint rejected' {param($r,$m) $p=Join-Path $r 'cli/codex/codex-package.json';$j=Get-Content -LiteralPath $p -Raw|ConvertFrom-Json;$j.entrypoint='cmd.exe';$j|ConvertTo-Json|Set-Content -LiteralPath $p -Encoding UTF8;Save-OfflineTestManifest $r}
    Invoke-OfflineRejectCase 'ARM64 executable rejected after valid new hash' {param($r,$m) $p=Join-Path $r 'cli/claude/claude.exe';$bytes=[IO.File]::ReadAllBytes($p);$offset=[BitConverter]::ToInt32($bytes,60);$bytes[$offset+4]=0x64;$bytes[$offset+5]=0xaa;[IO.File]::WriteAllBytes($p,$bytes);Save-OfflineTestManifest $r}
    Invoke-OfflineRejectCase 'Unsupported manifest schema rejected' {param($r,$m) $m.SchemaVersion=2;$m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $r 'offline-manifest.json') -Encoding UTF8}
    Invoke-OfflineRejectCase 'Bundle ID traversal rejected' {param($r,$m) $m.BundleId='../../outside';$m|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $r 'offline-manifest.json') -Encoding UTF8}
    $marker=Join-Path $OutputDirectory 'CLI-MUST-NOT-RUN.txt';$env:OFFLINE_TEST_MARKER=$marker
    $destination=Join-Path $OutputDirectory '설치 & 한글 경로'
    $userPathBefore=[Environment]::GetEnvironmentVariable('Path','User')
    foreach($mode in @('check','install','repeat')) {
        $report=Join-Path $OutputDirectory ($mode+'.json');$argsList=@('-NoProfile','-ExecutionPolicy','Bypass','-File',$installer,'-BundleDirectory',$template,'-DestinationRoot',$destination,'-ReportPath',$report)
        if($mode -ne 'check'){$argsList+='-Install'}
        & powershell.exe @argsList *> (Join-Path $OutputDirectory ($mode+'.log'))
        if($LASTEXITCODE -ne 0){throw "Offline subprocess failed: $mode"}
        $data=Get-Content -LiteralPath $report -Raw -Encoding UTF8|ConvertFrom-Json
        $expected=switch($mode){check{'VerifiedOnly'} install{'Installed'} repeat{'AlreadyInstalled'}}
        if($data.Status -ne $expected -or $data.NetworkRequestsPerformed -or $data.CliProcessesStarted -or (Test-Path -LiteralPath $marker)){throw 'Offline mode or execution policy differs.'}
        if($mode -eq 'check' -and (Test-Path -LiteralPath $destination)){throw 'Read-only mode created an install directory.'}
        $results.Add([pscustomobject]@{Name="CLI $mode mode, no CLI process or download";Passed=$true})
    }
    if([Environment]::GetEnvironmentVariable('Path','User') -cne $userPathBefore){throw 'User PATH changed.'}
    $results.Add([pscustomobject]@{Name='User PATH unchanged';Passed=$true})
    $receipt=Get-Content -LiteralPath (Join-Path $OutputDirectory 'install.json') -Raw -Encoding UTF8|ConvertFrom-Json
    [IO.File]::AppendAllText($receipt.ClaudeExecutable,'changed')
    $before=(Get-FileHash -LiteralPath $receipt.ClaudeExecutable).Hash
    $oldPreference=$ErrorActionPreference
    try{$ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -BundleDirectory $template -DestinationRoot $destination -ReportPath (Join-Path $OutputDirectory 'changed.json') -Install *> (Join-Path $OutputDirectory 'changed.log')}finally{$ErrorActionPreference=$oldPreference}
    if($LASTEXITCODE -ne 1 -or (Get-FileHash -LiteralPath $receipt.ClaudeExecutable).Hash -ne $before){throw 'An existing differing installation was modified.'}
    $results.Add([pscustomobject]@{Name='Existing differing install is preserved';Passed=$true})
    $protectedReport=Join-Path $OutputDirectory 'protected.json';'KEEP'|Set-Content -LiteralPath $protectedReport -Encoding ASCII
    $freshDestination=Join-Path $OutputDirectory 'must-not-create'
    try{$ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -BundleDirectory $template -DestinationRoot $freshDestination -ReportPath $protectedReport -Install *> (Join-Path $OutputDirectory 'protected.log')}finally{$ErrorActionPreference=$oldPreference}
    if($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $freshDestination) -or (Get-Content -LiteralPath $protectedReport -Raw).Trim() -ne 'KEEP'){throw 'An existing report caused filesystem mutations.'}
    $results.Add([pscustomobject]@{Name='Existing report rejected before any copy';Passed=$true})
    try{$ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $installer -BundleDirectory $template -DestinationRoot $freshDestination -ReportPath (Join-Path $OutputDirectory 'missing-parent/log.json') -Install *> (Join-Path $OutputDirectory 'missing-log-parent.log')}finally{$ErrorActionPreference=$oldPreference}
    if($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $freshDestination)){throw 'A missing log parent caused filesystem mutations.'}
    $results.Add([pscustomobject]@{Name='Missing log parent rejected before any copy';Passed=$true})
    Invoke-OfflineRejectCase 'Source junction rejected without following it' {param($r,$m) $null=New-Item -ItemType Junction -Path (Join-Path $r 'cli/codex/codex-resources/link') -Target $OutputDirectory}
    Invoke-OfflineRejectCase 'Manifest file symlink rejected before reading outside JSON' {param($r,$m) $path=Join-Path $r 'offline-manifest.json';$outside=Join-Path $OutputDirectory 'outside-manifest.json';Copy-Item -LiteralPath $path -Destination $outside;Move-Item -LiteralPath $path -Destination (Join-Path $r 'manifest-original.json');if(![OfflineTestLinks]::CreateSymbolicLink($path,$outside,2)){throw ('Test symlink creation failed: '+[Runtime.InteropServices.Marshal]::GetLastWin32Error())}}
    Invoke-OfflineRejectCase 'Oversized manifest rejected' {param($r,$m) [IO.File]::WriteAllText((Join-Path $r 'offline-manifest.json'),(' '*1048577))}
    Invoke-OfflineRejectCase 'Oversized Codex metadata rejected after valid new hash' {param($r,$m) [IO.File]::WriteAllText((Join-Path $r 'cli/codex/codex-package.json'),(' '*1048577));Save-OfflineTestManifest $r}
    $uncRejected=$false;try{Assert-OfflinePlainPath '\\offline-invalid-host\share\file'}catch{$uncRejected=$true}
    if(!$uncRejected){throw 'UNC paths must not be queried.'}
    $results.Add([pscustomobject]@{Name='UNC rejected before network filesystem access';Passed=$true})
    $prepareOutput=Join-Path $OutputDirectory 'unsigned-must-not-create'
    try{$ErrorActionPreference='Continue'; & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'scripts/Prepare-OfflineSetup.ps1') -CodexPackageDirectory (Join-Path $template 'cli/codex') -ClaudeExecutable (Join-Path $template 'cli/claude/claude.exe') -OutputDirectory $prepareOutput *> (Join-Path $OutputDirectory 'unsigned.log')}finally{$ErrorActionPreference=$oldPreference}
    if($LASTEXITCODE -ne 1 -or (Test-Path -LiteralPath $prepareOutput)){throw 'Unsigned preparer input was accepted.'}
    $results.Add([pscustomobject]@{Name='Preparation rejects unsigned binaries before output creation';Passed=$true})
    $tokens=$null;$errors=$null;$ast=[Management.Automation.Language.Parser]::ParseFile($installer,[ref]$tokens,[ref]$errors)
    $forbidden=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -match '^(Invoke-WebRequest|Invoke-RestMethod|Start-Process|winget|npm|curl|codex|claude)(\.exe)?$'},$true))
    if($errors.Count -or $forbidden.Count){throw 'Offline installer includes an external downloader or executable invocation.'}
    $results.Add([pscustomobject]@{Name='Offline installer AST has no downloader/CLI invocation';Passed=$true})
}finally{
    [pscustomobject]@{Task='TASK-038';TestKind='Synthetic PE payload and actual Windows PowerShell copy-only CLI';Passed=@($results.ToArray()|Where-Object Passed).Count;Checks=$results.ToArray();CustomerPcTested=$false}|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputDirectory 'results.json') -Encoding UTF8
}
Write-Host "Offline tests $($results.Count)/$($results.Count) PASS: $OutputDirectory"
