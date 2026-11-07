$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$view = [IO.File]::ReadAllText((Join-Path $root 'src/VS2017.AIAssistant/Views/AssistantControl.CommentCode.cs'))
$adapter = [IO.File]::ReadAllText((Join-Path $root 'src/VS2017.AIAssistant/Providers/CommentCodeProviderAdapter.cs'))
$start = $view.IndexOf('private async void CommentCodeRequested(')
$guard = $view.IndexOf('if (CommentCodeSafetyPause.IsPaused())', $start)
$notice = $view.IndexOf('MessageBox.Show(', $guard)
$stop = $view.IndexOf('return;', $notice)
$capture = $view.IndexOf('new CommentCodeEditorAdapter(', $start)
$providerStart = $adapter.IndexOf('public async Task<string> GenerateAsync(')
$providerGuard = $adapter.IndexOf('if (CommentCodeSafetyPause.IsPaused())', $providerStart)
$call = $adapter.IndexOf('proposals.GenerateWithContextAsync(', $providerStart)
if ($start -lt 0 -or $guard -le $start -or $notice -le $guard -or $stop -le $notice -or $capture -le $stop) { throw 'UI pause must report and return before editor capture.' }
if ($providerStart -lt 0 -or $providerGuard -le $providerStart -or $call -le $providerGuard) { throw 'Provider pause must run before the underlying provider call.' }
if ($adapter -notmatch 'internal static bool IsPaused\(\)\s*\{\s*return true;\s*\}') { throw 'Pause must be enforced in source without user configuration.' }
Write-Output 'PASS static pause wiring before editor capture and provider call.'
