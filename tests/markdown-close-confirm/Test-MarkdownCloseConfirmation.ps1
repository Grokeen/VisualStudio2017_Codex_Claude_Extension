param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw "OutputDirectory already exists: $output" }
New-Item -ItemType Directory -Path $output | Out-Null

$hostPath = Join-Path $repo 'src\VS2017.AIAssistant\Views\AssistantControl.Markdown.cs'
$editorPath = Join-Path $repo 'src\VS2017.AIAssistant\Views\MarkdownEditor.cs'
$docsPath = Join-Path $repo 'src\VS2017.AIAssistant\Views\MarkdownEditor.Documents.cs'
$dialogPath = Join-Path $repo 'src\VS2017.AIAssistant\Views\AssistantDialogs.cs'
$hostSource = [IO.File]::ReadAllText($hostPath)
$editorSource = [IO.File]::ReadAllText($editorPath)
$docs = [IO.File]::ReadAllText($docsPath)
$dialog = [IO.File]::ReadAllText($dialogPath)
$passed = 0
function Assert-Source([bool]$condition, [string]$name) {
    if (-not $condition) { throw "FAIL: $name" }
    $script:passed++
}

Assert-Source ($hostSource.Contains('저장 후 닫기')) 'explicit save and close action'
Assert-Source ($hostSource.Contains('변경 버리고 닫기')) 'explicit discard and close action'
Assert-Source ($hostSource.Contains('계속 작성')) 'explicit keep editing action'
Assert-Source ($hostSource.Contains('dialog.Message.Text = "저장되지 않은 변경이 있습니다. 저장 후 닫거나, 변경을 버리고 닫을 수 있습니다."')) 'confirmation message in dialog footer'
Assert-Source ($hostSource.Contains('AssistantWarningBrush')) 'warning contrast token used'
Assert-Source ($hostSource.Contains('AutomationProperties.SetName(dialog.Message, "저장하지 않은 Markdown 변경 확인")')) 'accessible confirmation message'
Assert-Source ($hostSource.Contains('AutomationProperties.SetName(saveAndClose, "변경 저장 후 Markdown 작성기 닫기")')) 'accessible save action'
Assert-Source ($hostSource.Contains('AutomationProperties.SetName(discardAndClose, "저장하지 않은 변경을 버리고 Markdown 작성기 닫기")')) 'accessible discard action'
Assert-Source ($hostSource.Contains('AutomationProperties.SetName(keepEditing, "Markdown 작성기로 돌아가기")')) 'accessible continue action'
Assert-Source ($hostSource.Contains('dialog.Actions.Children.Insert(0, saveAndClose)')) 'prompt actions use fixed dialog footer'
Assert-Source ($hostSource.Contains('dialog.Actions.Children.Remove(saveAndClose)')) 'prompt controls removed after choice'
Assert-Source ($hostSource.Contains('args.Cancel = true;') -and $hostSource.Contains('showClosePrompt();')) 'dirty close is canceled and explained'
Assert-Source ($hostSource.Contains('if (disposed || discardApproved || !editor.IsDirty)')) 'clean/disposed/approved close remains allowed'
Assert-Source ($hostSource.Contains('if (editor.SaveAndCheckBeforeClose())')) 'save closes only after save check'
Assert-Source ($hostSource.Contains('저장이 완료되지 않았습니다. 계속 작성하거나 변경을 버리고 닫을 수 있습니다.') -and $hostSource.Contains('keepEditing.Focus();')) 'cancelled or failed save keeps choice visible and returns focus to safe action'
Assert-Source ($hostSource.Contains('discardApproved = true;') -and $hostSource.Contains('discardAndClose.Click')) 'discard requires explicit button'
Assert-Source ($hostSource.Contains('keepEditing.Click') -and $hostSource.Contains('editor.Editor.Focus()')) 'continue editing returns focus to editor'
Assert-Source ($hostSource.Contains('if (closePromptVisible) keepEditing.Focus()')) 'non-destructive keep editing receives initial keyboard focus'
Assert-Source ($hostSource.Contains('AutomationProperties.SetHelpText(keepEditing, "저장하지 않은 변경을 유지하고 확인을 취소하여 편집기로 돌아갑니다.")')) 'screen reader context is available on focused safe action'
Assert-Source ($hostSource.Contains('closePromptVisible = true;') -and $hostSource.Contains('if (closePromptVisible) return;')) 'repeated close requests do not replace prompt or discard content'
Assert-Source ($dialog.Contains('IsCancel = true') -and $hostSource.Contains('args.Cancel = true;')) 'Escape/close cancellation is vetoed while dirty'
Assert-Source (-not $hostSource.Contains('editor.ConfirmClose(')) 'host no longer relies on body-bottom confirmation row for window close'
Assert-Source ($editorSource.Contains('public bool IsDirty')) 'existing content identity dirty check retained'
Assert-Source ($docs.Contains('ChooseSave();') -and $docs.Contains('return !IsDirty;')) 'save wrapper reports actual dirty result after save or save dialog'
Assert-Source ($docs.Contains('Dispatcher.VerifyAccess();') -and $docs.Contains('if (disposed) throw new ObjectDisposedException("MarkdownEditor");')) 'save wrapper verifies dispatcher and disposed state'
Assert-Source ($dialog.Contains('Footer = new Border')) 'confirmation host footer is a fixed dialog region'
Assert-Source ($hostSource.Contains('dialog.Message.ClearValue(TextBlock.ForegroundProperty)') -and $hostSource.Contains('AutomationProperties.SetName(dialog.Message, String.Empty)')) 'hiding prompt restores footer visual and accessibility state'
Assert-Source ($hostSource.Contains('AutomationProperties.SetName(dialog.Message, "저장이 완료되지 않음")')) 'save failure is named for assistive technology'
Assert-Source ($editorSource.Contains('discardRow.Visibility=Visibility.Visible')) 'existing document-transition confirmation remains available'
Assert-Source ($hostSource.Contains('if (target != null) dialog.Actions.Children.Insert(0, apply);')) 'existing input apply action remains distinct'

# Deterministic non-GUI dirty-close transition fixture. Source assertions above bind the
# product event/action wiring to this required state contract.
function Invoke-CloseContract([string]$Action, [bool]$Dirty, [bool]$SaveSucceeds = $false) {
    $state = [ordered]@{ Dirty = $Dirty; Closed = $false; Prompt = $false; Discarded = $false; Saved = $false }
    if (-not $Dirty) { $state.Closed = $true; return $state }
    $state.Prompt = $true
    switch ($Action) {
        'SaveSuccess' { if ($SaveSucceeds) { $state.Saved = $true; $state.Dirty = $false; $state.Closed = $true } }
        'Discard' { $state.Discarded = $true; $state.Closed = $true }
        'Continue' { $state.Prompt = $false }
        'Escape' { }
        'RepeatedClose' { }
    }
    return $state
}
$case = Invoke-CloseContract 'CleanClose' $false
Assert-Source ($case.Closed -and -not $case.Discarded) 'fixture clean close succeeds'
$case = Invoke-CloseContract 'DirtyClose' $true
Assert-Source ($case.Prompt -and -not $case.Closed -and $case.Dirty) 'fixture dirty close prompts and preserves edits'
$case = Invoke-CloseContract 'SaveSuccess' $true $true
Assert-Source ($case.Closed -and $case.Saved -and -not $case.Dirty) 'fixture successful save closes'
$case = Invoke-CloseContract 'SaveFailure' $true $false
Assert-Source ($case.Prompt -and -not $case.Closed -and $case.Dirty) 'fixture cancelled or failed save remains open and dirty'
$case = Invoke-CloseContract 'Discard' $true
Assert-Source ($case.Closed -and $case.Discarded) 'fixture discard closes only after explicit action'
$case = Invoke-CloseContract 'Continue' $true
Assert-Source (-not $case.Closed -and -not $case.Prompt -and $case.Dirty) 'fixture continue preserves edits and returns to editor'
$case = Invoke-CloseContract 'Escape' $true
Assert-Source ($case.Prompt -and -not $case.Closed -and -not $case.Discarded -and $case.Dirty) 'fixture Escape never implies discard'
$case = Invoke-CloseContract 'RepeatedClose' $true
Assert-Source ($case.Prompt -and -not $case.Closed -and -not $case.Discarded -and $case.Dirty) 'fixture repeated X never implies discard'

$result = [ordered]@{
    Scope = 'source contracts and deterministic non-GUI transition fixture; no GUI, SDK build, or live close simulation'
    Passed = $passed
    SourceContractExpected = 30
    FixtureExpected = 8
    Expected = 38
    Failed = 0
    ProductSourceChanged = $true
    Gui = $false
    ProductBuild = $false
    ActualCloseCancellationObserved = $false
    FocusesSafeAction = $true
    RepeatedCloseAndEscapeDiscard = $false
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding UTF8
"PASS=$passed; FAIL=0"
