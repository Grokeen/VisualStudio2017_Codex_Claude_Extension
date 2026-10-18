#requires -Version 5.1
<#
-------------------------------------------------------------------------
name         : Invoke-LauncherSuite
desc         : 테스트 실행기가 지정한 허용 목록의 검사만 PowerShell 5.1 STA에서 실행한다.
author       : CodexCode
create date  : 2026-10-18
update date  : 2026-10-18 CodexCode : 고정 스크립트·루트 내부 경로·실패 종료 코드 검증.
update date  : 2026-10-18 CodexOrig : TASK-016 문서·모델·AI 협업과 실제 비동기 UI 회귀 연결.
update date  : 2026-10-18 CodexOrig : TASK-018 드롭다운·이미지 요구 및 기본14개 검사 반영.
               2026-10-19 CodexOrig : TASK-025 신규 UI·Markdown·안내 검사를 기본 묶음에 순차 연결.
-------------------------------------------------------------------------
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('compatibility','build-entry','markdown','settings','workspace-actions','editing','foundation','experience','documents','collaboration','provider-options','provider-cli','scm-identity','image-requirements','image-followup','native-runtime','ide','live-codex')][string]$Suite,
    [string]$AssemblyPath,
    [string]$VsixPath,
    [string]$OutputDirectory,
    [string]$CodexPath
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
try {
    if ($PSVersionTable.PSEdition -ne 'Desktop' -or [Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Windows PowerShell 5.1 -STA로 실행하세요.' }
    $launcherRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
    $suiteArguments = @{}
    switch ($Suite) {
        'compatibility' { $scriptRelativePath = 'tests\compatibility\Test-VsixCompatibility.Tests.ps1' }
        'build-entry' { $scriptRelativePath = 'tests\build-entry\Test-BuildEntry.ps1' }
        'markdown' { $scriptRelativePath = 'tests\workspace\Test-MarkdownWorkspace.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath }
        'settings' { $scriptRelativePath = 'tests\workspace\Test-SettingsTransaction.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath }
        'workspace-actions' { $scriptRelativePath = 'tests\workspace-actions\Test-WorkspaceActions.ps1'; $suiteArguments.ProductAssemblyPath = $AssemblyPath }
        'editing' { $scriptRelativePath = 'tests\editing\Test-SourceEditing.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath }
        'foundation' { $scriptRelativePath = 'tests\foundation\Test-FoundationPackage.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.VsixPath = $VsixPath; $suiteArguments.OutputDirectory = $OutputDirectory }
        'experience' { $scriptRelativePath = 'tests\review\Test-WorkspaceExperience.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.OutputDirectory = $OutputDirectory }
        'documents' { $scriptRelativePath = 'tests\documents\Test-DocumentAttachments.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.WorkerPath = Join-Path ([IO.Path]::GetDirectoryName($AssemblyPath)) 'VS2017.AIAssistant.DocumentReader.exe' }
        'collaboration' { $scriptRelativePath = 'tests\collaboration\Test-ConversationCollaboration.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath }
        'provider-options' { $scriptRelativePath = 'tests\provider-options\Test-ProviderOptions.ps1'; $suiteArguments.ProductAssemblyPath = $AssemblyPath }
        'provider-cli' { $scriptRelativePath = 'tests\provider-options\Test-ProviderCliIntegration.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath }
        'scm-identity' { $scriptRelativePath = 'tests\provider-options\Test-ScmIdentity.ps1'; $suiteArguments.ProductAssemblyPath = $AssemblyPath }
        'image-requirements' { $scriptRelativePath = 'tests\review\Test-ImageRequirements.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.OutputDirectory = $OutputDirectory }
        'image-followup' { $scriptRelativePath = 'tests\task021\Test-Task021.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.OutputDirectory = $OutputDirectory }
        'native-runtime' { $scriptRelativePath = 'tests\task020\Test-NativeRuntime.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.OutputDirectory = $OutputDirectory }
        'ide' { $scriptRelativePath = 'tests\ide\Test-IdeIntegration.ps1'; $suiteArguments.VsixPath = $VsixPath }
        'live-codex' { $scriptRelativePath = 'tests\review\Test-LiveChatContext.ps1'; $suiteArguments.AssemblyPath = $AssemblyPath; $suiteArguments.CodexPath = $CodexPath }
    }
    $scriptFullPath = [IO.Path]::GetFullPath((Join-Path $launcherRoot $scriptRelativePath))
    if (!$scriptFullPath.StartsWith($launcherRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw '검사 스크립트가 프로젝트 폴더를 벗어났습니다.' }
    if (!(Test-Path -LiteralPath $scriptFullPath -PathType Leaf)) { throw "검사 스크립트가 없습니다: $scriptFullPath" }
    foreach ($key in @('AssemblyPath','ProductAssemblyPath','VsixPath','CodexPath')) {
        if ($suiteArguments.ContainsKey($key) -and !(Test-Path -LiteralPath $suiteArguments[$key] -PathType Leaf)) { throw "$key 파일을 찾지 못했습니다." }
    }
    Set-Location -LiteralPath $launcherRoot
    $env:VS2017_AI_DISABLE_RUNTIME_DISCOVERY = '1'
    Write-Output ('검사 시작: ' + $Suite)
    $global:LASTEXITCODE = 0
    & $scriptFullPath @suiteArguments
    $suiteSucceeded = $?
    $suiteExitCode = $LASTEXITCODE
    if ($suiteExitCode -ne 0) { exit $suiteExitCode }
    if (!$suiteSucceeded) { exit 1 }
    if ($Suite -eq 'image-followup') {
        $followupDirectory = Join-Path $OutputDirectory 'task022'
        & (Join-Path $launcherRoot 'tests\task022\Test-Task022.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $followupDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (!$?) { exit 1 }
        $latestDirectory = Join-Path $OutputDirectory 'task024'
        & (Join-Path $launcherRoot 'tests\task024\Test-Task024.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $latestDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (!$?) { exit 1 }
        $themeDirectory = Join-Path $OutputDirectory 'task026'
        & (Join-Path $launcherRoot 'tests\task026\Test-Task026.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $themeDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (!$?) { exit 1 }
        $auditDirectory = Join-Path $OutputDirectory 'audit-followup'
        & (Join-Path $launcherRoot 'tests\task024\Test-MarkdownAuditFollowup.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $auditDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (!$?) { exit 1 }
        $documentUiDirectory = Join-Path $OutputDirectory 'document-ui'
        & (Join-Path $launcherRoot 'tests\task026\Test-DocumentUi.ps1') -AssemblyPath $AssemblyPath -OutputDirectory $documentUiDirectory
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        if (!$?) { exit 1 }
    }
    Write-Output ('검사 완료: ' + $Suite)
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    [Console]::Error.WriteLine($_.ScriptStackTrace)
    exit 1
}
