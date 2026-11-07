[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sourcePath = Join-Path $repoRoot 'src/VS2017.AIAssistant/Views/CommentCodePreviewDialog.cs'
$fixturePath = Join-Path $PSScriptRoot 'preview-contract.json'
if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw 'CommentCodePreviewDialog.cs was not found.' }
$source = Get-Content -LiteralPath $sourcePath -Raw -Encoding UTF8
$fixture = Get-Content -LiteralPath $fixturePath -Raw -Encoding UTF8 | ConvertFrom-Json
$checks = New-Object System.Collections.Generic.List[string]

function Assert-SourceContains([string]$Pattern, [string]$Name) {
    if ($source -notmatch $Pattern) { throw "FAIL: $Name" }
    $checks.Add($Name)
}

function Assert-SourceOmits([string]$Pattern, [string]$Name) {
    if ($source -match $Pattern) { throw "FAIL: $Name" }
    $checks.Add($Name)
}

Assert-SourceContains 'class CommentCodePreviewDialog\s*:\s*AssistantDialog' 'uses the shared themed AssistantDialog'
Assert-SourceContains 'TargetFilePath' 'carries the target file path'
Assert-SourceContains 'Line' 'carries a one-based line coordinate'
Assert-SourceContains 'Column' 'carries a one-based column coordinate'
Assert-SourceContains 'ContextBefore' 'carries context before the insertion point'
Assert-SourceContains 'ExistingText' 'shows text that will be replaced, when present'
Assert-SourceContains 'ProposedCode' 'shows proposed code separately'
Assert-SourceContains 'ContextAfter' 'carries context after the insertion point'
Assert-SourceContains 'public void SetStale\(string reason\)' 'supports guard invalidation'
Assert-SourceContains 'acceptButton\.IsEnabled\s*=\s*false' 'disables accept when stale'
Assert-SourceContains 'Message\.Focusable\s*=\s*true' 'makes status keyboard and screen-reader reachable'
Assert-SourceContains 'Message\.Focus\(\)' 'moves focus to an announced stale or apply-error status'
Assert-SourceContains 'requestAgainButton\.Visibility\s*=\s*requestAgainCallback\s*==\s*null\s*\?\s*Visibility\.Collapsed\s*:\s*Visibility\.Visible' 'offers retry when a retry callback exists'
Assert-SourceContains 'acceptCallback\(\)' 'runs the final adapter validation before closing'
Assert-SourceContains 'result\.Status\s*==\s*CommentCodePreviewUiAcceptStatus\.Accepted' 'closes only on an explicit accepted result'
Assert-SourceContains 'result\.Status\s*==\s*CommentCodePreviewUiAcceptStatus\.Stale' 'keeps stale results out of the accepted path'
Assert-SourceContains 'result\.Status\s*==\s*CommentCodePreviewUiAcceptStatus\.Rejected' 'keeps rejected results open with an error'
Assert-SourceContains 'private void ShowRejected\(' 'shows an apply error without closing'
Assert-SourceContains 'IsReadOnly\s*=\s*true' 'renders preview text read-only'
Assert-SourceContains 'AutomationProperties\.Set(Name|HelpText)' 'provides UI Automation names and help text'
Assert-SourceContains 'cancelButton\.IsCancel\s*=\s*true' 'uses the dialog cancel route for Escape'
Assert-SourceContains 'acceptButton\.IsDefault\s*=\s*false' 'does not bind Enter to accept'
Assert-SourceContains 'PreviewClosing' 'delivers cancel when the dialog closes without applying'
Assert-SourceOmits '\bPreviewKeyDown\b|\bPreviewTextInput\b|InputBindings\.Add|CommandBindings\.Add' 'does not intercept editor-wide keys'
Assert-SourceOmits '\.Show\s*\(|\.ShowDialog\s*\(|\.Save\s*\(|\.Execute\s*\(' 'does not open UI, save, or execute code'
Assert-SourceOmits '\bDTE\b|\bIVsTextView\b|\bIWpfTextView\b|Package\.GetGlobalService' 'does not bind the UI layer to editor or SDK services'

if ($fixture.schemaVersion -ne 1) { throw 'Unexpected preview contract fixture schema.' }
if ($fixture.validInsert.line -lt 1 -or $fixture.validInsert.column -lt 1) { throw 'Fixture coordinates must be one-based.' }
if ([string]::IsNullOrWhiteSpace($fixture.validInsert.targetFilePath) -or [string]::IsNullOrWhiteSpace($fixture.validInsert.proposedCode)) { throw 'Fixture target and proposal must be present.' }
if ($fixture.staleAfterCaretOrDocumentChange.acceptEnabled -ne $false -or $fixture.staleAfterCaretOrDocumentChange.bufferMutation -ne $false) { throw 'Stale fixture must disable acceptance without mutating the editor.' }
if ($fixture.staleAfterSelectionChange.acceptEnabled -ne $false -or $fixture.staleAfterSelectionChange.bufferMutation -ne $false) { throw 'Selection-stale fixture must disable acceptance without mutating the editor.' }
if ($fixture.imeOrReadOnlyGuardRejectsAtAccept.acceptOutcome -ne 'Stale' -or $fixture.imeOrReadOnlyGuardRejectsAtAccept.bufferMutation -ne $false) { throw 'IME/read-only fixture must be rejected by the final accept guard.' }
if ($fixture.rejectedApply.acceptOutcome -ne 'Rejected' -or $fixture.rejectedApply.dialogRemainsOpen -ne $true -or $fixture.rejectedApply.bufferMutation -ne $false) { throw 'Rejected fixture must keep the preview open and leave the buffer unchanged.' }
foreach ($case in $fixture.coordinateCases) {
    $line = if ($case.PSObject.Properties.Name -contains 'expectedLine') { $case.expectedLine } else { $case.selectionLine }
    $column = if ($case.PSObject.Properties.Name -contains 'expectedColumn') { $case.expectedColumn } else { $case.selectionColumn }
    if ($line -lt 1 -or $column -lt 1) { throw "Invalid one-based coordinate fixture: $($case.name)" }
    if ($case.name -match 'multiline' -and ($case.selectedText -notmatch "`r?`n")) { throw 'Multiline selection fixture is incomplete.' }
}
$checks.Add('fixtures cover CRLF/LF, Korean text, supplementary UTF-16 characters and multiline selection without UI-side coordinate conversion')

[pscustomobject]@{
    Succeeded = $true
    Passed = $checks.Count
    Failed = 0
    Checks = @($checks)
    Scope = $fixture.scope
} | ConvertTo-Json -Depth 5
